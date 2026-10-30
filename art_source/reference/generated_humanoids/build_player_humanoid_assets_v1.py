"""Build project-owned player humanoid body and workwear assets.

Run with Blender 5.x in background mode. Source bodies and the NPC runtime
assets are read only; all player variants and wearables are written to their
own paths.
"""
from __future__ import annotations

import importlib.util
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector


ROOT = Path(__file__).resolve().parents[3]
GENERATED = ROOT / "art_source/reference/generated_humanoids"
MALE_SOURCE = GENERATED / "human_male_humanoid_rig_v1.blend"
FEMALE_BASE = GENERATED / "blender_female_base_source_v1.blend"
FEMALE_RIG_SOURCE = GENERATED / "human_elf_adapted_rig_v1.blend"
ASSET_ROOT = ROOT / "game/assets/humanoids/human"
CLOTHING_ROOT = ROOT / "game/assets/items/clothing"

spec = importlib.util.spec_from_file_location(
    "human_male_variant_builder", GENERATED / "build_human_male_variant.py"
)
if spec is None or spec.loader is None:
    raise RuntimeError("Could not load the shared Humanoid Rig v1 garment builder.")
builder = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = builder
spec.loader.exec_module(builder)

locomotion_spec = importlib.util.spec_from_file_location(
    "humanoid_locomotion_v1", ROOT / "art_source/blender/humanoid_locomotion_v1.py"
)
if locomotion_spec is None or locomotion_spec.loader is None:
    raise RuntimeError("Could not load the shared humanoid locomotion authoring helpers.")
locomotion = importlib.util.module_from_spec(locomotion_spec)
sys.modules[locomotion_spec.name] = locomotion
locomotion_spec.loader.exec_module(locomotion)


FEMALE_ANCHORS = {
    "anchor_root": ("root", (0.0, 0.036, 0.0)),
    "anchor_foot_l": ("foot_l", (0.053, 0.044, 0.084)),
    "anchor_foot_r": ("foot_r", (-0.053, 0.044, 0.084)),
    "anchor_hand_l": ("hand_l", None),
    "anchor_hand_r": ("hand_r", None),
    "anchor_carry": ("pelvis", (0.0, 0.105, 0.98)),
    "anchor_mouth": ("head", (0.0, -0.135, 1.555)),
    "anchor_anus": ("pelvis", (0.0, 0.085, 0.89)),
    "anchor_genital": ("pelvis", (0.0, -0.13, 0.87)),
    "anchor_urethral": ("pelvis", (0.0, -0.15, 0.875)),
    "anchor_vaginal": ("pelvis", (0.0, -0.15, 0.87)),
    "anchor_navel": ("spine_01", (0.0, -0.125, 1.055)),
    "anchor_breast_l": ("spine_03", (0.085, -0.14, 1.25)),
    "anchor_breast_r": ("spine_03", (-0.085, -0.14, 1.25)),
}


def normalize_source_body(body, source_body):
    world_matrix = body.matrix_world.copy()
    body.parent = None
    body.matrix_world = world_matrix
    for group in list(body.vertex_groups):
        body.vertex_groups.remove(group)
    for modifier in list(body.modifiers):
        if modifier.type == "ARMATURE":
            body.modifiers.remove(modifier)

    bpy.context.view_layer.update()
    source_world = [source_body.matrix_world @ vertex.co for vertex in source_body.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in source_world) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in source_world) for axis in range(3)))
    source_height = maximum.z - minimum.z
    if source_height <= 0.1:
        raise RuntimeError("Blender female base has an invalid source height.")

    scale = builder.TARGET_HEIGHT_M / source_height
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    target_body_y = -0.0076
    target_foot_z = 0.01
    reference_inverse = body.matrix_world.inverted()

    def normalize_point(point):
        normalized = Vector((
            (point.x - center_x) * scale,
            (point.y - center_y) * scale + target_body_y,
            (point.z - minimum.z) * scale + target_foot_z,
        ))
        arm_region = (
            (normalized.z > 1.10 and abs(normalized.x) > 0.12)
            or (normalized.z > 0.70 and abs(normalized.x) > 0.29)
        )
        if arm_region:
            side = 1.0 if normalized.x > 0.0 else -1.0
            influence = builder.smoothstep(0.12, 0.29, abs(normalized.x))
            angle = -side * math.radians(58.0) * influence
            pivot = Vector((side * 0.16, target_body_y, 1.38))
            normalized = pivot + Matrix.Rotation(angle, 4, "Y") @ (normalized - pivot)
        return reference_inverse @ normalized

    points = [normalize_point(point) for point in source_world]
    mesh = source_body.data.copy()
    mesh.name = "BlenderFemaleBodyBaseMesh"
    for vertex, point in zip(mesh.vertices, points):
        vertex.co = point
    body.data = mesh
    bpy.context.view_layer.update()
    builder.bind_body_to_canonical_rig(body, next(
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    ))
    return normalize_point


