"""Transfer acquired morph-animation curves to the shared facial bone layer.

Source shapes stay in their own assets. A nearest-surface correspondence on the
normalized head and a regularized skin-weight fit approximate their displacement
with the target's supported facial controls. No source body geometry is copied.
"""
import json
import math
import struct
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
FACE_BONES = ("jaw", "brow_l", "brow_r", "eyelid_l", "eyelid_r",
              "mouth_corner_l", "mouth_corner_r")


def load_glb(path):
    raw = path.read_bytes()
    length = struct.unpack_from("<I", raw, 12)[0]
    doc = json.loads(raw[20:20 + length])
    binary = raw[28 + length:]
    def accessor(index):
        item = doc["accessors"][index]
        dtype = {5126: "<f4", 5123: "<u2", 5125: "<u4", 5121: "u1"}[item["componentType"]]
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[item["type"]]
        result = np.zeros((item["count"], width), dtype=dtype)
        if "bufferView" in item:
            view = doc["bufferViews"][item["bufferView"]]
            offset = view.get("byteOffset", 0) + item.get("byteOffset", 0)
            stride = view.get("byteStride", np.dtype(dtype).itemsize * width)
            result = np.ndarray((item["count"], width), dtype=dtype, buffer=binary,
                              offset=offset, strides=(stride, np.dtype(dtype).itemsize)).copy()
        if "sparse" in item:
            sparse = item["sparse"]
            indices = sparse["indices"]
            index_type = {5121: "u1", 5123: "<u2", 5125: "<u4"}[indices["componentType"]]
            index_offset = doc["bufferViews"][indices["bufferView"]].get("byteOffset", 0) + indices.get("byteOffset", 0)
            value_offset = doc["bufferViews"][sparse["values"]["bufferView"]].get("byteOffset", 0) + sparse["values"].get("byteOffset", 0)
            rows = np.frombuffer(binary, dtype=index_type, count=sparse["count"], offset=index_offset)
            result[rows] = np.frombuffer(binary, dtype=dtype, count=sparse["count"] * width,
                                        offset=value_offset).reshape(-1, width)
        return result
    parents = {child: i for i, node in enumerate(doc["nodes"]) for child in node.get("children", [])}
    def world(index):
        node = doc["nodes"][index]
        if "matrix" in node:
            local = Matrix(np.array(node["matrix"]).reshape(4, 4).T)
        else:
            q = node.get("rotation", [0, 0, 0, 1])
            local = Matrix.LocRotScale(Vector(node.get("translation", [0, 0, 0])),
                Quaternion((q[3], *q[:3])), Vector(node.get("scale", [1, 1, 1])))
        return world(parents[index]) @ local if index in parents else local
    return doc, accessor, world


