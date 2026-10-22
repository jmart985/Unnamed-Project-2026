"""Generate the first project-owned ape/primate creature asset.

This remains deliberately procedural and editable. The visual anatomy is
primate-specific while the gameplay layer consumes semantic anatomy and a
family mapping rather than arbitrary mesh node names.
"""

from __future__ import annotations

import importlib.util
import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1] / "reference" / "generated_creatures"
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "ape"
MODEL_NAME = "ape_primate_rig_v1"

_canid_path = Path(__file__).with_name("generate_creature_canid_v1.py")
_spec = importlib.util.spec_from_file_location("canid_helpers", _canid_path)
_canid = importlib.util.module_from_spec(_spec)
assert _spec and _spec.loader
_spec.loader.exec_module(_canid)
uv_sphere = _canid.uv_sphere
capsule_between = _canid.capsule_between
cone = _canid.cone
material = _canid.material


FUR = material("Ape_Fur", (0.20, 0.12, 0.075))
FUR_LIGHT = material("Ape_FurLight", (0.34, 0.22, 0.14))
FACE = material("Ape_Face", (0.34, 0.22, 0.16))
PALM = material("Ape_Palm", (0.24, 0.15, 0.10))
EYE = material("Ape_Eye", (0.035, 0.018, 0.01), 0.25)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # Broad shoulders, deep chest, tapered waist, and a slightly raised rump.
    parts += [uv_sphere("Torso", (0, 0.02, 1.22), (0.58, 0.47, 0.66), FUR)]
    parts += [uv_sphere("Chest", (0, -0.38, 1.25), (0.62, 0.38, 0.54), FUR_LIGHT)]
    parts += [uv_sphere("Rump", (0, 0.43, 1.18), (0.48, 0.34, 0.46), FUR)]
    parts += [uv_sphere("Brow", (0, -0.72, 1.74), (0.30, 0.24, 0.18), FUR)]
    parts += [uv_sphere("Head", (0, -0.67, 1.62), (0.34, 0.32, 0.34), FUR)]
    # A short, broad muzzle and heavy brow read as an ape rather than a canid.
    parts += [uv_sphere("Muzzle", (0, -0.88, 1.52), (0.22, 0.20, 0.18), FACE)]
    parts += [uv_sphere("Nose", (0, -1.02, 1.57), (0.13, 0.08, 0.08), PALM)]
    parts += [uv_sphere("Jaw", (0, -0.87, 1.40), (0.24, 0.20, 0.14), FACE)]
    for side in (-1, 1):
        suffix = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Eye_{suffix}", (0.20 * side, -0.91, 1.70), (0.045, 0.035, 0.045), EYE, 14, 9)]
        parts += [uv_sphere(f"Ear_{suffix}", (0.34 * side, -0.67, 1.63), (0.12, 0.07, 0.10), FACE, 14, 9)]

    limbs = {
        # Long forelimbs place the broad hands near the ground for knuckle walking.
        "Arm_L": (Vector((-0.48, -0.24, 1.48)), Vector((-0.58, -0.28, 0.84)), Vector((-0.56, -0.42, 0.25))),
        "Arm_R": (Vector((0.48, -0.24, 1.48)), Vector((0.58, -0.28, 0.84)), Vector((0.56, -0.42, 0.25))),
        "Leg_L": (Vector((-0.30, 0.32, 1.02)), Vector((-0.32, 0.28, 0.58)), Vector((-0.32, 0.14, 0.15))),
        "Leg_R": (Vector((0.30, 0.32, 1.02)), Vector((0.32, 0.28, 0.58)), Vector((0.32, 0.14, 0.15))),
    }
    for name, (hip, knee, wrist_or_foot) in limbs.items():
        is_arm = name.startswith("Arm")
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.14 if is_arm else 0.16, FUR)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.15, 0.15, 0.15), FUR_LIGHT, 14, 9)]
        parts += [capsule_between(f"{name}_Lower", knee, wrist_or_foot, 0.12 if is_arm else 0.13, FUR)]
        if is_arm:
            parts += [uv_sphere(f"{name}_Hand", (wrist_or_foot.x, wrist_or_foot.y - 0.06, wrist_or_foot.z), (0.20, 0.24, 0.13), PALM, 14, 9)]
            # Separate knuckle pads make the hand useful for future contacts.
            for finger in range(4):
                parts += [uv_sphere(f"{name}_Knuckle_{finger + 1}", (wrist_or_foot.x + (finger - 1.5) * 0.065, wrist_or_foot.y - 0.21, wrist_or_foot.z - 0.04), (0.040, 0.08, 0.045), PALM, 10, 6)]
        else:
            parts += [uv_sphere(f"{name}_Foot", (wrist_or_foot.x, wrist_or_foot.y + 0.02, wrist_or_foot.z), (0.17, 0.23, 0.10), PALM, 14, 9)]

    # A short expressive tail remains optional in the semantic contract.
    tail_points = [Vector((0, 0.70, 1.21)), Vector((0, 0.91, 1.12)), Vector((0, 1.04, 1.00))]
    for index in range(len(tail_points) - 1):
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.07 - index * 0.018, FUR)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Ape_Primate_Rig_v1"
    armature.data.name = "Ape_Primate_Rig_v1"
    armature.show_in_front = True
    edit = armature.data.edit_bones
    for old in list(edit):
        edit.remove(old)

    def bone(name: str, head: tuple[float, float, float], tail: tuple[float, float, float], parent: str | None = None) -> None:
        b = edit.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = edit[parent]

    bone("root", (0, 0, 0), (0, 0, 0.12))
    bone("pelvis", (0, 0.35, 1.08), (0, 0.10, 1.16), "root")
    bone("spine_01", (0, 0.10, 1.16), (0, -0.12, 1.23), "pelvis")
    bone("spine_02", (0, -0.12, 1.23), (0, -0.32, 1.30), "spine_01")
    bone("spine_03", (0, -0.32, 1.30), (0, -0.50, 1.38), "spine_02")
    bone("neck_01", (0, -0.50, 1.38), (0, -0.67, 1.60), "spine_03")
    bone("head", (0, -0.67, 1.60), (0, -0.82, 1.68), "neck_01")
    bone("jaw", (0, -0.82, 1.48), (0, -1.05, 1.42), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"clavicle_{side}", (0.40 * x, -0.32, 1.47), (0.55 * x, -0.28, 1.48), "spine_03")
        bone(f"upperarm_{side}", (0.55 * x, -0.28, 1.48), (0.58 * x, -0.28, 0.84), f"clavicle_{side}")
        bone(f"lowerarm_{side}", (0.58 * x, -0.28, 0.84), (0.56 * x, -0.42, 0.25), f"upperarm_{side}")
        bone(f"hand_{side}", (0.56 * x, -0.42, 0.25), (0.56 * x, -0.60, 0.20), f"lowerarm_{side}")
        bone(f"knuckle_{side}", (0.56 * x, -0.60, 0.20), (0.56 * x, -0.70, 0.14), f"hand_{side}")
        bone(f"thigh_{side}", (0.30 * x, 0.32, 1.03), (0.32 * x, 0.28, 0.58), "pelvis")
        bone(f"calf_{side}", (0.32 * x, 0.28, 0.58), (0.32 * x, 0.14, 0.15), f"thigh_{side}")
        bone(f"foot_{side}", (0.32 * x, 0.14, 0.15), (0.32 * x, -0.05, 0.12), f"calf_{side}")
        bone(f"toe_{side}", (0.32 * x, -0.05, 0.12), (0.32 * x, -0.16, 0.12), f"foot_{side}")
        bone(f"ear_{side}", (0.34 * x, -0.67, 1.63), (0.43 * x, -0.67, 1.63), "head")
    for index, point in enumerate([(0, 0.91, 1.12), (0, 1.04, 1.00)], 1):
        parent = "pelvis" if index == 1 else "tail_01"
        bone(f"tail_{index:02d}", point, (point[0], point[1] + 0.12, point[2] - 0.10), parent)
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def attach_to_bones(parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    for part in parts:
        bone_name = "spine_02"
        name = part.name
        if any(name.startswith(prefix) for prefix in ("Head", "Muzzle", "Nose", "Jaw", "Brow", "Eye", "Ear")):
            bone_name = "head"
        elif name.startswith("Tail"):
            bone_name = "tail_01" if "01" in name else "tail_02"
        elif name.startswith("Arm_"):
            side = "l" if "_L" in name else "r"
            bone_name = f"upperarm_{side}" if "Upper" in name else f"lowerarm_{side}" if "Lower" in name or "Joint" in name else f"hand_{side}"
        elif name.startswith("Leg_"):
            side = "l" if "_L" in name else "r"
            bone_name = f"thigh_{side}" if "Upper" in name else f"calf_{side}" if "Lower" in name or "Joint" in name else f"foot_{side}"
        part.parent = armature
        group = part.vertex_groups.new(name=bone_name)
        group.add(list(range(len(part.data.vertices))), 1.0, "REPLACE")
        modifier = part.modifiers.new("PrimateArmature", "ARMATURE")
        modifier.object = armature


def action(armature: bpy.types.Object, name: str, poses: list[tuple[int, dict[str, tuple[float, float, float]]]]) -> None:
    clip = bpy.data.actions.new(name)
    clip.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = clip
    for frame, pose in poses:
        bpy.context.scene.frame_set(frame)
        for bone_name, rotation in pose.items():
            if bone_name in armature.pose.bones:
                pb = armature.pose.bones[bone_name]
                pb.rotation_euler = rotation
                pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)