def deform_female_body():
    body = bpy.data.objects["HumanBody"]
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    with bpy.data.libraries.load(str(FEMALE_BASE), link=False) as (source, target):
        requested = ("BlenderFemaleBodyBase", "BlenderFemaleEye.L", "BlenderFemaleEye.R")
        missing = set(requested) - set(source.objects)
        if missing:
            raise RuntimeError("Female base source is missing objects: {}".format(sorted(missing)))
        target.objects = list(requested)
    source_body, source_eye_l, source_eye_r = target.objects
    for source_obj in target.objects:
        bpy.context.collection.objects.link(source_obj)
    bpy.context.view_layer.update()

    normalize_point = normalize_source_body(body, source_body)
    skin = builder.make_project_material(
        "Human_Female_Skin", (0.62, 0.38, 0.29), roughness=0.87
    )
    body.data.materials.clear()
    body.data.materials.append(skin)
    for polygon in body.data.polygons:
        polygon.use_smooth = True

    eye_white = builder.make_project_material("Human_Female_Eye_White", (0.80, 0.77, 0.68), roughness=0.38)
    iris = builder.make_project_material("Human_Female_Iris", (0.18, 0.095, 0.048), roughness=0.35)
    pupil = builder.make_project_material("Human_Female_Pupil", (0.012, 0.009, 0.007), roughness=0.24)
    for side, source_eye in (("l", source_eye_l), ("r", source_eye_r)):
        eye_positions = [normalize_point(source_eye.matrix_world @ vertex.co)
                         for vertex in source_eye.data.vertices]
        eye_mesh = source_eye.data.copy()
        for vertex, position in zip(eye_mesh.vertices, eye_positions):
            vertex.co = position
        eye = builder.create_head_weighted_object(
            "HumanFemaleEyeBall_{}".format(side), eye_mesh, armature, body, eye_white
        )
        center = sum(eye_positions, Vector()) / len(eye_positions)
        builder.create_head_weighted_object(
            "HumanFemaleIris_{}".format(side),
            builder.make_uv_sphere_mesh(
                "HumanFemaleIrisMesh_{}".format(side),
                Vector((center.x, center.y - 0.0098, center.z)),
                (0.0048, 0.0022, 0.0052),
            ),
            armature, body, iris,
        )
        builder.create_head_weighted_object(
            "HumanFemalePupil_{}".format(side),
            builder.make_uv_sphere_mesh(
                "HumanFemalePupilMesh_{}".format(side),
                Vector((center.x, center.y - 0.0115, center.z)),
                (0.0020, 0.0012, 0.0025),
            ),
            armature, body, pupil,
        )
        bpy.data.objects.remove(source_eye, do_unlink=True)

    bpy.data.objects.remove(source_body, do_unlink=True)
    for old_name in ("HumanHair", "HumanBrows", "HumanEyes", "HumanLashes",
                     "HumanShirt", "HumanBoots"):
        old = bpy.data.objects.get(old_name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    for old in list(bpy.context.scene.objects):
        if old.name.startswith("anchor_"):
            bpy.data.objects.remove(old, do_unlink=True)

    scene = bpy.context.scene
    scene["asset_id"] = "player_human_female_humanoid_rig_v1"
    scene["humanoid_rig_version"] = 1
    scene["source_asset"] = "Blender Human Base Meshes v1.4.1 / GEO-body_female_realistic"
    scene["source_license"] = "CC0 1.0 body; owner approved project use; shared project rig and animations"
    scene["variant"] = "female-presenting adult human player body prototype"
    scene["body_mesh_vertices"] = len(body.data.vertices)
    scene["target_height_m"] = builder.TARGET_HEIGHT_M


def add_upper_body_attack_actions(armature):
    sagittal = (1.0, 0.0, 0.0)
    vertical = (0.0, 0.0, 1.0)
    for side in ("l", "r"):
        action = bpy.data.actions.new("human_attack_{}".format(side))
        armature.animation_data_create()
        armature.animation_data.action = action
        upper = "upperarm_{}".format(side)
        lower = "lowerarm_{}".format(side)
        turn = -1.0 if side == "l" else 1.0
        frames = (
            (1, 0.0, 0.0, 0.0),
            (3, -0.36, 0.78, -0.10 * turn),
            (6, 0.78, 0.18, 0.16 * turn),
            (8, 0.45, 0.32, 0.10 * turn),
            (12, 0.0, 0.0, 0.0),
        )
        for frame, arm_angle, elbow_angle, torso_angle in frames:
            bpy.context.scene.frame_set(frame)
            values = (
                (upper, ((sagittal, arm_angle),)),
                (lower, ((sagittal, elbow_angle),)),
                ("spine_03", ((vertical, torso_angle),)),
            )
            for bone_name, rotations in values:
                pose_bone = armature.pose.bones[bone_name]
                pose_bone.rotation_mode = "QUATERNION"
                rotation = Quaternion((1.0, 0.0, 0.0, 0.0))
                for world_axis, angle in rotations:
                    local_axis = locomotion._local_axis_for_world(
                        armature.data.bones[bone_name], world_axis
                    )
                    rotation @= Quaternion(local_axis, angle)
                pose_bone.rotation_quaternion = rotation
                pose_bone.keyframe_insert(
                    data_path="rotation_quaternion", frame=frame, group=bone_name
                )
        action.use_fake_user = True
        action.use_cyclic = False
    armature.animation_data.action = bpy.data.actions.get("human_idle")
    bpy.context.scene.frame_set(1)


def rename_workwear_nodes():
    names = {
        "HumanMaleShirt": "HumanWorkShirt",
        "HumanMaleShirtButtons": "HumanWorkShirtDetails",
        "HumanTrousers": "HumanWorkTrousers",
        "HumanMaleLeatherBelt": "HumanWorkBelt",
        "HumanMaleBeltBuckle": "HumanWorkBeltBuckle",
        "HumanMaleBoots": "HumanBoots",
        "HumanMaleShortHair": "HumanHair",
    }
    for source_name, target_name in names.items():
        obj = bpy.data.objects.get(source_name)
        if obj is not None:
            obj.name = target_name
            obj.data.name = target_name + "Mesh"
    for side in ("l", "r"):
        for prefix in ("HumanMaleEyeBall", "HumanMaleIris", "HumanMalePupil"):
            obj = bpy.data.objects.get("{}_{}".format(prefix, side))
            if obj is not None:
                obj.name = "{}_{}".format(prefix.replace("HumanMale", "Human"), side)
                obj.data.name = obj.name + "Mesh"


def validate_female_asset():
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError("Expected exactly one female Humanoid Rig v1 armature.")
    armature = armatures[0]
    required_bones = {
        "root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
        "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
        "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
        "thigh_l", "calf_l", "foot_l", "toe_l", "thigh_r", "calf_r", "foot_r", "toe_r",
    }
    missing = required_bones - set(armature.data.bones.keys())
    if missing:
        raise RuntimeError("Female Humanoid Rig v1 is missing bones: {}".format(sorted(missing)))

    body = bpy.data.objects.get("HumanBody")
    shirt = bpy.data.objects.get("HumanMaleShirt")
    trousers = bpy.data.objects.get("HumanTrousers")
    if body is None or shirt is None or trousers is None:
        raise RuntimeError("Female body or one of its workwear fit variants is missing.")
    body_height = max(vertex.co.z for vertex in body.data.vertices) - min(
        vertex.co.z for vertex in body.data.vertices
    )
    if abs(body_height - builder.TARGET_HEIGHT_M) > 0.02:
        raise RuntimeError("Female body height {:.3f} m is outside the 1.75 m target.".format(body_height))
    for garment in (shirt, trousers):
        if not any(modifier.type == "ARMATURE" and modifier.object == armature
                   for modifier in garment.modifiers):
            raise RuntimeError("Female garment {} is not skinned to Humanoid Rig v1.".format(garment.name))
        shells = [modifier for modifier in garment.modifiers if modifier.type == "SOLIDIFY"]
        if len(shells) != 1:
            raise RuntimeError("Female garment {} is missing its physical fabric shell.".format(garment.name))
    if not all(
        bpy.data.objects.get(name) is not None
        and bpy.data.objects[name].parent_type == "BONE"
        for name in FEMALE_ANCHORS
    ):
        raise RuntimeError("Female player model is missing a semantic interaction anchor.")
    if not any(action.name == "human_idle" for action in bpy.data.actions):
        raise RuntimeError("The female player body needs the shared idle animation.")


def export_selected(path, objects, export_animations):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    armature = next(obj for obj in objects if obj.type == "ARMATURE")
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_animations=export_animations,
        export_skins=True,
        export_yup=True,
        export_apply=True,
    )


