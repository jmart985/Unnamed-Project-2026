"""Generate the first project-owned feline/cat creature asset."""

from __future__ import annotations

import importlib.util
import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1] / "reference" / "generated_creatures"
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "cat"
MODEL_NAME = "cat_felid_rig_v1"

_canid_path = Path(__file__).with_name("generate_creature_canid_v1.py")
_spec = importlib.util.spec_from_file_location("canid_helpers", _canid_path)
_canid = importlib.util.module_from_spec(_spec)
assert _spec and _spec.loader
_spec.loader.exec_module(_canid)
uv_sphere = _canid.uv_sphere
capsule_between = _canid.capsule_between
cone = _canid.cone
material = _canid.material


FUR = material("Cat_Fur", (0.42, 0.28, 0.16))
FUR_LIGHT = material("Cat_Chest", (0.72, 0.58, 0.40))
FACE = material("Cat_Face", (0.55, 0.34, 0.20))
PAW = material("Cat_Paw", (0.12, 0.07, 0.045))
NOSE = material("Cat_Nose", (0.30, 0.12, 0.11))
EYE = material("Cat_Eye", (0.64, 0.55, 0.10), 0.25)
EAR = material("Cat_InnerEar", (0.72, 0.32, 0.28))


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # Cats are compact and flexible rather than barrel-chested or long-legged.
    parts += [uv_sphere("Body", (0, 0.06, 0.62), (0.29, 0.56, 0.29), FUR)]
    parts += [uv_sphere("Chest", (0, -0.34, 0.66), (0.28, 0.28, 0.29), FUR_LIGHT)]
    parts += [uv_sphere("Hips", (0, 0.39, 0.64), (0.30, 0.26, 0.28), FUR)]
    parts += [capsule_between("Neck", Vector((0, -0.35, 0.67)), Vector((0, -0.53, 0.82)), 0.14, FUR)]
    parts += [uv_sphere("Head", (0, -0.63, 0.90), (0.21, 0.22, 0.22), FUR)]
    parts += [uv_sphere("Muzzle", (0, -0.84, 0.85), (0.13, 0.18, 0.11), FACE)]
    parts += [uv_sphere("Nose", (0, -0.99, 0.86), (0.07, 0.05, 0.05), NOSE)]
    parts += [uv_sphere("Jaw", (0, -0.82, 0.78), (0.13, 0.14, 0.08), FACE)]
    for side in (-1, 1):
        suffix = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Eye_{suffix}", (0.14 * side, -0.78, 0.97), (0.035, 0.025, 0.035), EYE, 12, 8)]
        parts += [cone(f"Ear_{suffix}", (0.14 * side, -0.62, 1.16), 0.11, 0.30, FUR, (0, 0.10 * side, 0))]
        parts += [cone(f"InnerEar_{suffix}", (0.14 * side, -0.63, 1.13), 0.06, 0.20, EAR, (0, 0.10 * side, 0))]

    limbs = {
        "Front_L": (Vector((-0.19, -0.25, 0.56)), Vector((-0.19, -0.27, 0.30)), Vector((-0.19, -0.29, 0.09))),
        "Front_R": (Vector((0.19, -0.25, 0.56)), Vector((0.19, -0.27, 0.30)), Vector((0.19, -0.29, 0.09))),
        "Rear_L": (Vector((-0.20, 0.32, 0.57)), Vector((-0.21, 0.37, 0.28)), Vector((-0.21, 0.31, 0.09))),
        "Rear_R": (Vector((0.20, 0.32, 0.57)), Vector((0.21, 0.37, 0.28)), Vector((0.21, 0.31, 0.09))),
    }
    for name, (hip, knee, paw) in limbs.items():
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.06, FUR)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.07, 0.07, 0.07), FUR, 12, 8)]
        parts += [capsule_between(f"{name}_Lower", knee, paw, 0.05, FUR)]
        parts += [uv_sphere(f"{name}_Paw", (paw.x, paw.y - 0.035, paw.z), (0.08, 0.11, 0.055), PAW, 12, 8)]

    tail_points = [Vector((0, 0.57, 0.70)), Vector((0, 0.78, 0.78)), Vector((0, 0.98, 0.88)), Vector((0, 1.16, 0.94)), Vector((0, 1.30, 1.00))]
    for index in range(len(tail_points) - 1):
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.085 - index * 0.014, FUR)]
    parts += [uv_sphere("TailTip", (0, 1.34, 1.02), (0.09, 0.12, 0.11), FUR_LIGHT, 12, 8)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Cat_Felid_Rig_v1"
    armature.data.name = "Cat_Felid_Rig_v1"
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

    bone("root", (0, 0, 0), (0, 0, 0.1))
    bone("pelvis", (0, 0.42, 0.59), (0, 0.18, 0.64), "root")
    bone("spine_01", (0, 0.18, 0.64), (0, -0.04, 0.68), "pelvis")
    bone("spine_02", (0, -0.04, 0.68), (0, -0.22, 0.70), "spine_01")
    bone("spine_03", (0, -0.22, 0.70), (0, -0.37, 0.73), "spine_02")
    bone("neck_01", (0, -0.37, 0.73), (0, -0.54, 0.84), "spine_03")
    bone("head", (0, -0.54, 0.84), (0, -0.72, 0.90), "neck_01")
    bone("jaw", (0, -0.72, 0.83), (0, -0.90, 0.78), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"ear_{side}", (0.14 * x, -0.62, 1.10), (0.14 * x, -0.62, 1.32), "head")
        for prefix, hip, knee, paw, parent in (
            (f"front_{side}", (0.19 * x, -0.25, 0.56), (0.19 * x, -0.27, 0.30), (0.19 * x, -0.29, 0.09), "spine_03"),
            (f"rear_{side}", (0.20 * x, 0.32, 0.57), (0.21 * x, 0.37, 0.28), (0.21 * x, 0.31, 0.09), "pelvis"),
        ):
            bone(f"{prefix}_upper", hip, knee, parent)
            bone(f"{prefix}_lower", knee, paw, f"{prefix}_upper")
            bone(f"{prefix}_foot", paw, (paw[0], paw[1] - 0.07, 0.05), f"{prefix}_lower")
    previous = "pelvis"
    for index, point in enumerate([(0, 0.78, 0.78), (0, 0.98, 0.88), (0, 1.16, 0.94), (0, 1.30, 1.00)], 1):
        bone(f"tail_{index:02d}", point, (point[0], point[1] + 0.11, point[2] + 0.02), previous)
        previous = f"tail_{index:02d}"
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def attach_to_bones(parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    for part in parts:
        name = part.name
        bone_name = "spine_02"
        if any(name.startswith(prefix) for prefix in ("Head", "Muzzle", "Nose", "Jaw", "Eye", "Ear", "InnerEar")):
            bone_name = "head"
        elif name.startswith("Neck"):
            bone_name = "neck_01"
        elif name.startswith("Tail"):
            bone_name = "tail_04" if "Tip" in name else f"tail_{min(int(name.split("_")[1]), 4):02d}"
        elif name.startswith("Front_") or name.startswith("Rear_"):
            family, side = name.split("_")[0:2]
            prefix = f"{family.lower()}_{side.lower()}"
            bone_name = f"{prefix}_upper" if "Upper" in name else f"{prefix}_lower" if "Lower" in name or "Joint" in name else f"{prefix}_foot"
        part.parent = armature
        group = part.vertex_groups.new(name=bone_name)
        group.add(list(range(len(part.data.vertices))), 1.0, "REPLACE")
        modifier = part.modifiers.new("FelidArmature", "ARMATURE")
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
    action(armature, "idle", [(1, {}), (24, {"tail_02": (0, 0.10, 0)}), (48, {})])
    action(armature, "walk", [(1, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)}), (16, {"front_l_upper": (-0.20, 0, 0), "rear_l_upper": (0.16, 0, 0), "front_r_upper": (0.20, 0, 0), "rear_r_upper": (-0.16, 0, 0)}), (32, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)})])
    action(armature, "trot", [(1, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)}), (13, {"front_l_upper": (-0.38, 0, 0), "rear_l_upper": (-0.38, 0, 0), "front_r_upper": (0.38, 0, 0), "rear_r_upper": (0.38, 0, 0)}), (24, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)})])
    action(armature, "run", [(1, {"front_l_upper": (0.56, 0, 0), "rear_r_upper": (0.56, 0, 0), "front_r_upper": (-0.56, 0, 0), "rear_l_upper": (-0.56, 0, 0)}), (10, {"front_l_upper": (-0.56, 0, 0), "rear_r_upper": (-0.56, 0, 0), "front_r_upper": (0.56, 0, 0), "rear_l_upper": (0.56, 0, 0)}), (20, {"front_l_upper": (0.56, 0, 0), "rear_r_upper": (0.56, 0, 0), "front_r_upper": (-0.56, 0, 0), "rear_l_upper": (-0.56, 0, 0)})])
    action(armature, "stalk", [(1, {"spine_02": (0.08, 0, 0), "head": (0.12, 0, 0)}), (18, {"spine_02": (-0.08, 0, 0), "head": (-0.12, 0, 0)}), (36, {"spine_02": (0.08, 0, 0), "head": (0.12, 0, 0)})])
    action(armature, "pounce", [(1, {"front_l_upper": (-0.55, 0, 0), "front_r_upper": (-0.55, 0, 0), "rear_l_upper": (0.45, 0, 0), "rear_r_upper": (0.45, 0, 0)}), (10, {"front_l_upper": (0.60, 0, 0), "front_r_upper": (0.60, 0, 0), "rear_l_upper": (-0.50, 0, 0), "rear_r_upper": (-0.50, 0, 0)}), (20, {})])
    action(armature, "eat", [(1, {"neck_01": (0.16, 0, 0), "head": (0.16, 0, 0)}), (16, {"neck_01": (0.46, 0, 0), "head": (0.30, 0, 0)}), (32, {"neck_01": (0.16, 0, 0), "head": (0.16, 0, 0)})])
    action(armature, "hurt", [(1, {"spine_02": (-0.10, 0.08, 0)}), (12, {"spine_02": (0.12, -0.08, 0)}), (24, {"spine_02": (-0.10, 0.08, 0)})])
    action(armature, "turn", [(1, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)}), (12, {"spine_02": (0, 0.16, 0), "neck_01": (0, 0.20, 0)}), (24, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)})])
    for name in ("idle", "walk", "trot", "run"):
        if bpy.data.actions.get(name):
            bpy.data.actions[name].use_cyclic = True


def setup_preview() -> None:
    bpy.ops.object.camera_add(location=(2.6, -3.7, 1.7))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, -0.10, 0.65)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 3.0))
    bpy.context.object.data.energy = 800
    bpy.context.object.data.size = 4
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.5, 1.6))
    bpy.context.object.data.energy = 350
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
        "display_name": "Cat / Felid Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 0.55,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "felid",
        "semantic_anatomy": ["head", "jaw", "torso", "front_leg_l", "front_leg_r", "rear_leg_l", "rear_leg_r", "tail", "paw"],
        "animations": ["idle", "walk", "trot", "run", "stalk", "pounce", "eat", "hurt", "turn"],
        "interaction_anchors": ["head", "jaw", "paw_l", "paw_r", "tail_04"],
        "runtime_path": f"game/assets/creatures/cat/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural felid body if the GLB is unavailable",
        "notes": "Compact feline silhouette with flexible torso, short muzzle, triangular ears, articulated paws, and long balancing tail.",
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