def create_actions(armature: bpy.types.Object) -> None:
    action(armature, "idle", [(1, {}), (24, {"head": (0.03, 0, 0)}), (48, {})])
    action(armature, "walk", [(1, {"upperarm_l": (0.30, 0, 0), "upperarm_r": (-0.30, 0, 0), "thigh_l": (-0.18, 0, 0), "thigh_r": (0.18, 0, 0)}), (16, {"upperarm_l": (-0.30, 0, 0), "upperarm_r": (0.30, 0, 0), "thigh_l": (0.18, 0, 0), "thigh_r": (-0.18, 0, 0)}), (32, {"upperarm_l": (0.30, 0, 0), "upperarm_r": (-0.30, 0, 0), "thigh_l": (-0.18, 0, 0), "thigh_r": (0.18, 0, 0)})])
    action(armature, "knuckle_walk", [(1, {"upperarm_l": (0.50, 0, 0), "upperarm_r": (-0.50, 0, 0), "thigh_l": (-0.36, 0, 0), "thigh_r": (0.36, 0, 0)}), (12, {"upperarm_l": (-0.50, 0, 0), "upperarm_r": (0.50, 0, 0), "thigh_l": (0.36, 0, 0), "thigh_r": (-0.36, 0, 0)}), (24, {"upperarm_l": (0.50, 0, 0), "upperarm_r": (-0.50, 0, 0), "thigh_l": (-0.36, 0, 0), "thigh_r": (0.36, 0, 0)})])
    action(armature, "run", [(1, {"upperarm_l": (0.70, 0, 0), "upperarm_r": (-0.70, 0, 0), "thigh_l": (-0.52, 0, 0), "thigh_r": (0.52, 0, 0)}), (10, {"upperarm_l": (-0.70, 0, 0), "upperarm_r": (0.70, 0, 0), "thigh_l": (0.52, 0, 0), "thigh_r": (-0.52, 0, 0)}), (20, {"upperarm_l": (0.70, 0, 0), "upperarm_r": (-0.70, 0, 0), "thigh_l": (-0.52, 0, 0), "thigh_r": (0.52, 0, 0)})])
    action(armature, "climb_pose", [(1, {"upperarm_l": (-0.65, 0, 0), "upperarm_r": (-0.65, 0, 0), "thigh_l": (0.50, 0, 0), "thigh_r": (0.50, 0, 0)}), (18, {"upperarm_l": (0.55, 0, 0), "upperarm_r": (0.55, 0, 0), "thigh_l": (-0.30, 0, 0), "thigh_r": (-0.30, 0, 0)}), (36, {"upperarm_l": (-0.65, 0, 0), "upperarm_r": (-0.65, 0, 0), "thigh_l": (0.50, 0, 0), "thigh_r": (0.50, 0, 0)})])
    action(armature, "eat", [(1, {"neck_01": (0.12, 0, 0), "head": (0.14, 0, 0)}), (16, {"neck_01": (0.48, 0, 0), "head": (0.32, 0, 0)}), (32, {"neck_01": (0.12, 0, 0), "head": (0.14, 0, 0)})])
    action(armature, "hurt", [(1, {"spine_02": (-0.12, 0.08, 0)}), (12, {"spine_02": (0.14, -0.08, 0)}), (24, {"spine_02": (-0.12, 0.08, 0)})])
    action(armature, "turn", [(1, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)}), (12, {"spine_02": (0, 0.16, 0), "neck_01": (0, 0.20, 0)}), (24, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)})])
    for name in ("idle", "walk", "knuckle_walk", "run"):
        if bpy.data.actions.get(name):
            bpy.data.actions[name].use_cyclic = True