def export_player_asset_set(sex, authoring_blend):
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    if sex == "male":
        builder.validate_asset()
    else:
        validate_female_asset()
    # Keep the long sleeves off the waist and underarms at rest. Source rigs
    # with A/T-pose arms otherwise drop both upper arms through the shirt sides.
    locomotion.add_humanoid_locomotion_actions(armature, maximum_arm_drop=0.72)
    add_upper_body_attack_actions(armature)
    bpy.context.scene.frame_set(1)

    # Validate the exported closed garment shells using their author's names
    # before changing names to stable, sex-neutral runtime identifiers.
    body_path = ASSET_ROOT / "player_{}_humanoid_rig_v1.glb".format(sex)
    original_names = {
        "HumanMaleShirt": "HumanWorkShirt",
        "HumanMaleShirtButtons": "HumanWorkShirtDetails",
        "HumanTrousers": "HumanWorkTrousers",
        "HumanMaleLeatherBelt": "HumanWorkBelt",
        "HumanMaleBeltBuckle": "HumanWorkBeltBuckle",
        "HumanMaleBoots": "HumanBoots",
        "HumanMaleShortHair": "HumanHair",
    }
    if sex == "male":
        temporary = body_path.with_name("player_male_garment_validation.glb")
        builder.OUT_GLB = temporary
        export_selected(temporary, [
            obj for obj in bpy.context.scene.objects
            if obj.type in {"ARMATURE", "MESH", "EMPTY"}
        ], export_animations=True)
        builder.validate_exported_anchors()
        builder.validate_exported_garment_shells()
    else:
        temporary = body_path.with_name("player_female_garment_validation.glb")
        builder.OUT_GLB = temporary
        export_selected(temporary, [
            obj for obj in bpy.context.scene.objects
            if obj.type in {"ARMATURE", "MESH", "EMPTY"}
        ], export_animations=True)
        builder.validate_exported_anchors()
        builder.validate_exported_garment_shells()

    for source_name, target_name in original_names.items():
        obj = bpy.data.objects.get(source_name)
        if obj is not None:
            obj.name = target_name
            obj.data.name = target_name + "Mesh"
    for side in ("l", "r"):
        for prefix in ("HumanMaleEyeBall", "HumanMaleIris", "HumanMalePupil"):
            obj = bpy.data.objects.get("{}_{}".format(prefix, side))
            if obj is not None:
                obj.name = "{}_{}".format(prefix.replace("HumanMale", "Human"), side)
                obj.data.name = obj.name + "Mesh"

    garment_variants = {
        "shirt_{}".format(sex): [
            "HumanWorkShirt", "HumanWorkShirtDetails"
        ],
        "trousers_{}".format(sex): [
            "HumanWorkTrousers", "HumanWorkBelt", "HumanWorkBeltBuckle"
        ],
    }
    for variant, object_names in garment_variants.items():
        garment_path = CLOTHING_ROOT / "workwear_{}.glb".format(variant)
        export_selected(garment_path, [
            armature, *(bpy.data.objects[name] for name in object_names)
        ], export_animations=False)

    export_selected(body_path, [
        obj for obj in bpy.context.scene.objects
        if obj.type in {"ARMATURE", "MESH", "EMPTY"}
    ], export_animations=True)

    # Clothing-specific GLBs contain the same meshes as the named parts inside
    # the player model, allowing the first implementation to toggle them without
    # creating a second skeleton while preserving a clean asset boundary.
    bpy.ops.wm.save_as_mainfile(filepath=str(authoring_blend))
    print("Player {} model written: {}".format(sex, body_path))
    print("Player {} source written: {}".format(sex, authoring_blend))


