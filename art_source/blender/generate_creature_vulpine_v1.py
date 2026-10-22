"""Generate the first project-owned vulpine/fox asset.

The fox keeps the shared quadruped semantics but uses a lighter frame, pointed
muzzle, large ears, and a long bushy tail so it does not collapse into the wolf
silhouette.
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
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "fox"
MODEL_NAME = "fox_vulpine_rig_v1"

_canid_path = Path(__file__).with_name("generate_creature_canid_v1.py")
_spec = importlib.util.spec_from_file_location("canid_helpers", _canid_path)
_canid = importlib.util.module_from_spec(_spec)
assert _spec and _spec.loader
_spec.loader.exec_module(_canid)
uv_sphere = _canid.uv_sphere
capsule_between = _canid.capsule_between
cone = _canid.cone
material = _canid.material


RUST = material("Fox_Rust", (0.62, 0.20, 0.075))
CREAM = material("Fox_Cream", (0.88, 0.72, 0.53))
TIP = material("Fox_TailTip", (0.78, 0.75, 0.68))
NOSE = material("Fox_Nose", (0.075, 0.035, 0.025))
EYE = material("Fox_Eye", (0.07, 0.025, 0.008), 0.25)
PAW = material("Fox_Paw", (0.12, 0.065, 0.04))


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # Light narrow torso and raised rump distinguish a fox from a wolf.
    parts += [uv_sphere("Body", (0, 0.08, 0.76), (0.34, 0.65, 0.32), RUST)]
    parts += [uv_sphere("Chest", (0, -0.42, 0.79), (0.32, 0.30, 0.31), CREAM)]
    parts += [uv_sphere("Rump", (0, 0.47, 0.80), (0.35, 0.29, 0.33), RUST)]
    parts += [capsule_between("Neck", Vector((0, -0.42, 0.80)), Vector((0, -0.64, 0.98)), 0.15, RUST)]
    parts += [uv_sphere("Head", (0, -0.73, 1.06), (0.22, 0.24, 0.23), RUST)]
    parts += [uv_sphere("Muzzle", (0, -0.98, 1.00), (0.15, 0.25, 0.13), CREAM)]
    parts += [uv_sphere("Nose", (0, -1.18, 1.02), (0.10, 0.07, 0.07), NOSE)]
    parts += [uv_sphere("Jaw", (0, -0.96, 0.93), (0.14, 0.16, 0.09), CREAM)]
    for side in (-1, 1):
        suffix = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Eye_{suffix}", (0.15 * side, -0.88, 1.12), (0.035, 0.028, 0.035), EYE, 12, 8)]
        parts += [cone(f"Ear_{suffix}", (0.15 * side, -0.72, 1.35), 0.13, 0.36, RUST, (0, 0.12 * side, 0))]
        parts += [cone(f"InnerEar_{suffix}", (0.15 * side, -0.72, 1.31), 0.07, 0.25, CREAM, (0, 0.12 * side, 0))]

    limbs = {
        "Front_L": (Vector((-0.22, -0.30, 0.66)), Vector((-0.23, -0.33, 0.35)), Vector((-0.23, -0.33, 0.10))),
        "Front_R": (Vector((0.22, -0.30, 0.66)), Vector((0.23, -0.33, 0.35)), Vector((0.23, -0.33, 0.10))),
        "Rear_L": (Vector((-0.23, 0.39, 0.70)), Vector((-0.24, 0.42, 0.36)), Vector((-0.24, 0.38, 0.10))),
        "Rear_R": (Vector((0.23, 0.39, 0.70)), Vector((0.24, 0.42, 0.36)), Vector((0.24, 0.38, 0.10))),
    }
    for name, (hip, knee, paw) in limbs.items():
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.075, RUST)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.085, 0.085, 0.085), RUST, 12, 8)]
        parts += [capsule_between(f"{name}_Lower", knee, paw, 0.06, RUST)]
        parts += [uv_sphere(f"{name}_Paw", (paw.x, paw.y - 0.04, paw.z), (0.085, 0.12, 0.06), PAW, 12, 8)]

    tail_points = [Vector((0, 0.70, 0.87)), Vector((0, 0.92, 0.91)), Vector((0, 1.13, 0.83)), Vector((0, 1.30, 0.70)), Vector((0, 1.40, 0.55))]
    for index in range(len(tail_points) - 1):
        material_for_segment = RUST if index < 3 else TIP
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.13 - index * 0.018, material_for_segment)]
    parts += [uv_sphere("TailTip", (0, 1.42, 0.51), (0.12, 0.15, 0.14), TIP, 14, 9)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Fox_Vulpine_Rig_v1"
    armature.data.name = "Fox_Vulpine_Rig_v1"
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
    bone("pelvis", (0, 0.48, 0.72), (0, 0.20, 0.77), "root")
    bone("spine_01", (0, 0.20, 0.77), (0, -0.05, 0.80), "pelvis")
    bone("spine_02", (0, -0.05, 0.80), (0, -0.27, 0.82), "spine_01")
    bone("spine_03", (0, -0.27, 0.82), (0, -0.45, 0.85), "spine_02")
    bone("neck_01", (0, -0.45, 0.85), (0, -0.65, 1.00), "spine_03")
    bone("head", (0, -0.65, 1.00), (0, -0.86, 1.06), "neck_01")
    bone("jaw", (0, -0.86, 0.98), (0, -1.08, 0.94), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"ear_{side}", (0.15 * x, -0.72, 1.30), (0.15 * x, -0.72, 1.58), "head")
        for prefix, hip, knee, paw, parent in (
            (f"front_{side}", (0.22 * x, -0.30, 0.66), (0.23 * x, -0.33, 0.35), (0.23 * x, -0.33, 0.10), "spine_03"),
            (f"rear_{side}", (0.23 * x, 0.39, 0.70), (0.24 * x, 0.42, 0.36), (0.24 * x, 0.38, 0.10), "pelvis"),
        ):
            bone(f"{prefix}_upper", hip, knee, parent)
            bone(f"{prefix}_lower", knee, paw, f"{prefix}_upper")
            bone(f"{prefix}_foot", paw, (paw[0], paw[1] - 0.07, 0.06), f"{prefix}_lower")
    previous = "pelvis"
    for index, point in enumerate([(0, 0.92, 0.91), (0, 1.13, 0.83), (0, 1.30, 0.70), (0, 1.40, 0.55)], 1):
        bone(f"tail_{index:02d}", point, (point[0], point[1] + 0.10, point[2] - 0.08), previous)
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
        modifier = part.modifiers.new("VulpineArmature", "ARMATURE")
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
    action(armature, "idle", [(1, {}), (24, {"tail_02": (0, 0.08, 0)}), (48, {})])
    action(armature, "walk", [(1, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)}), (16, {"front_l_upper": (-0.20, 0, 0), "rear_l_upper": (0.16, 0, 0), "front_r_upper": (0.20, 0, 0), "rear_r_upper": (-0.16, 0, 0)}), (32, {"front_l_upper": (0.20, 0, 0), "rear_l_upper": (-0.16, 0, 0), "front_r_upper": (-0.20, 0, 0), "rear_r_upper": (0.16, 0, 0)})])
    action(armature, "trot", [(1, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)}), (13, {"front_l_upper": (-0.38, 0, 0), "rear_l_upper": (-0.38, 0, 0), "front_r_upper": (0.38, 0, 0), "rear_r_upper": (0.38, 0, 0)}), (24, {"front_l_upper": (0.38, 0, 0), "rear_l_upper": (0.38, 0, 0), "front_r_upper": (-0.38, 0, 0), "rear_r_upper": (-0.38, 0, 0)})])
    action(armature, "run", [(1, {"front_l_upper": (0.55, 0, 0), "rear_l_upper": (-0.55, 0, 0), "front_r_upper": (-0.55, 0, 0), "rear_r_upper": (0.55, 0, 0)}), (10, {"front_l_upper": (-0.55, 0, 0), "rear_l_upper": (0.55, 0, 0), "front_r_upper": (0.55, 0, 0), "rear_r_upper": (-0.55, 0, 0)}), (20, {"front_l_upper": (0.55, 0, 0), "rear_l_upper": (-0.55, 0, 0), "front_r_upper": (-0.55, 0, 0), "rear_r_upper": (0.55, 0, 0)})])
    action(armature, "sniff", [(1, {"neck_01": (0.10, 0, 0), "head": (0.14, 0, 0)}), (16, {"neck_01": (0.36, 0, 0), "head": (0.28, 0, 0)}), (32, {"neck_01": (0.10, 0, 0), "head": (0.14, 0, 0)})])
    action(armature, "eat", [(1, {"neck_01": (0.16, 0, 0), "head": (0.16, 0, 0)}), (16, {"neck_01": (0.46, 0, 0), "head": (0.30, 0, 0)}), (32, {"neck_01": (0.16, 0, 0), "head": (0.16, 0, 0)})])
    action(armature, "hurt", [(1, {"spine_02": (-0.10, 0.08, 0)}), (12, {"spine_02": (0.12, -0.08, 0)}), (24, {"spine_02": (-0.10, 0.08, 0)})])
    action(armature, "turn", [(1, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)}), (12, {"spine_02": (0, 0.16, 0), "neck_01": (0, 0.20, 0)}), (24, {"spine_02": (0, -0.16, 0), "neck_01": (0, -0.20, 0)})])
    for name in ("idle", "walk", "trot", "run"):
        if bpy.data.actions.get(name):
            bpy.data.actions[name].use_cyclic = True


def setup_preview() -> None:
    bpy.ops.object.camera_add(location=(2.5, -3.8, 1.8))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, -0.08, 0.75)) - camera.location).to_track_quat("-Z", "Y").to_euler()
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
        "display_name": "Fox / Vulpine Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 0.62,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "vulpine",
        "semantic_anatomy": ["head", "jaw", "torso", "front_leg_l", "front_leg_r", "rear_leg_l", "rear_leg_r", "tail", "paw"],
        "animations": ["idle", "walk", "trot", "run", "sniff", "eat", "hurt", "turn"],
        "interaction_anchors": ["head", "jaw", "paw_l", "paw_r", "tail_04"],
        "runtime_path": f"game/assets/creatures/fox/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural canid body if the GLB is unavailable",
        "notes": "Light fox silhouette with pointed muzzle, large ears, compact paws, and a long bushy tail; intentionally distinct from the wolf asset.",
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