def setup_preview() -> None:
    bpy.ops.object.camera_add(location=(2.8, -3.8, 2.2))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, -0.10, 1.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 3.5))
    bpy.context.object.data.energy = 900
    bpy.context.object.data.size = 4
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.5, 1.8))
    bpy.context.object.data.energy = 400
    bpy.context.object.data.size = 3
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.render.resolution_x = 640
    bpy.context.scene.render.resolution_y = 640
    bpy.context.scene.render.resolution_percentage = 100
    bpy.context.scene.render.film_transparent = True


def export() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    RUNTIME_ROOT.mkdir(parents=True, exist_ok=True)
    blend = ROOT / f"{MODEL_NAME}.blend"
    glb = ROOT / f"{MODEL_NAME}.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", export_apply=True, export_animations=True, export_animation_mode="ACTIONS", export_extra_animations=True, export_nla_strips=True, export_skins=True, export_morph=False)
    shutil.copy2(glb, RUNTIME_ROOT / glb.name)
    metadata = {
        "asset_id": MODEL_NAME,
        "display_name": "Ape / Primate Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 1.35,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "primate",
        "semantic_anatomy": ["head", "jaw", "torso", "clavicle_l", "clavicle_r", "hand_l", "hand_r", "knuckle_l", "knuckle_r", "foot_l", "foot_r", "tail"],
        "animations": ["idle", "walk", "knuckle_walk", "run", "climb_pose", "eat", "hurt", "turn"],
        "interaction_anchors": ["hand_l", "hand_r", "knuckle_l", "knuckle_r"],
        "runtime_path": f"game/assets/creatures/ape/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural primate body if the GLB is unavailable",
        "notes": "Broad-shouldered ape silhouette with long forelimbs, knuckle contacts, hands, feet, brow, muzzle, ears, and short tail.",
    }
    text = json.dumps(metadata, indent=2)
    (ROOT / f"{MODEL_NAME}.json").write_text(text, encoding="utf-8")
    (RUNTIME_ROOT / f"{MODEL_NAME}.json").write_text(text, encoding="utf-8")
    bpy.context.scene.render.filepath = str(ROOT / f"{MODEL_NAME}_preview.png")
    bpy.ops.render.render(write_still=True)


def main() -> None:
    clear_scene()
    parts = create_mesh()
    armature = create_armature()
    attach_to_bones(parts, armature)
    create_actions(armature)
    setup_preview()
    export()


if __name__ == "__main__":
    main()