def rebuild_player_shirt_and_belt():
    # Refit these pieces to the active Blender base body. The belt is sampled
    # from that body's waist dimensions, so the female base cannot inherit a
    # male-only waist radius.
    for name in (
        "HumanMaleShirt", "HumanMaleShirtButtons",
        "HumanMaleLeatherBelt", "HumanMaleBeltBuckle",
    ):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    builder.make_male_shirt_torso(include_sleeves=False)
    builder.make_shirt_buttons()
    builder.make_leather_belt()


def build_male():
    bpy.ops.wm.open_mainfile(filepath=str(MALE_SOURCE))
    builder.OUT_GLB = ASSET_ROOT / "player_male_humanoid_rig_v1.glb"
    builder.ANCHOR_TARGETS = builder.ANCHOR_TARGETS
    rebuild_player_shirt_and_belt()
    builder.configure_render()
    builder.OUT_PREVIEW = GENERATED / "player_human_male_humanoid_rig_v1_preview.png"
    builder.OUT_FRONT_PREVIEW = GENERATED / "player_human_male_humanoid_rig_v1_front.png"
    builder.OUT_SIDE_PREVIEW = GENERATED / "player_human_male_humanoid_rig_v1_side.png"
    builder.OUT_BACK_PREVIEW = GENERATED / "player_human_male_humanoid_rig_v1_back.png"
    bpy.context.scene.render.filepath = str(builder.OUT_PREVIEW)
    bpy.ops.render.render(write_still=True)
    builder.render_turntable_previews()
    export_player_asset_set(
        "male", GENERATED / "player_human_male_humanoid_rig_v1.blend"
    )


