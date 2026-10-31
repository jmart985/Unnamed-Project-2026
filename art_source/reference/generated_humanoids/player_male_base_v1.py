"""Fit Humanoid Rig v1 to Blender's untouched realistic male base mesh.

The sculpt face sets belong to the preserved Blender source, not gameplay
anatomy IDs. Their shared boundary loops locate the actual source joints.
Only the armature changes here; the normalized body vertices never move.
"""
from __future__ import annotations

import bpy
import json
import struct
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree


SOURCE_BODY_SETS = {
    "l": {"upperarm": 21, "lowerarm": 12, "hand": 9,
          "thigh": 24, "calf": 15, "foot": 14,
          "fingers": {"thumb": 80, "index": 76, "middle": 72, "ring": 68, "pinky": 64}},
    "r": {"upperarm": 20, "lowerarm": 11, "hand": 10,
          "thigh": 23, "calf": 16, "foot": 13,
          "fingers": {"thumb": 84, "index": 88, "middle": 92, "ring": 96, "pinky": 100}},
}


def fit_rig_to_base(body, armature):
    points = [body.matrix_world @ vertex.co for vertex in body.data.vertices]
    face_sets = body.data.attributes.get(".sculpt_face_set")
    if face_sets is None or face_sets.domain != "FACE":
        raise RuntimeError("Blender male source is missing its anatomical sculpt face sets.")
    regions = {}
    for face, value in zip(body.data.polygons, face_sets.data):
        regions.setdefault(value.value, set()).update(face.vertices)

    def center(indices):
        if not indices:
            raise RuntimeError("The Blender male source has an empty joint boundary.")
        return sum((points[i] for i in indices), Vector()) / len(indices)

    def joint(first, second):
        return center(regions[first] & regions[second])

    def tip(region, start, direction):
        direction = direction.normalized()
        furthest = max((points[i] - start).dot(direction) for i in regions[region])
        return center({i for i in regions[region]
                       if (points[i] - start).dot(direction) >= furthest - 0.003})

    # Reset all template animation before editing the rest skeleton. Retain its
    # semantic names and parent hierarchy so gameplay and sockets stay stable.
    armature.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for bone in armature.pose.bones:
        bone.matrix_basis.identity()
        for constraint in list(bone.constraints):
            bone.constraints.remove(constraint)

    waist = joint(18, 19)
    abdomen = joint(19, 1)
    neck = joint(1, 17)
    chest = abdomen.lerp(neck, 0.5)
    hip_center = (joint(18, 24) + joint(18, 23)) / 2
    shoulder_center = (joint(1, 21) + joint(1, 20)) / 2
    head_base = neck + Vector((0, -0.025, 0.065))
    head_top = Vector((head_base.x, head_base.y, max(p.z for p in points) - 0.015))
    positions = {
        "root": (Vector(), Vector((0, 0, 0.12))),
        "pelvis": (hip_center, waist),
        "spine_01": (waist, abdomen),
        "spine_02": (abdomen, chest),
        "spine_03": (chest, neck),
        "neck_01": (neck, head_base),
        "head": (head_base, head_top),
        "head_end": (head_top, head_top + Vector((0, 0, 0.02))),
    }
    for side, source in SOURCE_BODY_SETS.items():
        sign = 1 if side == "l" else -1
        shoulder = joint(1, source["upperarm"])
        elbow = joint(source["upperarm"], source["lowerarm"])
        wrist = joint(source["lowerarm"], source["hand"])
        palm = joint(source["hand"], source["fingers"]["middle"])
        hip = joint(18, source["thigh"])
        knee = joint(source["thigh"], source["calf"])
        ankle = joint(source["calf"], source["foot"])
        toe_base = Vector((ankle.x + sign * 0.045, -0.035, 0.035))
        toe_tip = Vector((ankle.x + sign * 0.055, -0.098, 0.025))
        positions.update({
            f"clavicle_{side}": (shoulder_center + Vector((sign * 0.035, 0, 0)), shoulder),
            f"upperarm_{side}": (shoulder, elbow),
            f"lowerarm_{side}": (elbow, wrist),
            f"hand_{side}": (wrist, palm),
            f"thigh_{side}": (hip, knee),
            f"calf_{side}": (knee, ankle),
            f"foot_{side}": (ankle, toe_base),
            f"toe_{side}": (toe_base, toe_tip),
            f"ball_{side}_end": (toe_tip, toe_tip + Vector((0, -0.01, 0))),
        })
        for finger, first in source["fingers"].items():
            base = joint(source["hand"], first)
            middle = joint(first, first + 1)
            distal = joint(first + 1, first + 2)
            end = tip(first + 2, distal, distal - middle)
            for number, start, finish in ((1, base, middle), (2, middle, distal), (3, distal, end)):
                positions[f"{finger}_{number:02}_{side}"] = (start, finish)
            positions[f"{finger}_03_{side}_end"] = (end, end + (end - distal).normalized() * 0.005)

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    inverse = armature.matrix_world.inverted()
    missing = set(armature.data.edit_bones.keys()) - set(positions)
    if missing:
        raise RuntimeError(f"The male base fitter does not cover canonical bones: {sorted(missing)}")
    for bone in armature.data.edit_bones:
        bone.use_connect = False
    for name, (start, end) in positions.items():
        bone = armature.data.edit_bones[name]
        bone.head = inverse @ start
        bone.tail = inverse @ end
        bone.align_roll(Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    body["source_body"] = "Blender Human Base Meshes v1.4.1 / GEO-body_male_realistic"
    body["source_shape_preserved"] = True
    bpy.context.scene["male_base_rig_fit"] = "source face-set boundary loops; uniform body normalization only"
    print(f"Fitted {len(positions)} canonical bones to the untouched Blender male body.")


def validate_source_shape(body, source_path):
    """Require every original source vertex, not just a name or vertex count."""
    with bpy.data.libraries.load(str(source_path), link=False) as (_, loaded):
        loaded.objects = ["BlenderMaleBodyBase"]
    source = loaded.objects[0]
    source_points = [source.matrix_world @ vertex.co for vertex in source.data.vertices]
    lo = Vector(tuple(min(p[i] for p in source_points) for i in range(3)))
    hi = Vector(tuple(max(p[i] for p in source_points) for i in range(3)))
    scale = 1.75 / (hi.z - lo.z)
    expected = [Vector(((p.x - (lo.x + hi.x) / 2) * scale,
                        (p.y - (lo.y + hi.y) / 2) * scale - 0.0076,
                        (p.z - lo.z) * scale + 0.01)) for p in source_points]
    actual = [body.matrix_world @ vertex.co for vertex in body.data.vertices]
    if len(actual) != len(expected):
        raise RuntimeError(f"Male base topology changed: {len(actual)} vertices instead of {len(expected)}.")
    error = max((a - b).length for a, b in zip(actual, expected))
    bpy.data.objects.remove(source, do_unlink=True)
    if error > 0.00001:
        raise RuntimeError(f"Male base geometry differs from normalized Blender source by {error:.6f} m.")
    print(f"MALE BASE SOURCE SHAPE PASSED: {len(actual)} vertices, max source deviation {error:.8f} m.")


def validate_exported_source_shape(path, body):
    """Inspect GLB world positions so a correct .blend cannot mask a bad export."""
    blob = path.read_bytes()
    json_length = struct.unpack_from("<I", blob, 12)[0]
    document = json.loads(blob[20:20 + json_length])
    binary_start = 20 + json_length + 8
    nodes = document["nodes"]
    parents = {child: parent for parent, node in enumerate(nodes) for child in node.get("children", [])}

    def world(index):
        node = nodes[index]
        if "matrix" in node:
            flat = node["matrix"]
            local = Matrix([flat[i:i + 4] for i in range(0, 16, 4)]).transposed()
        else:
            x, y, z, w = node.get("rotation", (0, 0, 0, 1))
            local = Matrix.LocRotScale(Vector(node.get("translation", (0, 0, 0))),
                                       Quaternion((w, x, y, z)),
                                       Vector(node.get("scale", (1, 1, 1))))
        return world(parents[index]) @ local if index in parents else local

    body_nodes = [i for i, node in enumerate(nodes) if node.get("name") == "HumanBody"]
    if len(body_nodes) != 1:
        raise RuntimeError("Expected exactly one Blender male body in the exported GLB.")
    tree = KDTree(len(body.data.vertices))
    for vertex in body.data.vertices:
        tree.insert(body.matrix_world @ vertex.co, vertex.index)
    tree.balance()
    found = set()
    maximum_error = 0.0
    for primitive in document["meshes"][nodes[body_nodes[0]]["mesh"]]["primitives"]:
        accessor = document["accessors"][primitive["attributes"]["POSITION"]]
        view = document["bufferViews"][accessor["bufferView"]]
        if accessor["componentType"] != 5126 or accessor["type"] != "VEC3" or "sparse" in accessor:
            raise RuntimeError("Unsupported male body position layout in GLB.")
        start = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", 12)
        for i in range(accessor["count"]):
            point = world(body_nodes[0]) @ Vector(struct.unpack_from("<3f", blob, start + i * stride))
            _, source_index, error = tree.find(Vector((point.x, -point.z, point.y)))
            maximum_error = max(maximum_error, error)
            found.add(source_index)
    if maximum_error > 0.00002 or len(found) != len(body.data.vertices):
        raise RuntimeError(f"Export changed Blender male shape: {maximum_error:.7f} m error; "
                           f"{len(found)}/{len(body.data.vertices)} source vertices preserved.")
    print(f"MALE BASE GLB SHAPE PASSED: {len(found)} source vertices, max error {maximum_error:.8f} m.")