def add_source_expressions(body, rig):
    report = []
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    face = [v for v in body.data.vertices if (body.matrix_world @ v.co).z > head.z - .025]
    weights = np.array([[next((g.weight for g in v.groups
                       if body.vertex_groups[g.group].name == name), 0)
                       for name in FACE_BONES] for v in face])
    target_top = max((body.matrix_world @ v.co).z for v in face)
    for slug, body_name, head_name, flip in (
        ("dwight", "Dwight Fairfield", "head", True),
        ("yuffie", "Genesis 8.1 Female Mesh", "head", False),
    ):
        path = ROOT / f"game/assets/humanoids/human/npc/{slug}.glb"
        doc, read, world = load_glb(path)
        node_index = next(i for i, n in enumerate(doc["nodes"]) if n.get("name") == body_name)
        node = doc["nodes"][node_index]
        primitives = doc["meshes"][node["mesh"]]["primitives"]
        target_names = doc["meshes"][node["mesh"]].get("extras", {}).get("targetNames", [])
        if not primitives[0].get("targets"):
            continue
        head_index = next(i for i, n in enumerate(doc["nodes"]) if n.get("name") == head_name)
        convert = Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
        positions, deltas = [], []
        transform = convert @ world(node_index)
        for primitive in primitives:
            positions.extend([transform @ Vector(p) for p in read(primitive["attributes"]["POSITION"])])
            deltas.extend(np.stack([read(t["POSITION"]) for t in primitive["targets"]], axis=1))
        source_head = convert @ world(head_index).translation
        top = max(p.z for p in positions)
        scale = (target_top - head.z) / max(.04, top - source_head.z)
        candidates = []
        for rotation in (0, math.pi):
            orientation = Matrix.Rotation(rotation, 4, "Z")
            tree = KDTree(len(positions))
            for i, p in enumerate(positions):
                tree.insert(head + orientation @ (p - source_head) * scale, i)
            tree.balance()
            nearest = [tree.find(body.matrix_world @ v.co)[1] for v in face]
            basis = np.array((orientation @ transform).to_3x3(), dtype=float) * scale
            mapped = np.array(deltas)[nearest] @ basis.T
            score = float(np.sum(np.linalg.norm(mapped,axis=2) * weights.sum(axis=1)[:,None]))
            candidates.append((score,rotation,mapped))
        _,rotation,mapped = max(candidates,key=lambda candidate:candidate[0])
        for clip in doc.get("animations", []):
            if not clip["name"].startswith("npc_face_"):
                continue
            channel = next((c for c in clip["channels"]
                if c["target"]["node"] == node_index and c["target"]["path"] == "weights"), None)
            if channel is None:
                continue
            sampler = clip["samplers"][channel["sampler"]]
            if sampler.get("interpolation", "LINEAR") != "LINEAR":
                raise RuntimeError("Facial retargeting requires linear source weight samples.")
            times = read(sampler["input"]).ravel()
            values = read(sampler["output"]).reshape(len(times), mapped.shape[1])
            # These project-exported clips share a scalar envelope across all
            # morph channels. Select the named source shape, avoiding an
            # unintended simultaneous happy/sad/angry facial pose.
            target_index = target_names.index(clip["name"])
            envelope = values[:, target_index].copy()
            values[:] = 0
            values[:, target_index] = envelope
            name = "source_" + slug + "_" + clip["name"].removeprefix("npc_face_")
            action = bpy.data.actions.new(name)
            rig.animation_data.action = action
            errors, peak = [], 0.0
            for i in range(9):
                time = float(times[0] + (times[-1] - times[0]) * i / 8)
                morph = np.array([np.interp(time, times, values[:, j]) for j in range(values.shape[1])])
                desired = np.einsum("vmc,m->vc", mapped, morph)
                regularized = weights.T @ weights + np.eye(len(FACE_BONES)) * .08
                movement = np.linalg.solve(regularized, weights.T @ desired)
                # Coarse optional controls cannot reproduce every facial morph.
                movement = np.clip(movement, -.012, .012)
                peak = max(peak, float(np.max(np.abs(movement))))
                errors.append(float(np.sqrt(np.mean((weights @ movement - desired) ** 2))))
                frame = 1 + round((time - times[0]) * 30)
                for name_at_bone, displacement in zip(FACE_BONES, movement):
                    bone = rig.pose.bones[name_at_bone]
                    inverse = (rig.matrix_world @ bone.bone.matrix_local).to_3x3().inverted()
                    bone.location = inverse @ Vector(displacement)
                    bone.keyframe_insert("location", frame=frame)
            action.use_fake_user = True
            action.use_cyclic = False
            report.append({"clip": name, "source": str(path.relative_to(ROOT)),
                "sourceClip": clip["name"], "method": "facial morph to supported bone least-squares fit",
                "sourceShape": target_names[target_index], "headAlignmentDegrees": round(math.degrees(rotation)),
                "peakControlDisplacementM": peak, "maxRmsDisplacementErrorM": max(errors)})
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    rig.animation_data.action = bpy.data.actions["human_idle"]
    bpy.context.scene.frame_set(1)
    return report