def build_female():
    bpy.ops.wm.open_mainfile(filepath=str(FEMALE_RIG_SOURCE))
    deform_female_body()
    builder.ANCHOR_TARGETS = FEMALE_ANCHORS
    builder.make_short_hair()
    rebuild_player_shirt_and_belt()
    builder.make_trousers()
    builder.make_male_boots()
    builder.add_semantic_anchors()

    builder.configure_render()
    builder.OUT_GLB = ASSET_ROOT / "player_female_humanoid_rig_v1.glb"
    builder.OUT_PREVIEW = GENERATED / "player_human_female_humanoid_rig_v1_preview.png"
    builder.OUT_FRONT_PREVIEW = GENERATED / "player_human_female_humanoid_rig_v1_front.png"
    builder.OUT_SIDE_PREVIEW = GENERATED / "player_human_female_humanoid_rig_v1_side.png"
    builder.OUT_BACK_PREVIEW = GENERATED / "player_human_female_humanoid_rig_v1_back.png"
    bpy.context.scene.render.filepath = str(builder.OUT_PREVIEW)
    bpy.ops.render.render(write_still=True)
    builder.render_turntable_previews()
    export_player_asset_set(
        "female", GENERATED / "player_human_female_humanoid_rig_v1.blend"
    )


if __name__ == "__main__":
    build_male()
    build_female()
