"""Generate the first project-owned production creature: a stylized wolf.

This source file is intentionally procedural and editable.  It creates a clean
canid silhouette, a semantic quadruped armature, simple locomotion actions, and
the GLB/metadata files consumed by the Godot prototype.  The geometry is a
deliberate first production slice: it is detailed enough to replace the
placeholder, while staying within the project's early crowd budget.
"""

from __future__ import annotations

import json
import math
import os
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1] / "reference" / "generated_creatures"
RUNTIME_ROOT = Path(__file__).resolve().parents[2] / "game" / "assets" / "creatures" / "wolf"
MODEL_NAME = "wolf_canid_rig_v1"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    # Keep the named project materials alive: they are created at module load
    # time and reused by the generator after the scene is cleared.
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def material(name: str, color: tuple[float, float, float], roughness: float = 0.82, metallic: float = 0.0) -> bpy.types.Material:
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
    return mat


WOLF = material("Wolf_Slate", (0.11, 0.14, 0.18))
WOLF_LIGHT = material("Wolf_Chest", (0.28, 0.29, 0.30))
WOLF_DARK = material("Wolf_Mask", (0.035, 0.042, 0.052))
NOSE = material("Wolf_Nose", (0.025, 0.018, 0.02), 0.55)
EYE = material("Wolf_Eye", (0.78, 0.48, 0.08), 0.35)
CLAW = material("Wolf_Claw", (0.06, 0.055, 0.05), 0.6)


def smooth(obj: bpy.types.Object) -> bpy.types.Object:
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    return obj


def apply_scale(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def uv_sphere(name: str, location: tuple[float, float, float], scale: tuple[float, float, float], mat: bpy.types.Material, segments: int = 20, rings: int = 12) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)
    obj.data.materials.append(mat)
    return smooth(obj)


def capsule_between(name: str, start: Vector, end: Vector, radius: float, mat: bpy.types.Material) -> bpy.types.Object:
    direction = end - start
    length = direction.length
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("SoftEdges", "BEVEL")
    bevel.width = radius * 0.65
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return smooth(obj)


