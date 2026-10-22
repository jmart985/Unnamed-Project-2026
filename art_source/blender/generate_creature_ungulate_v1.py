"""Generate the first project-owned ruminant: a stylized cow.

The generator reuses the small modeling helpers from the canid generator but
keeps a separate ungulate body plan and armature.  It is deliberately source
owned and editable; no downloaded mesh is admitted to runtime.
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
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "cow"
MODEL_NAME = "cow_ungulate_rig_v1"


# Shared mesh helpers keep the asset generators consistent without forcing a
# canid silhouette or canid bone layout onto the ruminant family.
_canid_path = Path(__file__).with_name("generate_creature_canid_v1.py")
_spec = importlib.util.spec_from_file_location("canid_helpers", _canid_path)
_canid = importlib.util.module_from_spec(_spec)
assert _spec and _spec.loader
_spec.loader.exec_module(_canid)
uv_sphere = _canid.uv_sphere
capsule_between = _canid.capsule_between
cone = _canid.cone
material = _canid.material
smooth = _canid.smooth


BODY = material("Cow_Cream", (0.72, 0.67, 0.56))
CHEST = material("Cow_Chest", (0.88, 0.84, 0.74))
PATCH = material("Cow_Patch", (0.19, 0.11, 0.075))
MUZZLE = material("Cow_Muzzle", (0.30, 0.18, 0.15))
HOOF = material("Cow_Hoof", (0.075, 0.055, 0.045))
HORN = material("Cow_Horn", (0.62, 0.48, 0.25))
EYE = material("Cow_Eye", (0.08, 0.035, 0.015), 0.35)
PINK = material("Cow_InnerEar", (0.55, 0.25, 0.24))
UDDER = material("Cow_Udder", (0.68, 0.37, 0.34))
TEAT = material("Cow_Teat", (0.48, 0.22, 0.20))


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # A broad barrel, deep chest, and raised rump establish a ruminant profile.
    parts += [uv_sphere("Body", (0, 0.02, 0.86), (0.48, 0.83, 0.43), BODY)]
    parts += [uv_sphere("Chest", (0, -0.50, 0.91), (0.45, 0.40, 0.40), CHEST)]
    parts += [uv_sphere("Rump", (0, 0.53, 0.88), (0.45, 0.40, 0.40), BODY)]
    # Three asymmetrical patches give the otherwise simple prototype a readable
    # cow coat pattern without introducing texture dependencies.
    parts += [uv_sphere("PatchShoulder", (-0.36, -0.37, 0.98), (0.08, 0.22, 0.19), PATCH, 14, 9)]
    parts += [uv_sphere("PatchRump", (0.33, 0.50, 0.98), (0.10, 0.25, 0.21), PATCH, 14, 9)]
    parts += [uv_sphere("PatchBack", (-0.24, 0.15, 1.18), (0.12, 0.22, 0.08), PATCH, 14, 9)]

    # A stylized four-quarter udder is part of the production cow silhouette.
    # Each quarter and teat has a stable name so milking interactions can target
    # semantic anchors without depending on mesh indices.
    parts += [uv_sphere("Udder", (0, 0.28, 0.58), (0.28, 0.25, 0.17), UDDER)]
    for side in (-1, 1):
        for row, y in (("Front", 0.17), ("Rear", 0.37)):
            suffix = "L" if side < 0 else "R"
            parts += [uv_sphere(f"Udder_{row}_{suffix}", (0.105 * side, y, 0.57), (0.12, 0.12, 0.11), UDDER, 14, 9)]
            parts += [capsule_between(f"Teat_{row}_{suffix}", Vector((0.105 * side, y, 0.52)), Vector((0.105 * side, y, 0.39)), 0.035, TEAT)]

    parts += [capsule_between("Neck", Vector((0, -0.48, 0.92)), Vector((0, -0.68, 1.20)), 0.22, BODY)]
    parts += [uv_sphere("Head", (0, -0.77, 1.29), (0.27, 0.30, 0.27), BODY)]
    parts += [uv_sphere("Muzzle", (0, -1.05, 1.22), (0.23, 0.25, 0.16), MUZZLE)]
    parts += [uv_sphere("Nose", (0, -1.26, 1.23), (0.16, 0.09, 0.09), MUZZLE)]
    parts += [uv_sphere("Jaw", (0, -1.07, 1.13), (0.20, 0.20, 0.10), MUZZLE)]
    for side in (-1, 1):
        suffix = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Eye_{suffix}", (0.20 * side, -0.96, 1.38), (0.045, 0.035, 0.045), EYE, 14, 9)]
        parts += [cone(f"Horn_{suffix}", (0.16 * side, -0.73, 1.58), 0.075, 0.25, HORN, (0, 0.2 * side, 0.0))]
        parts += [uv_sphere(f"Ear_{suffix}", (0.28 * side, -0.75, 1.40), (0.15, 0.08, 0.07), BODY, 14, 9)]
        parts += [uv_sphere(f"InnerEar_{suffix}", (0.29 * side, -0.80, 1.40), (0.09, 0.035, 0.035), PINK, 12, 8)]

    limbs = {
        "Front_L": (Vector((-0.32, -0.34, 0.80)), Vector((-0.32, -0.35, 0.43)), Vector((-0.32, -0.35, 0.13))),
        "Front_R": (Vector((0.32, -0.34, 0.80)), Vector((0.32, -0.35, 0.43)), Vector((0.32, -0.35, 0.13))),
        "Rear_L": (Vector((-0.32, 0.40, 0.83)), Vector((-0.32, 0.43, 0.43)), Vector((-0.32, 0.39, 0.13))),
        "Rear_R": (Vector((0.32, 0.40, 0.83)), Vector((0.32, 0.43, 0.43)), Vector((0.32, 0.39, 0.13))),
    }
    for name, (hip, knee, hoof) in limbs.items():
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.10, BODY)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.115, 0.115, 0.115), BODY, 14, 9)]
        parts += [capsule_between(f"{name}_Lower", knee, hoof, 0.075, BODY)]
        # Two separated hoof lobes are the minimum visible cloven-hoof cue.
        for split in (-1, 1):
            parts += [uv_sphere(f"{name}_Hoof_{split}", (hoof.x + split * 0.055, hoof.y - 0.035, hoof.z), (0.065, 0.105, 0.065), HOOF, 12, 8)]

    tail_points = [Vector((0, 0.77, 0.91)), Vector((0, 0.98, 0.77)), Vector((0, 1.10, 0.61)), Vector((0, 1.15, 0.45))]
    for index in range(len(tail_points) - 1):
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.075 - index * 0.016, BODY)]
    parts += [uv_sphere("TailTuft", (0, 1.15, 0.37), (0.11, 0.12, 0.16), PATCH, 14, 9)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Cow_Ungulate_Rig_v1"
    armature.data.name = "Cow_Ungulate_Rig_v1"
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
    bone("pelvis", (0, 0.55, 0.82), (0, 0.30, 0.88), "root")
    bone("spine_01", (0, 0.30, 0.88), (0, 0.05, 0.93), "pelvis")
    bone("spine_02", (0, 0.05, 0.93), (0, -0.22, 0.96), "spine_01")
    bone("spine_03", (0, -0.22, 0.96), (0, -0.47, 1.00), "spine_02")
    bone("neck_01", (0, -0.47, 1.00), (0, -0.68, 1.20), "spine_03")
    bone("head", (0, -0.68, 1.20), (0, -0.96, 1.25), "neck_01")
    bone("jaw", (0, -0.96, 1.19), (0, -1.16, 1.15), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"ear_{side}", (0.28 * x, -0.75, 1.39), (0.38 * x, -0.75, 1.41), "head")
        bone(f"horn_{side}", (0.16 * x, -0.73, 1.55), (0.16 * x, -0.73, 1.78), "head")
        for prefix, hip, knee, hoof, parent in (
            (f"front_{side}", (0.32 * x, -0.34, 0.80), (0.32 * x, -0.35, 0.43), (0.32 * x, -0.35, 0.13), "spine_03"),
            (f"rear_{side}", (0.32 * x, 0.40, 0.83), (0.32 * x, 0.43, 0.43), (0.32 * x, 0.39, 0.13), "pelvis"),
        ):
            bone(f"{prefix}_upper", hip, knee, parent)
            bone(f"{prefix}_lower", knee, hoof, f"{prefix}_upper")
            bone(f"{prefix}_foot", hoof, (hoof[0], hoof[1] - 0.04, 0.07), f"{prefix}_lower")
            bone(f"{prefix}_hoof_l", (hoof[0] - 0.05, hoof[1] - 0.04, 0.07), (hoof[0] - 0.05, hoof[1] - 0.13, 0.07), f"{prefix}_foot")
            bone(f"{prefix}_hoof_r", (hoof[0] + 0.05, hoof[1] - 0.04, 0.07), (hoof[0] + 0.05, hoof[1] - 0.13, 0.07), f"{prefix}_foot")
    bone("udder", (0, 0.28, 0.66), (0, 0.28, 0.52), "pelvis")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        for row, y in (("front", 0.17), ("rear", 0.37)):
            bone(f"udder_{row}_{side}", (0.105 * x, y, 0.62), (0.105 * x, y, 0.50), "udder")
            bone(f"teat_{row}_{side}", (0.105 * x, y, 0.50), (0.105 * x, y, 0.37), f"udder_{row}_{side}")
    previous = "pelvis"
    for index, point in enumerate([(0, 0.98, 0.77), (0, 1.10, 0.61), (0, 1.15, 0.45)], 1):
        tail = (0, point[1] + 0.04, point[2] - 0.10)
        bone(f"tail_{index:02d}", point, tail, previous)
        previous = f"tail_{index:02d}"
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def attach_to_bones(parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    for part in parts:
        bone_name = "spine_02"
        if any(part.name.startswith(prefix) for prefix in ("Head", "Muzzle", "Nose", "Jaw", "Eye", "Ear", "Horn", "InnerEar")):
            bone_name = "head"
        elif part.name.startswith("Neck"):
            bone_name = "neck_01"
        elif part.name.startswith("Tail"):
            if part.name.startswith("TailTuft"):
                bone_name = "tail_03"
            else:
                bone_name = f"tail_{min(int(part.name.split("_")[1]), 3):02d}"
        elif part.name == "Udder":
            bone_name = "udder"
        elif part.name.startswith("Udder_") or part.name.startswith("Teat_"):
            tokens = part.name.split("_")
            family, row, side = tokens[0].lower(), tokens[1].lower(), tokens[2].lower()
            bone_name = f"{family}_{row}_{side}"
        elif part.name.startswith("Front_") or part.name.startswith("Rear_"):
            family, side = part.name.split("_")[0:2]
            side = side.lower()
            prefix = f"{family.lower()}_{side}"
            bone_name = f"{prefix}_upper" if "Upper" in part.name else f"{prefix}_lower" if "Lower" in part.name or "Joint" in part.name else f"{prefix}_foot"
        part.parent = armature
        if part.type == "MESH":
            group = part.vertex_groups.new(name=bone_name)
            group.add(list(range(len(part.data.vertices))), 1.0, "REPLACE")
            modifier = part.modifiers.new("UngulateArmature", "ARMATURE")
            modifier.object = armature


def keyframe_action(armature: bpy.types.Object, name: str, poses: list[tuple[int, dict[str, tuple[float, float, float]]]]) -> None:
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, pose in poses:
        bpy.context.scene.frame_set(frame)
        for bone_name, rotation in pose.items():
            if bone_name in armature.pose.bones:
                pb = armature.pose.bones[bone_name]
                pb.rotation_euler = rotation
                pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)


def create_actions(armature: bpy.types.Object) -> None:
    keyframe_action(armature, "idle", [(1, {}), (24, {"tail_02": (0, 0.06, 0)}), (48, {})])
    keyframe_action(armature, "walk", [(1, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)}), (17, {"front_l_upper": (-0.20, 0, 0), "rear_l_upper": (0.16, 0, 0), "front_r_upper": (0.20, 0, 0), "rear_r_upper": (-0.16, 0, 0)}), (32, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)})])
    keyframe_action(armature, "trot", [(1, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)}), (13, {"front_l_upper": (-0.38, 0, 0), "rear_l_upper": (-0.38, 0, 0), "front_r_upper": (0.38, 0, 0), "rear_r_upper": (0.38, 0, 0)}), (24, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)})])
    keyframe_action(armature, "eat", [(1, {"neck_01": (0.18, 0, 0), "head": (0.12, 0, 0)}), (16, {"neck_01": (0.52, 0, 0), "head": (0.30, 0, 0)}), (32, {"neck_01": (0.18, 0, 0), "head": (0.12, 0, 0)})])
    keyframe_action(armature, "hurt", [(1, {"spine_02": (-0.10, 0.08, 0)}), (12, {"spine_02": (0.12, -0.08, 0)}), (24, {"spine_02": (-0.10, 0.08, 0)})])
    keyframe_action(armature, "turn", [(1, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)}), (12, {"spine_02": (0, 0.16, 0), "neck_01": (0, 0.20, 0)}), (24, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)})])
    for name in ("idle", "walk", "trot"):
        action = bpy.data.actions.get(name)
        if action and hasattr(action, "use_cyclic"):
            action.use_cyclic = True


def setup_preview() -> None:
    bpy.ops.object.camera_add(location=(2.8, -3.8, 2.1))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.rotation_euler = (Vector((0, -0.05, 0.85)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 3.5))
    key = bpy.context.object
    key.data.energy = 800
    key.data.size = 4
    key.rotation_euler = (math.radians(25), 0, math.radians(35))
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.5, 1.8))
    fill = bpy.context.object
    fill.data.energy = 400
    fill.data.size = 3
    fill.rotation_euler = (math.radians(75), 0, math.radians(-65))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True


def export() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    RUNTIME_ROOT.mkdir(parents=True, exist_ok=True)
    blend = ROOT / f"{MODEL_NAME}.blend"
    glb = ROOT / f"{MODEL_NAME}.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", export_apply=True,
        export_animations=True, export_animation_mode="ACTIONS", export_extra_animations=True,
        export_nla_strips=True, export_skins=True, export_morph=False)
    shutil.copy2(glb, RUNTIME_ROOT / glb.name)
    metadata = {
        "asset_id": MODEL_NAME,
        "display_name": "Cow / Ungulate Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 1.25,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "ruminant_ungulate",
        "semantic_anatomy": ["head", "jaw", "torso", "front_leg_l", "front_leg_r", "rear_leg_l", "rear_leg_r", "tail", "cloven_hoof", "udder", "udder_front_l", "udder_front_r", "udder_rear_l", "udder_rear_r", "teat_front_l", "teat_front_r", "teat_rear_l", "teat_rear_r"],
        "interaction_anchors": {"milking": ["teat_front_l", "teat_front_r", "teat_rear_l", "teat_rear_r"]},
        "animations": ["idle", "walk", "trot", "eat", "hurt", "turn"],
        "runtime_path": f"game/assets/creatures/cow/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural ungulate body if the GLB is unavailable",
        "notes": "Stylized cow silhouette with ruminant barrel torso, horns, cloven hooves, lowered tail, and a four-quarter udder with semantic milking anchors.",
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

