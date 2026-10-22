"""Generate the first project-owned suiform pig/boar creature asset."""

from __future__ import annotations

import importlib.util
import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1] / "reference" / "generated_creatures"
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "pig"
MODEL_NAME = "pig_suiform_rig_v1"

_canid_path = Path(__file__).with_name("generate_creature_canid_v1.py")
_spec = importlib.util.spec_from_file_location("canid_helpers", _canid_path)
_canid = importlib.util.module_from_spec(_spec)
assert _spec and _spec.loader
_spec.loader.exec_module(_canid)
uv_sphere = _canid.uv_sphere
capsule_between = _canid.capsule_between
cone = _canid.cone
material = _canid.material

BODY = material("Pig_Flesh", (0.58, 0.27, 0.24))
LIGHT = material("Pig_Belly", (0.82, 0.49, 0.42))
DARK = material("Pig_Shadow", (0.25, 0.09, 0.08))
NOSE = material("Pig_Nose", (0.48, 0.19, 0.20))
HOOF = material("Pig_Hoof", (0.13, 0.07, 0.06))
EAR = material("Pig_InnerEar", (0.72, 0.28, 0.30))
EYE = material("Pig_Eye", (0.06, 0.025, 0.018), 0.35)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # A low, heavy torso, round belly, and short limbs make this unmistakably
    # suiform rather than a recolored cow or long-legged canid.
    parts += [uv_sphere("Body", (0, 0.03, 0.68), (0.43, 0.62, 0.39), BODY)]
    parts += [uv_sphere("Chest", (0, -0.37, 0.70), (0.40, 0.34, 0.36), BODY)]
    parts += [uv_sphere("Belly", (0, 0.02, 0.49), (0.39, 0.47, 0.22), LIGHT)]
    parts += [uv_sphere("Rump", (0, 0.42, 0.70), (0.42, 0.32, 0.35), BODY)]
    parts += [capsule_between("Neck", Vector((0, -0.37, 0.74)), Vector((0, -0.52, 0.82)), 0.20, BODY)]
    parts += [uv_sphere("Head", (0, -0.65, 0.82), (0.29, 0.30, 0.27), BODY)]
    parts += [uv_sphere("Snout", (0, -0.91, 0.73), (0.22, 0.16, 0.15), NOSE)]
    parts += [uv_sphere("NoseDisc", (0, -1.04, 0.74), (0.17, 0.065, 0.11), NOSE)]
    for side in (-1, 1):
        suffix = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Nostril_{suffix}", (0.07 * side, -1.095, 0.77), (0.035, 0.018, 0.026), DARK, 12, 8)]
        parts += [uv_sphere(f"Eye_{suffix}", (0.20 * side, -0.82, 0.93), (0.045, 0.032, 0.045), EYE, 14, 9)]
        parts += [cone(f"Ear_{suffix}", (0.20 * side, -0.60, 1.08), 0.14, 0.27, BODY, (0, 0.18 * side, 0.12 * side))]
        parts += [cone(f"InnerEar_{suffix}", (0.20 * side, -0.61, 1.075), 0.075, 0.16, EAR, (0, 0.18 * side, 0.12 * side))]
    parts += [uv_sphere("Jaw", (0, -0.82, 0.63), (0.21, 0.18, 0.10), DARK)]

    limbs = {
        "Front_L": (Vector((-0.29, -0.34, 0.60)), Vector((-0.30, -0.36, 0.34)), Vector((-0.30, -0.37, 0.12))),
        "Front_R": (Vector((0.29, -0.34, 0.60)), Vector((0.30, -0.36, 0.34)), Vector((0.30, -0.37, 0.12))),
        "Rear_L": (Vector((-0.30, 0.35, 0.61)), Vector((-0.32, 0.39, 0.34)), Vector((-0.31, 0.35, 0.12))),
        "Rear_R": (Vector((0.30, 0.35, 0.61)), Vector((0.32, 0.39, 0.34)), Vector((0.31, 0.35, 0.12))),
    }
    for name, (hip, knee, hoof) in limbs.items():
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.095, BODY)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.105, 0.105, 0.105), BODY, 14, 9)]
        parts += [capsule_between(f"{name}_Lower", knee, hoof, 0.072, BODY)]
        for split in (-1, 1):
            parts += [uv_sphere(f"{name}_Hoof_{split}", (hoof.x + split * 0.052, hoof.y - 0.035, hoof.z), (0.065, 0.095, 0.06), HOOF, 12, 8)]

    # A short raised curl differentiates the suiform tail from a cow's tuft or
    # a canid's straight counterbalance.
    tail_points = [Vector((0, 0.58, 0.78)), Vector((0, 0.77, 0.80)), Vector((0, 0.88, 0.93)), Vector((0, 0.77, 1.04)), Vector((0, 0.62, 0.99))]
    for index in range(len(tail_points) - 1):
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.065 - index * 0.010, BODY)]
    parts += [uv_sphere("TailTip", (0, 0.58, 0.95), (0.075, 0.09, 0.075), BODY, 12, 8)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Pig_Suiform_Rig_v1"
    armature.data.name = "Pig_Suiform_Rig_v1"
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
    bone("pelvis", (0, 0.42, 0.65), (0, 0.20, 0.70), "root")
    bone("spine_01", (0, 0.20, 0.70), (0, -0.02, 0.73), "pelvis")
    bone("spine_02", (0, -0.02, 0.73), (0, -0.22, 0.75), "spine_01")
    bone("spine_03", (0, -0.22, 0.75), (0, -0.40, 0.77), "spine_02")
    bone("neck_01", (0, -0.40, 0.77), (0, -0.54, 0.83), "spine_03")
    bone("head", (0, -0.54, 0.83), (0, -0.84, 0.79), "neck_01")
    bone("jaw", (0, -0.84, 0.67), (0, -1.00, 0.68), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"ear_{side}", (0.20 * x, -0.60, 1.06), (0.29 * x, -0.60, 1.20), "head")
        for prefix, hip, knee, hoof, parent in (
            (f"front_{side}", (0.29 * x, -0.34, 0.60), (0.30 * x, -0.36, 0.34), (0.30 * x, -0.37, 0.12), "spine_03"),
            (f"rear_{side}", (0.30 * x, 0.35, 0.61), (0.32 * x, 0.39, 0.34), (0.31 * x, 0.35, 0.12), "pelvis"),
        ):
            bone(f"{prefix}_upper", hip, knee, parent)
            bone(f"{prefix}_lower", knee, hoof, f"{prefix}_upper")
            bone(f"{prefix}_foot", hoof, (hoof[0], hoof[1] - 0.04, 0.07), f"{prefix}_lower")
            bone(f"{prefix}_hoof_l", (hoof[0] - 0.05, hoof[1] - 0.04, 0.07), (hoof[0] - 0.05, hoof[1] - 0.13, 0.07), f"{prefix}_foot")
            bone(f"{prefix}_hoof_r", (hoof[0] + 0.05, hoof[1] - 0.04, 0.07), (hoof[0] + 0.05, hoof[1] - 0.13, 0.07), f"{prefix}_foot")
    previous = "pelvis"
    for index, point in enumerate([(0, 0.77, 0.80), (0, 0.88, 0.93), (0, 0.77, 1.04), (0, 0.62, 0.99)], 1):
        parent = previous
        bone(f"tail_{index:02d}", point, (point[0], point[1] - 0.03, point[2] - 0.02), parent)
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
        if name.startswith(("Head", "Snout", "Nose", "Nostril", "Eye", "Ear", "InnerEar", "Jaw")):
            bone_name = "head" if not name.startswith("Jaw") else "jaw"
        elif name.startswith("Neck"):
            bone_name = "neck_01"
        elif name.startswith("Tail"):
            if name == "TailTip":
                bone_name = "tail_04"
            else:
                bone_name = f"tail_{min(int(name.split('_')[1]), 4):02d}"
        elif name.startswith(("Front_", "Rear_")):
            family, side = name.split("_")[0:2]
            prefix = f"{family.lower()}_{side.lower()}"
            bone_name = f"{prefix}_upper" if "Upper" in name else f"{prefix}_lower" if "Lower" in name or "Joint" in name else f"{prefix}_foot"
        part.parent = armature
        group = part.vertex_groups.new(name=bone_name)
        group.add(list(range(len(part.data.vertices))), 1.0, "REPLACE")
        modifier = part.modifiers.new("SuiformArmature", "ARMATURE")
        modifier.object = armature


def keyframe_action(armature: bpy.types.Object, name: str, poses: list[tuple[int, dict[str, tuple[float, float, float]]]]) -> None:
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
    keyframe_action(armature, "idle", [(1, {}), (24, {"tail_02": (0, 0.14, 0)}), (48, {})])
    keyframe_action(armature, "walk", [(1, {"front_l_upper": (0.18, 0, 0), "rear_l_upper": (-0.15, 0, 0), "front_r_upper": (-0.18, 0, 0), "rear_r_upper": (0.15, 0, 0)}), (16, {"front_l_upper": (-0.18, 0, 0), "rear_l_upper": (0.15, 0, 0), "front_r_upper": (0.18, 0, 0), "rear_r_upper": (-0.15, 0, 0)}), (32, {"front_l_upper": (0.18, 0, 0), "rear_l_upper": (-0.15, 0, 0), "front_r_upper": (-0.18, 0, 0), "rear_r_upper": (0.15, 0, 0)})])
    keyframe_action(armature, "trot", [(1, {"front_l_upper": (0.34, 0, 0), "rear_l_upper": (0.34, 0, 0), "front_r_upper": (-0.34, 0, 0), "rear_r_upper": (-0.34, 0, 0)}), (13, {"front_l_upper": (-0.34, 0, 0), "rear_l_upper": (-0.34, 0, 0), "front_r_upper": (0.34, 0, 0), "rear_r_upper": (0.34, 0, 0)}), (24, {"front_l_upper": (0.34, 0, 0), "rear_l_upper": (0.34, 0, 0), "front_r_upper": (-0.34, 0, 0), "rear_r_upper": (-0.34, 0, 0)})])
    keyframe_action(armature, "run", [(1, {"front_l_upper": (0.46, 0, 0), "rear_r_upper": (0.46, 0, 0), "front_r_upper": (-0.46, 0, 0), "rear_l_upper": (-0.46, 0, 0)}), (10, {"front_l_upper": (-0.46, 0, 0), "rear_r_upper": (-0.46, 0, 0), "front_r_upper": (0.46, 0, 0), "rear_l_upper": (0.46, 0, 0)}), (20, {"front_l_upper": (0.46, 0, 0), "rear_r_upper": (0.46, 0, 0), "front_r_upper": (-0.46, 0, 0), "rear_l_upper": (-0.46, 0, 0)})])
    keyframe_action(armature, "graze", [(1, {"neck_01": (0.25, 0, 0), "head": (0.32, 0, 0)}), (20, {"neck_01": (0.58, 0, 0), "head": (0.48, 0, 0)}), (40, {"neck_01": (0.25, 0, 0), "head": (0.32, 0, 0)})])
    keyframe_action(armature, "eat", [(1, {"neck_01": (0.20, 0, 0), "head": (0.20, 0, 0)}), (12, {"neck_01": (0.52, 0, 0), "head": (0.38, 0, 0)}), (24, {"neck_01": (0.20, 0, 0), "head": (0.20, 0, 0)})])
    keyframe_action(armature, "hurt", [(1, {"spine_02": (-0.10, 0.08, 0)}), (12, {"spine_02": (0.12, -0.08, 0)}), (24, {"spine_02": (-0.10, 0.08, 0)})])
    keyframe_action(armature, "turn", [(1, {"spine_02": (0, -0.15, 0), "neck_01": (0, -0.18, 0)}), (12, {"spine_02": (0, 0.15, 0), "neck_01": (0, 0.18, 0)}), (24, {"spine_02": (0, -0.15, 0), "neck_01": (0, -0.18, 0)})])
    for name in ("idle", "walk", "trot", "run"):
        if bpy.data.actions.get(name):
            bpy.data.actions[name].use_cyclic = True


def setup_preview() -> None:
    bpy.ops.object.camera_add(location=(2.7, -3.6, 1.55))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, -0.06, 0.66)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 2.8))
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
        "display_name": "Pig / Suiform Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 0.72,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "suiform",
        "semantic_anatomy": ["head", "jaw", "torso", "front_leg_l", "front_leg_r", "rear_leg_l", "rear_leg_r", "tail", "cloven_hoof"],
        "animations": ["idle", "walk", "trot", "run", "graze", "eat", "hurt", "turn"],
        "interaction_anchors": ["head", "jaw", "front_hoof_l", "front_hoof_r", "tail_04"],
        "runtime_path": f"game/assets/creatures/pig/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural suiform body if the GLB is unavailable",
        "notes": "Compact, heavy pig silhouette with broad snout, triangular ears, cloven hooves, and a raised curled tail.",
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