def cone(name: str, location: tuple[float, float, float], radius: float, depth: float, mat: bpy.types.Material, rotation=(0.0, 0.0, 0.0)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=radius, radius2=0.015, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("EarEdge", "BEVEL")
    bevel.width = 0.015
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return smooth(obj)


def create_mesh() -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    # Shoulder height is ~0.68 m; the complete model is ~1.08 m to the ears.
    parts += [uv_sphere("Body", (0, 0.02, 0.64), (0.34, 0.68, 0.34), WOLF)]
    parts += [uv_sphere("Chest", (0, -0.39, 0.69), (0.30, 0.36, 0.33), WOLF_LIGHT)]
    parts += [uv_sphere("Hindquarters", (0, 0.45, 0.67), (0.31, 0.34, 0.32), WOLF)]
    parts += [capsule_between("Neck", Vector((0, -0.47, 0.72)), Vector((0, -0.62, 0.88)), 0.18, WOLF)]
    parts += [uv_sphere("Head", (0, -0.72, 0.94), (0.23, 0.27, 0.22), WOLF)]
    parts += [uv_sphere("Muzzle", (0, -0.99, 0.88), (0.13, 0.30, 0.12), WOLF_LIGHT)]
    parts += [uv_sphere("Nose", (0, -1.25, 0.88), (0.085, 0.065, 0.065), NOSE)]
    parts += [uv_sphere("Jaw", (0, -1.04, 0.81), (0.12, 0.18, 0.075), WOLF_DARK)]
    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        parts += [uv_sphere(f"Eye_{s}", (0.10 * side, -0.96, 1.02), (0.040, 0.025, 0.040), EYE, 16, 10)]
        parts += [cone(f"Ear_{s}", (0.145 * side, -0.70, 1.21), 0.12, 0.34, WOLF_DARK, (0, 0, -0.08 * side))]

    # Digitigrade front and rear limbs; the paw and three toes make the stance
    # read as a wolf even before animations are applied.
    limbs = {
        "Front_L": (Vector((-0.22, -0.36, 0.70)), Vector((-0.22, -0.39, 0.40)), Vector((-0.22, -0.42, 0.14))),
        "Front_R": (Vector((0.22, -0.36, 0.70)), Vector((0.22, -0.39, 0.40)), Vector((0.22, -0.42, 0.14))),
        "Rear_L": (Vector((-0.22, 0.40, 0.67)), Vector((-0.22, 0.48, 0.37)), Vector((-0.22, 0.40, 0.14))),
        "Rear_R": (Vector((0.22, 0.40, 0.67)), Vector((0.22, 0.48, 0.37)), Vector((0.22, 0.40, 0.14))),
    }
    for name, (hip, knee, paw) in limbs.items():
        parts += [capsule_between(f"{name}_Upper", hip, knee, 0.075, WOLF)]
        parts += [uv_sphere(f"{name}_Joint", tuple(knee), (0.09, 0.09, 0.09), WOLF_DARK)]
        parts += [capsule_between(f"{name}_Lower", knee, paw, 0.06, WOLF)]
        parts += [uv_sphere(f"{name}_Paw", tuple(paw), (0.105, 0.16, 0.065), WOLF_DARK)]
        for toe in (-1, 0, 1):
            parts += [uv_sphere(f"{name}_Toe_{toe}", (paw.x + toe * 0.045, paw.y - 0.12, paw.z - 0.005), (0.025, 0.055, 0.025), CLAW, 12, 8)]

    # Four-segment expressive tail with a slight upward curve.
    tail_points = [Vector((0, 0.67, 0.73)), Vector((0, 0.87, 0.70)), Vector((0, 1.03, 0.71)), Vector((0, 1.14, 0.77)), Vector((0, 1.18, 0.83))]
    for index in range(len(tail_points) - 1):
        parts += [capsule_between(f"Tail_{index + 1:02d}", tail_points[index], tail_points[index + 1], 0.085 - index * 0.014, WOLF)]
    return parts


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "Wolf_Canid_Rig_v1"
    armature.data.name = "Wolf_Canid_Rig_v1"
    armature.show_in_front = True
    edit = armature.data.edit_bones
    for bone in list(edit):
        edit.remove(bone)

    def bone(name: str, head: tuple[float, float, float], tail: tuple[float, float, float], parent: str | None = None) -> None:
        b = edit.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = edit[parent]
            b.use_connect = False

    bone("root", (0, 0, 0), (0, 0, 0.12))
    bone("pelvis", (0, 0.55, 0.63), (0, 0.30, 0.70), "root")
    bone("spine_01", (0, 0.30, 0.70), (0, 0.05, 0.78), "pelvis")
    bone("spine_02", (0, 0.05, 0.78), (0, -0.22, 0.81), "spine_01")
    bone("spine_03", (0, -0.22, 0.81), (0, -0.46, 0.84), "spine_02")
    bone("neck_01", (0, -0.46, 0.84), (0, -0.64, 0.94), "spine_03")
    bone("head", (0, -0.64, 0.94), (0, -0.92, 0.94), "neck_01")
    bone("jaw", (0, -0.86, 0.88), (0, -1.06, 0.83), "head")
    for side in ("l", "r"):
        x = -1 if side == "l" else 1
        bone(f"ear_{side}", (0.15 * x, -0.70, 1.11), (0.15 * x, -0.70, 1.30), "head")
        bone(f"eye_{side}", (0.105 * x, -0.91, 1.02), (0.105 * x, -0.98, 1.02), "head")
        front = f"front_{side}"
        rear = f"rear_{side}"
        bone(f"{front}_upper", (0.22 * x, -0.36, 0.70), (0.22 * x, -0.39, 0.40), "spine_03")
        bone(f"{front}_lower", (0.22 * x, -0.39, 0.40), (0.22 * x, -0.42, 0.14), f"{front}_upper")
        bone(f"{front}_foot", (0.22 * x, -0.42, 0.14), (0.22 * x, -0.49, 0.08), f"{front}_lower")
        bone(f"{front}_digits", (0.22 * x, -0.49, 0.08), (0.22 * x, -0.60, 0.08), f"{front}_foot")
        bone(f"{rear}_upper", (0.22 * x, 0.40, 0.67), (0.22 * x, 0.48, 0.37), "pelvis")
        bone(f"{rear}_lower", (0.22 * x, 0.48, 0.37), (0.22 * x, 0.40, 0.14), f"{rear}_upper")
        bone(f"{rear}_foot", (0.22 * x, 0.40, 0.14), (0.22 * x, 0.32, 0.08), f"{rear}_lower")
        bone(f"{rear}_digits", (0.22 * x, 0.32, 0.08), (0.22 * x, 0.21, 0.08), f"{rear}_foot")
    previous = "pelvis"
    for index, point in enumerate([(0, 0.87, 0.70), (0, 1.03, 0.71), (0, 1.14, 0.77), (0, 1.18, 0.83)], 1):
        next_point = (0, point[1] + 0.12, point[2] + 0.03)
        bone(f"tail_{index:02d}", point, next_point, previous)
        previous = f"tail_{index:02d}"
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def attach_to_bones(parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    # The first-pass asset uses stable rigid part groups.  An Armature modifier
    # is used instead of bone-parenting each object so the source and exported
    # transforms stay in the same world space (and the meshes do not drift when
    # Blender/Godot evaluates the skeleton).
    for part in parts:
        bone_name = "spine_02"
        if part.name.startswith("Head") or part.name.startswith("Muzzle") or part.name.startswith("Nose") or part.name.startswith("Jaw") or part.name.startswith("Eye") or part.name.startswith("Ear"):
            bone_name = "head"
        elif part.name.startswith("Neck"):
            bone_name = "neck_01"
        elif part.name.startswith("Tail_"):
            number = int(part.name.split("_")[1])
            bone_name = f"tail_{min(number, 4):02d}"
        elif part.name.startswith("Front_"):
            side = "l" if "_L" in part.name else "r"
            if "Upper" in part.name:
                bone_name = f"front_{side}_upper"
            elif "Joint" in part.name or "Lower" in part.name:
                bone_name = f"front_{side}_lower"
            else:
                bone_name = f"front_{side}_foot"
        elif part.name.startswith("Rear_"):
            side = "l" if "_L" in part.name else "r"
            if "Upper" in part.name:
                bone_name = f"rear_{side}_upper"
            elif "Joint" in part.name or "Lower" in part.name:
                bone_name = f"rear_{side}_lower"
            else:
                bone_name = f"rear_{side}_foot"
        part.parent = armature
        if part.type != "MESH":
            continue
        group = part.vertex_groups.new(name=bone_name)
        group.add(list(range(len(part.data.vertices))), 1.0, "REPLACE")
        modifier = part.modifiers.new("CanidArmature", "ARMATURE")
        modifier.object = armature


def keyframe_action(armature: bpy.types.Object, name: str, frame_count: int, poses: list[tuple[int, dict[str, tuple[float, float, float]]]]) -> None:
    action = bpy.data.actions.new(name)
    # Keep every clip in the .blend and GLB even though only the most recently
    # authored action is active on the armature at export time.
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, pose in poses:
        bpy.context.scene.frame_set(frame)
        for bone_name, rotation in pose.items():
            if bone_name not in armature.pose.bones:
                continue
            pb = armature.pose.bones[bone_name]
            pb.rotation_euler = rotation
            pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)
    # Blender 5 stores action curves through layered channels, while Blender 4
    # exposes ``fcurves`` directly.  Keyframes are valid in both versions; the
    # optional interpolation pass is only needed on the older API.
    if hasattr(action, "fcurves"):
        for fcurve in action.fcurves:
            for key in fcurve.keyframe_points:
                key.interpolation = "BEZIER"


def create_actions(armature: bpy.types.Object) -> None:
    keyframe_action(armature, "idle", 48, [(1, {}), (24, {"spine_02": (0.02, 0, 0), "tail_02": (0, 0.05, 0)}), (48, {})])
    keyframe_action(armature, "walk", 32, [
        (1, {"front_l_upper": (0.28, 0, 0), "rear_l_upper": (-0.24, 0, 0), "front_r_upper": (-0.28, 0, 0), "rear_r_upper": (0.24, 0, 0), "tail_02": (0, 0.12, 0)}),
        (17, {"front_l_upper": (-0.28, 0, 0), "rear_l_upper": (0.24, 0, 0), "front_r_upper": (0.28, 0, 0), "rear_r_upper": (-0.24, 0, 0), "tail_02": (0, -0.12, 0)}),
        (32, {"front_l_upper": (0.28, 0, 0), "rear_l_upper": (-0.24, 0, 0), "front_r_upper": (-0.28, 0, 0), "rear_r_upper": (0.24, 0, 0), "tail_02": (0, 0.12, 0)}),
    ])
    keyframe_action(armature, "trot", 24, [
        (1, {"front_l_upper": (0.5, 0, 0), "rear_l_upper": (0.5, 0, 0), "front_r_upper": (-0.5, 0, 0), "rear_r_upper": (-0.5, 0, 0)}),
        (13, {"front_l_upper": (-0.5, 0, 0), "rear_l_upper": (-0.5, 0, 0), "front_r_upper": (0.5, 0, 0), "rear_r_upper": (0.5, 0, 0)}),
        (24, {"front_l_upper": (0.5, 0, 0), "rear_l_upper": (0.5, 0, 0), "front_r_upper": (-0.5, 0, 0), "rear_r_upper": (-0.5, 0, 0)}),
    ])
    keyframe_action(armature, "run", 20, [
        (1, {"front_l_upper": (0.7, 0, 0), "rear_r_upper": (0.7, 0, 0), "front_r_upper": (-0.7, 0, 0), "rear_l_upper": (-0.7, 0, 0), "spine_02": (0.05, 0, 0)}),
        (11, {"front_l_upper": (-0.7, 0, 0), "rear_r_upper": (-0.7, 0, 0), "front_r_upper": (0.7, 0, 0), "rear_l_upper": (0.7, 0, 0), "spine_02": (-0.05, 0, 0)}),
        (20, {"front_l_upper": (0.7, 0, 0), "rear_r_upper": (0.7, 0, 0), "front_r_upper": (-0.7, 0, 0), "rear_l_upper": (-0.7, 0, 0), "spine_02": (0.05, 0, 0)}),
    ])
    keyframe_action(armature, "eat", 32, [(1, {"neck_01": (0.25, 0, 0), "head": (0.28, 0, 0)}), (16, {"neck_01": (0.48, 0, 0), "head": (0.35, 0, 0)}), (32, {"neck_01": (0.25, 0, 0), "head": (0.28, 0, 0)})])
    keyframe_action(armature, "attack", 20, [(1, {"neck_01": (-0.2, 0, 0), "head": (-0.2, 0, 0)}), (8, {"neck_01": (0.36, 0, 0), "head": (0.42, 0, 0), "jaw": (-0.4, 0, 0)}), (20, {"neck_01": (-0.2, 0, 0), "head": (-0.2, 0, 0)})])
    keyframe_action(armature, "hurt", 24, [(1, {"spine_02": (-0.15, 0.12, 0), "head": (0.1, 0, 0)}), (12, {"spine_02": (0.15, -0.12, 0), "head": (-0.1, 0, 0)}), (24, {"spine_02": (-0.15, 0.12, 0), "head": (0.1, 0, 0)})])
    # Loop all locomotion and idle clips; one-shot actions can be restarted by
    # the runtime animation adapter when attack/eat/hurt events are connected.
    for action_name in ("idle", "walk", "trot", "run"):
        action = bpy.data.actions.get(action_name)
        if action:
            action.use_cyclic = True


def setup_preview(armature: bpy.types.Object) -> None:
    bpy.ops.object.camera_add(location=(2.5, -3.4, 1.55))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.rotation_euler = (math.radians(70), 0, math.radians(36))
    # Aim the camera at the model without relying on a constraint in exports.
    direction = Vector((0, -0.05, 0.65)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 3.0))
    key = bpy.context.object
    key.data.energy = 700
    key.data.shape = "DISK"
    key.data.size = 4
    key.rotation_euler = (math.radians(25), 0, math.radians(35))
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.5, 1.4))
    fill = bpy.context.object
    fill.data.energy = 350
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
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / f"{MODEL_NAME}.blend"))
    bpy.ops.export_scene.gltf(
        filepath=str(ROOT / f"{MODEL_NAME}.glb"),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_extra_animations=True,
        export_nla_strips=True,
        export_skins=True,
        export_morph=False,
    )
    # The runtime copies only the project-owned GLB.  Blender remains the
    # editable source of truth and no downloaded asset is admitted here.
    import shutil
    shutil.copy2(ROOT / f"{MODEL_NAME}.glb", RUNTIME_ROOT / f"{MODEL_NAME}.glb")
    metadata = {
        "asset_id": MODEL_NAME,
        "display_name": "Wolf / Canid Rig v1",
        "source": "Project-generated from procedural Blender source",
        "creator": "Unnamed Project 2026",
        "license": "Project-owned generated asset",
        "usage": "Production Approved",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "shoulder_height_m": 0.70,
        "orientation": "Blender -Y / Godot -Z forward",
        "rig_family": "canid",
        "semantic_anatomy": ["head", "jaw", "torso", "front_leg_l", "front_leg_r", "rear_leg_l", "rear_leg_r", "tail"],
        "required_bones": [
            "root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head", "jaw",
            "front_l_upper", "front_l_lower", "front_l_foot", "front_l_digits",
            "front_r_upper", "front_r_lower", "front_r_foot", "front_r_digits",
            "rear_l_upper", "rear_l_lower", "rear_l_foot", "rear_l_digits",
            "rear_r_upper", "rear_r_lower", "rear_r_foot", "rear_r_digits",
            "tail_01", "tail_02", "tail_03", "tail_04", "ear_l", "ear_r", "eye_l", "eye_r",
        ],
        "animations": ["idle", "walk", "trot", "run", "eat", "attack", "hurt"],
        "runtime_path": f"game/assets/creatures/wolf/{MODEL_NAME}.glb",
        "fallback": "EntityView procedural canid body if the GLB is unavailable",
        "notes": "Stylized wolf silhouette with digitigrade legs, pointed ears, tapered muzzle, expressive tail, and three-toed paws.",
    }
    (ROOT / f"{MODEL_NAME}.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    (RUNTIME_ROOT / f"{MODEL_NAME}.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    bpy.context.scene.render.filepath = str(ROOT / f"{MODEL_NAME}_preview.png")
    bpy.ops.render.render(write_still=True)


def main() -> None:
    clear_scene()
    parts = create_mesh()
    armature = create_armature()
    attach_to_bones(parts, armature)
    create_actions(armature)
    setup_preview(armature)
    export()


if __name__ == "__main__":
    main()

