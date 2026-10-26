"""Adapt the locally approved Radagon FullClothes FBX for a prototype human NPC.

The original commercial source package stays outside the project. This script
requires explicit source FBX and texture paths and writes only the project-ready
derived scene, GLB, and metadata. Run Blender with --disable-autoexec.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from humanoid_locomotion_v1 import add_humanoid_locomotion_actions


PROJECT_ROOT = Path(__file__).resolve().parents[2]
RUNTIME_ROOT = PROJECT_ROOT / "game/assets/humanoids/human"
SOURCE_BODY_ROOT = PROJECT_ROOT / "art_source/reference/generated_humanoids"
ASSET_ID = "radagon_male_humanoid_rig_v1"
TARGET_HEIGHT_M = 1.75
GENITAL_DOWNWARD_ANGLE_DEGREES = 70.0
GENITAL_CLOTHING_CLEARANCE_M = 0.04
MAX_RUNTIME_GLB_BYTES = 100_000_000
REQUIRED_BONES = {
    "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l", "ball_l",
    "thigh_r", "calf_r", "foot_r", "ball_r",
    *{f"{finger}_{segment}_{side}" for side in ("l", "r")
      for finger in ("thumb", "index", "middle", "ring", "pinky")
      for segment in ("01", "02", "03")},
    *{f"spline_{segment:02d}.x" for segment in range(1, 7)},
}
PROJECT_BONE_MAP = {
    "root": "root (source armature object)",
    "toe_l": "ball_l",
    "toe_r": "ball_r",
    "anchor_genital": "pelvis (mesh-derived base position)",
    "anchor_penile": "spline_06.x (mesh-derived tip position)",
    "anchor_urethral": "spline_06.x (mesh-derived tip position)",
}
TEXTURE_SIZES = {
    "body": 1024, "head": 1024, "clothes": 1024, "eyes": 512,
    "hair": 1024, "eyelashes": 512,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-fbx", type=Path, required=True)
    parser.add_argument("--source-blend", type=Path, required=True,
                        help="Original Radagon Blender file used for packed cracked-skin maps.")
    parser.add_argument("--texture-dir", type=Path, required=True)
    parser.add_argument("--output-glb", type=Path,
                        default=RUNTIME_ROOT / "radagon_male_humanoid_rig_v1.glb")
    parser.add_argument("--output-json", type=Path,
                        default=RUNTIME_ROOT / "radagon_male_humanoid_rig_v1.json")
    parser.add_argument("--output-blend", type=Path,
                        default=SOURCE_BODY_ROOT / "radagon_male_humanoid_rig_v1.blend")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    if not points:
        raise RuntimeError("The adapted Radagon scene has no meshes.")
    return (Vector((min(point[i] for point in points) for i in range(3))),
            Vector((max(point[i] for point in points) for i in range(3))))


def verify_source_skeleton(armature: bpy.types.Object) -> None:
    missing = REQUIRED_BONES - set(armature.data.bones.keys())
    if missing:
        raise RuntimeError(f"Radagon source rig is missing expected anatomy bones: {sorted(missing)}")
    armature["asset_id"] = ASSET_ID
    armature["project_bone_map"] = json.dumps(PROJECT_BONE_MAP, sort_keys=True)
    armature["source_rig_preserved"] = True


def load_texture(texture_dir: Path, filename: str, max_size: int, non_color: bool = False) -> bpy.types.Image:
    path = texture_dir / filename
    if not path.is_file():
        raise FileNotFoundError(f"Required Radagon texture not found: {path}")
    image = bpy.data.images.load(str(path), check_existing=True)
    image.name = Path(filename).stem
    factor = min(1.0, max_size / max(image.size[0], image.size[1]))
    if factor < 1.0:
        image.scale(max(1, round(image.size[0] * factor)), max(1, round(image.size[1] * factor)))
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    image.pack()
    return image


def material(name: str, color: tuple[float, float, float], texture_dir: Path,
             base: str | None = None, normal: str | None = None,
             roughness: str | None = None, metallic: str | None = None,
             opacity: str | None = None, emissive_image: bpy.types.Image | None = None,
             base_image: bpy.types.Image | None = None,
             normal_image: bpy.types.Image | None = None,
             roughness_image: bpy.types.Image | None = None) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = (*color, 1.0)
    tree = result.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = 0.72

    def texture_node(filename: str, category: str, non_color: bool = False):
        image = load_texture(texture_dir, filename, TEXTURE_SIZES[category], non_color)
        node = tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        return node

    if base or base_image:
        if base_image:
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = base_image
        else:
            node = texture_node(base, name.lower().replace("radagon_", ""))
        if name == "Radagon_Hair":
            tint = tree.nodes.new("ShaderNodeMixRGB")
            tint.blend_type = "MULTIPLY"
            tint.inputs[0].default_value = 1.0
            tint.inputs[2].default_value = (*color, 1.0)
            tree.links.new(node.outputs["Color"], tint.inputs[1])
            tree.links.new(tint.outputs["Color"], shader.inputs["Base Color"])
        else:
            tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    if roughness or roughness_image:
        if roughness_image:
            roughness_image.colorspace_settings.name = "Non-Color"
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = roughness_image
        else:
            node = texture_node(roughness, name.lower().replace("radagon_", ""), True)
        tree.links.new(node.outputs["Color"], shader.inputs["Roughness"])
    if metallic:
        node = texture_node(metallic, "clothes", True)
        tree.links.new(node.outputs["Color"], shader.inputs["Metallic"])
    if normal or normal_image:
        if normal_image:
            normal_image.colorspace_settings.name = "Non-Color"
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = normal_image
        else:
            node = texture_node(normal, name.lower().replace("radagon_", ""), True)
        normal_map = tree.nodes.new("ShaderNodeNormalMap")
        tree.links.new(node.outputs["Color"], normal_map.inputs["Color"])
        tree.links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    if opacity:
        node = texture_node(opacity, "hair" if "hair" in name.lower() else "eyelashes", True)
        tree.links.new(node.outputs["Color"], shader.inputs["Alpha"])
        result.surface_render_method = "DITHERED"
    if emissive_image:
        node = tree.nodes.new("ShaderNodeTexImage")
        node.image = emissive_image
        node.image.colorspace_settings.name = "Non-Color"
        tint = tree.nodes.new("ShaderNodeMixRGB")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1.0
        tint.inputs[2].default_value = (1.0, 0.58, 0.12, 1.0)
        tree.links.new(node.outputs["Color"], tint.inputs[1])
        emission_input = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
        if emission_input is not None:
            tree.links.new(tint.outputs["Color"], emission_input)
        strength_input = shader.inputs.get("Emission Strength")
        if strength_input is not None:
            strength_input.default_value = 2.0
    return result


def load_cracked_skin_images(source_blend: Path) -> dict[str, bpy.types.Image]:
    required = (
        "Radagon_Body_BaseColor_Cracked.tga",
        "Radagon_Body_Emissive_Cracked.tga",
        "Radagon_Head_Cracked_BaseColor.tga",
        "Radagon_Head_Cracked_Emissive.tga",
        "Radagon_Penis_BaseColor.tga",
        "Radagon_Penis_Normal.tga",
        "Radagon_Penis_Roughness.tga",
    )
    with bpy.data.libraries.load(str(source_blend), link=False) as (data_from, data_to):
        missing = set(required) - set(data_from.images)
        if missing:
            raise RuntimeError(f"Source Blender file is missing cracked-skin maps: {sorted(missing)}")
        data_to.images = list(required)
    images = dict(zip(required, data_to.images))
    for image in images.values():
        if image is None:
            raise RuntimeError("A cracked-skin image could not be loaded from the source Blender file.")
        scale = min(1.0, 1024 / max(image.size[0], image.size[1]))
        if scale < 1.0:
            image.scale(max(1, round(image.size[0] * scale)), max(1, round(image.size[1] * scale)))
        image.pack()
        if "_Normal." in image.name or "_Roughness." in image.name:
            image.colorspace_settings.name = "Non-Color"
    return images


def replace_materials(texture_dir: Path, meshes: list[bpy.types.Object],
                      cracked_skin_images: dict[str, bpy.types.Image]) -> None:
    palette = {
        "Radagon_Head": material("Radagon_Head", (0.72, 0.53, 0.43), texture_dir,
            "Radagon_Head_BaseColor.tga", "Radagon_Head_Normal.tga", "Radagon_Head_Roughness.tga",
            emissive_image=cracked_skin_images["Radagon_Head_Cracked_Emissive.tga"],
            base_image=cracked_skin_images["Radagon_Head_Cracked_BaseColor.tga"]),
        "Radagon_Body": material("Radagon_Body", (0.72, 0.53, 0.43), texture_dir,
            "Radagon_Body_BaseColor.tga", "Radagon_Body_Normal.tga", "Radagon_Body_Roughness.tga",
            emissive_image=cracked_skin_images["Radagon_Body_Emissive_Cracked.tga"],
            base_image=cracked_skin_images["Radagon_Body_BaseColor_Cracked.tga"]),
        "Radagon_Clothes": material("Radagon_Clothes", (0.16, 0.15, 0.14), texture_dir,
            "Radagon_Clothes_BaseColor.tga", "Radagon_Clothes_Normal.tga",
            "Radagon_Clothes_Roughness.tga", "Radagon_Clothes_Metallic.tga"),
        "Radagon_Hair": material("Radagon_Hair", (0.45, 0.09, 0.04), texture_dir,
            "Radagon_Hair_BaseColor.tga", "Radagon_Hair_normal.tga", opacity="Radagon_Hair_opacity.tga"),
        "Radagon_Penis": material("Radagon_Penis", (0.72, 0.53, 0.43), texture_dir,
            base_image=cracked_skin_images["Radagon_Penis_BaseColor.tga"],
            normal_image=cracked_skin_images["Radagon_Penis_Normal.tga"],
            roughness_image=cracked_skin_images["Radagon_Penis_Roughness.tga"]),
        "Radagon_Eyes": material("Radagon_Eyes", (0.34, 0.18, 0.08), texture_dir,
            "Radagon_Eyes_BaseColor.tga", "Radagon_Eyes_Normal.tga", "Radagon_Eyes_Roughness.tga"),
        "Eyelashes": material("Radagon_Eyelashes", (0.12, 0.06, 0.04), texture_dir,
            "Radagon_Eyelashes.tga", opacity="Radagon_Eyelashes_Opacity.tga"),
    }
    palette["Cornea"] = bpy.data.materials.new("Radagon_Cornea")
    palette["Cornea"].diffuse_color = (0.72, 0.65, 0.5, 1.0)
    palette["Cornea"].use_nodes = True
    palette["Eyeshadow"] = bpy.data.materials.new("Radagon_Eyeshadow")
    palette["Eyeshadow"].diffuse_color = (0.22, 0.12, 0.10, 1.0)
    palette["Eyeshadow"].use_nodes = True

    for obj in meshes:
        source_materials = list(obj.data.materials)
        if not source_materials:
            obj.data.materials.append(palette["Radagon_Body"])
            continue
        for index, source in enumerate(source_materials):
            if source is None:
                converted = palette["Radagon_Body"]
            elif source.name == "Radagon_Penis":
                converted = palette["Radagon_Penis"]
            else:
                converted = palette.get(source.name, palette["Radagon_Body"])
            # Replace slots in place. Clearing the list resets all polygon
            # material indices to zero and loses source texture assignments.
            obj.data.materials[index] = converted

    for old_material in list(bpy.data.materials):
        if old_material.users == 0:
            bpy.data.materials.remove(old_material)
    palette["Radagon_Penis"].name = "Radagon_Penis"
    for old_image in list(bpy.data.images):
        if old_image.users == 0:
            bpy.data.images.remove(old_image)


def normalize_material_images() -> dict[str, list[int]]:
    """Enforce the declared runtime texture limits, including already-loaded FBX images."""
    def category_for(name: str) -> str:
        lowered = name.lower()
        if "eyelash" in lowered:
            return "eyelashes"
        if "eyes" in lowered:
            return "eyes"
        if "hair" in lowered:
            return "hair"
        if "clothes" in lowered:
            return "clothes"
        if "head" in lowered:
            return "head"
        if "body" in lowered or "penis" in lowered:
            return "body"
        raise RuntimeError(f"No runtime texture limit is defined for image {name}.")

    normalized: dict[str, list[int]] = {}
    for image in bpy.data.images:
        if image.users == 0:
            continue
        category = category_for(image.name)
        max_dimension = TEXTURE_SIZES[category]
        width, height = image.size
        factor = min(1.0, max_dimension / max(width, height))
        if factor < 1.0:
            image.scale(max(1, round(width * factor)), max(1, round(height * factor)))
        # Repack after scaling so glTF exports the edited pixels rather than
        # the original, larger source image embedded by the FBX importer.
        image.pack()
        width, height = image.size
        if max(width, height) > max_dimension:
            raise RuntimeError(
                f"Texture {image.name} exceeds its {category} runtime limit: {width}x{height} > {max_dimension}.")
        normalized[image.name] = [int(width), int(height)]
    return normalized


def add_bone_anchor(armature: bpy.types.Object, name: str, bone_name: str | None,
                    world_position: Vector | None = None) -> Vector:
    bone = armature.data.bones.get(bone_name) if bone_name else None
    if bone_name and bone is None:
        raise RuntimeError(f"Semantic anchor {name} references missing bone {bone_name}.")
    if world_position is None:
        world_position = (armature.matrix_world @ Vector(bone.tail_local)
                          if bone is not None else armature.matrix_world.translation.copy())
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.025
    obj.matrix_world = Matrix.Translation(world_position)
    obj.parent = armature
    if bone_name:
        obj.parent_type = "BONE"
        obj.parent_bone = bone_name
    else:
        obj.parent_type = "OBJECT"
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_world = Matrix.Translation(world_position)
    obj["semantic_anchor"] = True
    obj["anchor_id"] = name
    return world_position


def add_anchors(armature: bpy.types.Object) -> dict[str, tuple[str, Vector]]:
    world = armature.matrix_world
    bones = armature.data.bones
    def center(bone_name: str) -> Vector:
        bone = bones[bone_name]
        return world @ ((Vector(bone.head_local) + Vector(bone.tail_local)) * 0.5)
    pelvis = center("pelvis")
    head = center("head")
    chest = center("spine_02")
    positions = {
        "anchor_root": (None, world.translation.copy()),
        "anchor_foot_l": ("ball_l", None),
        "anchor_foot_r": ("ball_r", None),
        "anchor_hand_l": ("hand_l", None),
        "anchor_hand_r": ("hand_r", None),
        "anchor_carry": ("pelvis", pelvis + Vector((0.0, 0.08, 0.02))),
        "anchor_mouth": ("head", head + Vector((0.0, -0.07, -0.055))),
        "anchor_anus": ("pelvis", pelvis + Vector((0.0, 0.075, -0.10))),
        "anchor_genital": ("pelvis", pelvis + Vector((0.0, -0.085, -0.10))),
        "anchor_urethral": ("pelvis", pelvis + Vector((0.0, -0.10, -0.085))),
        "anchor_navel": ("spine_01", chest + Vector((0.0, -0.105, -0.20))),
        "anchor_penile": ("pelvis", pelvis + Vector((0.0, -0.105, -0.12))),
        "anchor_testicular": ("pelvis", pelvis + Vector((0.0, -0.07, -0.16))),
        "anchor_breast_l": ("spine_03", Vector((0.105, chest.y - 0.10, chest.z + 0.17))),
        "anchor_breast_r": ("spine_03", Vector((-0.105, chest.y - 0.10, chest.z + 0.17))),
    }
    expected_positions = {}
    for name, (bone_name, position) in positions.items():
        actual_position = add_bone_anchor(armature, name, bone_name, position)
        expected_positions[name] = (bone_name or armature.name, actual_position)
    return expected_positions


def pose_genital_region_down(body: bpy.types.Object, normalization_factor: float) -> dict[str, object]:
    """Angle the existing genital surface toward the legs while keeping its seam fixed."""
    material_slots = {material.name: index for index, material in enumerate(body.data.materials)
                      if material is not None}
    if "Radagon_Penis" not in material_slots:
        raise RuntimeError("The Radagon body mesh has no Radagon_Penis material region.")
    material_index = material_slots["Radagon_Penis"]
    genital_faces = [polygon for polygon in body.data.polygons
                     if polygon.material_index == material_index]
    genital_vertices = {vertex for polygon in genital_faces for vertex in polygon.vertices}
    surrounding_vertices = {vertex for polygon in body.data.polygons
                            if polygon.material_index != material_index
                            for vertex in polygon.vertices}
    seam_vertices = genital_vertices & surrounding_vertices
    if len(genital_faces) < 100 or len(seam_vertices) < 3:
        raise RuntimeError("The genital material region is incomplete or disconnected from the body.")

    def average(indices: set[int]) -> Vector:
        return sum((body.data.vertices[index].co for index in indices), Vector()) / len(indices)

    shape_keys = body.data.shape_keys
    key_blocks = list(shape_keys.key_blocks) if shape_keys else []
    if key_blocks:
        reference = shape_keys.reference_key
        key_coordinates = [(key, key.data) for key in key_blocks]
    else:
        reference = None
        key_coordinates = [(None, body.data.vertices)]
    base = average(seam_vertices)
    tip_index = min(genital_vertices, key=lambda index: body.data.vertices[index].co.y)
    tip_before = body.data.vertices[tip_index].co.copy()
    angle = math.radians(GENITAL_DOWNWARD_ANGLE_DEGREES)
    world_scale = body.matrix_world.to_3x3().col[1].length * normalization_factor
    if world_scale <= 0.0:
        raise RuntimeError("The Radagon body has an invalid scale for fitting the genital mesh beneath clothing.")
    tuck_local = GENITAL_CLOTHING_CLEARANCE_M / world_scale
    seam_world = body.matrix_world @ base
    for key, coordinates in key_coordinates:
        key_base = sum((coordinates[index].co for index in seam_vertices), Vector()) / len(seam_vertices)
        key_tip = min(genital_vertices, key=lambda index: coordinates[index].co.y)
        direction_y = coordinates[key_tip].co.y - key_base.y
        if direction_y >= -1.0:
            raise RuntimeError(f"Could not locate the forward-facing anatomy tip in shape key {key.name if key else 'Basis'}.")
        for index in genital_vertices - seam_vertices:
            original = coordinates[index].co.copy()
            progress = max(0.0, min(1.0, (original.y - key_base.y) / direction_y))
            local_angle = angle * progress
            cosine = math.cos(local_angle)
            sine = math.sin(local_angle)
            relative = original - key_base
            coordinates[index].co = key_base + Vector((
                relative.x,
                relative.y * cosine - relative.z * sine + tuck_local * progress,
                relative.y * sine + relative.z * cosine,
            ))
    body.data.update()
    tip_after = (reference.data[tip_index].co.copy() if reference else body.data.vertices[tip_index].co.copy())
    if tip_after.z >= tip_before.z - 2.0:
        raise RuntimeError("Downward genital pose did not lower the mesh tip enough to validate.")

    bpy.context.view_layer.update()
    return {
        "angle_degrees": GENITAL_DOWNWARD_ANGLE_DEGREES,
        "posterior_clothing_clearance_m": GENITAL_CLOTHING_CLEARANCE_M,
        "seam_vertex_count": len(seam_vertices),
        "face_count": len(genital_faces),
        "shape_key_count": len(key_blocks),
        "tip_before_local": [round(value, 5) for value in tip_before],
        "tip_after_local": [round(value, 5) for value in tip_after],
        "tip_world_m": [round(value, 5) for value in (body.matrix_world @ tip_after)],
        "seam_world_m": [round(value, 5) for value in seam_world],
        "tip_drop_m": round((body.matrix_world.to_3x3() @ (tip_after - tip_before)).z, 5),
    }


def limit_skin_weights(meshes: list[bpy.types.Object], maximum_influences: int = 4) -> dict[str, int]:
    """Apply the glTF skin limit explicitly and normalize retained bone weights."""
    modified_vertices = 0
    removed_influences = 0
    for obj in meshes:
        if obj.find_armature() is None or not obj.vertex_groups:
            continue
        to_remove: dict[int, list[int]] = {}
        to_update: list[tuple[int, list[tuple[int, float]]]] = []
        for vertex in obj.data.vertices:
            influences = sorted(((assignment.group, assignment.weight) for assignment in vertex.groups
                                 if assignment.weight > 0.0), key=lambda item: (-item[1], item[0]))
            if len(influences) <= maximum_influences:
                continue
            kept = influences[:maximum_influences]
            total = sum(weight for _, weight in kept)
            to_update.append((vertex.index, [(group, weight / total) for group, weight in kept]))
            for group, _ in influences[maximum_influences:]:
                to_remove.setdefault(group, []).append(vertex.index)
            modified_vertices += 1
            removed_influences += len(influences) - maximum_influences
        for group_index, indices in to_remove.items():
            obj.vertex_groups[group_index].remove(indices)
        for vertex_index, influences in to_update:
            for group_index, weight in influences:
                obj.vertex_groups[group_index].add([vertex_index], weight, "REPLACE")
        obj.data.update()
    return {"maximum_influences": maximum_influences,
            "vertices_modified": modified_vertices,
            "influences_removed": removed_influences}


def evaluated_genital_landmarks(body: bpy.types.Object) -> tuple[Vector, Vector, Vector, dict[str, int]]:
    """Return on-surface landmarks from the skinned, normalized rest pose."""
    material_index = next((index for index, material in enumerate(body.data.materials)
                           if material is not None and material.name.split(".")[0] == "Radagon_Penis"), None)
    if material_index is None:
        raise RuntimeError("Cannot measure genital landmarks without the Radagon_Penis material region.")
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        faces = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
        genital_vertices = {vertex for polygon in faces for vertex in polygon.vertices}
        other_vertices = {vertex for polygon in mesh.polygons if polygon.material_index != material_index
                          for vertex in polygon.vertices}
        seam_vertices = genital_vertices & other_vertices
        if len(faces) < 100 or len(seam_vertices) < 3:
            raise RuntimeError("The skinned genital region has no reliable shared seam with the body mesh.")
        points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        seam_center = sum((points[index] for index in seam_vertices), Vector()) / len(seam_vertices)
        base_index = min(seam_vertices, key=lambda index: (points[index] - seam_center).length_squared)
        base = points[base_index].copy()
        forward = (body.matrix_world.to_3x3() @ Vector((0.0, -1.0, 0.0))).normalized()
        tip_index = max(genital_vertices, key=lambda index: (points[index] - base).dot(forward))
        tip = points[tip_index].copy()
        base_local_y = mesh.vertices[base_index].co.y
        proximal = [index for index in genital_vertices if mesh.vertices[index].co.y >= base_local_y - 4.0]
        proximal_center = sum((points[index] for index in proximal), Vector()) / max(1, len(proximal))
        testicular_index = min(proximal, key=lambda index: (points[index] - proximal_center).length_squared)
        testicular = points[testicular_index].copy()
        return base, tip, testicular, {
            "faces": len(faces),
            "seam_vertices": len(seam_vertices),
            "landmark_vertices": 3,
        }
    finally:
        evaluated.to_mesh_clear()


def add_anatomy_anchors(armature: bpy.types.Object, body: bpy.types.Object
                        ) -> dict[str, tuple[str, Vector]]:
    """Place reproductive anchors at the adapted mesh's measured base and tip."""
    base, tip, testicular, _ = evaluated_genital_landmarks(body)
    result = add_anchors(armature)
    # Replace guessed pelvis offsets with measured, bone-parented positions.
    anatomy = {
        "anchor_genital": ("pelvis", base),
        "anchor_penile": ("spline_06.x", tip),
        "anchor_urethral": ("spline_06.x", tip),
        "anchor_testicular": ("pelvis", testicular),
    }
    for name, (bone_name, position) in anatomy.items():
        matches = [obj for obj in bpy.context.scene.objects if obj.get("anchor_id") == name]
        if len(matches) != 1:
            raise RuntimeError(f"Expected exactly one initial {name} anchor, found {len(matches)}.")
        bpy.data.objects.remove(matches[0], do_unlink=True)
        add_bone_anchor(armature, name, bone_name, position)
        result[name] = (bone_name, position)
    return result


def glb_json(path: Path) -> dict:
    data = path.read_bytes()
    if data[:4] != b"glTF":
        raise RuntimeError("Blender did not write a valid GLB header.")
    total = struct.unpack_from("<I", data, 8)[0]
    cursor = 12
    while cursor < total:
        length, kind = struct.unpack_from("<II", data, cursor)
        chunk = data[cursor + 8:cursor + 8 + length]
        if kind == 0x4E4F534A:
            return json.loads(chunk.decode("utf-8"))
        cursor += 8 + length
    raise RuntimeError("GLB has no JSON chunk.")


def verify_embedded_texture_sizes(path: Path, doc: dict) -> dict[str, list[int]]:
    data = path.read_bytes()
    cursor = 12
    binary_offset = None
    while cursor < len(data):
        length, kind = struct.unpack_from("<II", data, cursor)
        if kind == 0x004E4942:
            binary_offset = cursor + 8
            break
        cursor += 8 + length
    if binary_offset is None:
        raise RuntimeError("GLB has no binary chunk for its embedded textures.")

    def category_for(name: str) -> str:
        lowered = name.lower()
        if "eyelash" in lowered:
            return "eyelashes"
        if "eyes" in lowered:
            return "eyes"
        if "hair" in lowered:
            return "hair"
        if "clothes" in lowered:
            return "clothes"
        if "head" in lowered:
            return "head"
        if "body" in lowered or "penis" in lowered:
            return "body"
        raise RuntimeError(f"No runtime texture limit is defined for embedded image {name}.")

    dimensions: dict[str, list[int]] = {}
    for image in doc.get("images", []):
        name = image.get("name", "unnamed image")
        view_index = image.get("bufferView")
        if view_index is None:
            raise RuntimeError(f"GLB image {name} is not embedded in the binary asset.")
        view = doc["bufferViews"][view_index]
        offset = binary_offset + view.get("byteOffset", 0)
        if data[offset:offset + 8] != b"\x89PNG\r\n\x1a\n":
            raise RuntimeError(f"GLB image {name} is not an embedded PNG as expected.")
        width, height = struct.unpack_from(">II", data, offset + 16)
        category = category_for(name)
        limit = TEXTURE_SIZES[category]
        if max(width, height) > limit:
            raise RuntimeError(
                f"Embedded texture {name} exceeds its {category} runtime limit: {width}x{height} > {limit}.")
        dimensions[name] = [width, height]
    return dimensions


def verify_export(path: Path, expected_anchors: dict[str, tuple[str, Vector]]) -> dict:
    doc = glb_json(path)
    texture_dimensions = verify_embedded_texture_sizes(path, doc)
    nodes = doc.get("nodes", [])
    names = [node.get("name", "") for node in nodes]
    for name in REQUIRED_BONES:
        if names.count(name) != 1:
            raise RuntimeError(f"Export must contain one canonical bone node {name}; found {names.count(name)}.")
    parents = {}
    for index, node in enumerate(nodes):
        for child in node.get("children", []):
            parents[child] = index
    node_transforms = []
    for node in nodes:
        if "matrix" in node:
            values = node["matrix"]
            node_transforms.append(Matrix(tuple(tuple(values[column * 4 + row] for column in range(4))
                                                for row in range(4))) )
        else:
            translation = Vector(node.get("translation", (0.0, 0.0, 0.0)))
            rotation = node.get("rotation", (0.0, 0.0, 0.0, 1.0))
            scale = node.get("scale", (1.0, 1.0, 1.0))
            quaternion = Quaternion((rotation[3], rotation[0], rotation[1], rotation[2]))
            node_transforms.append(Matrix.Translation(translation)
                                   @ quaternion.to_matrix().to_4x4()
                                   @ Matrix.Diagonal((*scale, 1.0)))

    world_transforms = [None] * len(nodes)
    def world_transform(index: int) -> Matrix:
        cached = world_transforms[index]
        if cached is not None:
            return cached
        parent = parents.get(index)
        result = node_transforms[index] if parent is None else world_transform(parent) @ node_transforms[index]
        world_transforms[index] = result
        return result

    world_positions = {name: world_transform(index).translation.copy()
                       for index, name in enumerate(names)}
    for anchor_name, (bone_name, expected_blender_position) in expected_anchors.items():
        matches = [i for i, name in enumerate(names) if name == anchor_name]
        if len(matches) != 1 or parents.get(matches[0]) is None or names[parents[matches[0]]] != bone_name:
            raise RuntimeError(f"Exported anchor {anchor_name} is missing or not parented to {bone_name}.")
        # Blender Z-up coordinates export as glTF Y-up: (x, y, z) -> (x, z, -y).
        expected_gltf_position = Vector((expected_blender_position.x,
                                         expected_blender_position.z,
                                         -expected_blender_position.y))
        actual = world_positions[anchor_name]
        if (actual - expected_gltf_position).length > 0.015:
            raise RuntimeError(
                f"Exported anchor {anchor_name} is misplaced: expected {expected_gltf_position}, got {actual}.")
    materials = doc.get("materials", [])
    anatomy_material_indices = {index for index, item in enumerate(materials)
                                if item.get("name") == "Radagon_Penis"}
    used_material_indices = {primitive.get("material")
                             for mesh in doc.get("meshes", [])
                             for primitive in mesh.get("primitives", [])}
    if not anatomy_material_indices or not (anatomy_material_indices & used_material_indices):
        raise RuntimeError("The exported GLB does not render the preserved Radagon_Penis region.")
    return {"nodes": len(nodes), "skins": len(doc.get("skins", [])),
            "animations": [item.get("name", "") for item in doc.get("animations", [])],
            "anchors": sorted(expected_anchors),
            "embedded_texture_dimensions": texture_dimensions,
            "anchor_world_positions_gltf": {name: list(world_positions[name]) for name in sorted(expected_anchors)}}


def verify_exported_anatomy(path: Path, expected_anchors: dict[str, tuple[str, Vector]]) -> dict:
    """Reimport the GLB and verify that anatomy anchors still meet the exported mesh."""
    existing = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = set(bpy.context.scene.objects) - existing
    anatomy_meshes = [obj for obj in imported if obj.type == "MESH"
                      and any(material is not None and material.name.split(".")[0] == "Radagon_Penis"
                              for material in obj.data.materials)]
    if len(anatomy_meshes) != 1:
        raise RuntimeError(f"Expected one reimported anatomy mesh; found {len(anatomy_meshes)}.")
    body = anatomy_meshes[0]
    material_index = next(index for index, item in enumerate(body.data.materials)
                          if item is not None and item.name.split(".")[0] == "Radagon_Penis")
    faces = [polygon for polygon in body.data.polygons if polygon.material_index == material_index]
    genital_vertices = {vertex for polygon in faces for vertex in polygon.vertices}
    if len(faces) < 100 or not genital_vertices:
        raise RuntimeError("The exported GLB lost its genital geometry region.")
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_body = body.evaluated_get(depsgraph)
    evaluated_mesh = evaluated_body.to_mesh()
    try:
        world_vertices = [evaluated_body.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
        base_anchor = next((obj for obj in imported if obj.name.split(".")[0] == "anchor_genital"), None)
        tip_anchor = next((obj for obj in imported if obj.name.split(".")[0] == "anchor_penile"), None)
        if base_anchor is None or tip_anchor is None:
            raise RuntimeError("The exported GLB is missing its genital or penile anchor node.")
        base_anchor_world = base_anchor.matrix_world.translation.copy()
        tip_anchor_world = tip_anchor.matrix_world.translation.copy()
        base_index = min(genital_vertices, key=lambda index: (world_vertices[index] - base_anchor_world).length_squared)
        exported_base = world_vertices[base_index]
        # The imported model faces Blender -Y; the anatomically distal endpoint
        # is the most forward point of this region after skin deformation.
        tip_index = min(genital_vertices, key=lambda index: world_vertices[index].y)
        exported_tip = world_vertices[tip_index]
        base_error = (exported_base - base_anchor_world).length
        tip_error = (exported_tip - tip_anchor_world).length
        if base_error > 0.02 or tip_error > 0.02:
            raise RuntimeError(
                f"Exported anatomy anchors do not meet the mesh: base {base_error:.4f} m, tip {tip_error:.4f} m.")
        return {
            "material": "Radagon_Penis",
            "face_count": len(faces),
            "genital_base_world_blender_m": [round(value, 5) for value in exported_base],
            "penile_tip_world_blender_m": [round(value, 5) for value in exported_tip],
            "anchor_base_error_m": round(base_error, 6),
            "anchor_tip_error_m": round(tip_error, 6),
        }
    finally:
        evaluated_body.to_mesh_clear()


def main() -> None:
    args = parse_args()
    source = args.source_fbx.expanduser().resolve()
    source_blend = args.source_blend.expanduser().resolve()
    texture_dir = args.texture_dir.expanduser().resolve()
    if not source.is_file() or not source_blend.is_file():
        raise FileNotFoundError(source if not source.is_file() else source_blend)
    args.output_glb.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_blend.parent.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    source_blend_hash = hashlib.sha256(source_blend.read_bytes()).hexdigest()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("The source FBX has no armature.")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name != "Cube"]
    if not meshes:
        raise RuntimeError("The source FBX has no character meshes.")
    for obj in [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name == "Cube"]:
        bpy.data.objects.remove(obj, do_unlink=True)

    body = next((obj for obj in meshes if obj.name == "Radagon Body"), None)
    if body is None:
        raise RuntimeError("The FullClothes FBX does not contain Radagon Body.")
    source_topology = {
        obj.name: {"vertices": len(obj.data.vertices), "faces": len(obj.data.polygons)}
        for obj in meshes
    }
    verify_source_skeleton(armature)

    minimum, maximum = bounds(meshes)
    source_height = maximum.z - minimum.z
    if not 1.0 <= source_height <= 3.0:
        raise RuntimeError(f"Unexpected imported character height {source_height:.3f} m.")
    factor = TARGET_HEIGHT_M / source_height
    genital_pose = pose_genital_region_down(body, factor)
    skin_weight_report = limit_skin_weights(meshes)
    armature.scale *= factor
    armature.location.z += -minimum.z * factor
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for obj in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()
    minimum, maximum = bounds(meshes)
    measured_height = maximum.z - minimum.z
    if abs(measured_height - TARGET_HEIGHT_M) > 0.002 or abs(minimum.z) > 0.002:
        raise RuntimeError(f"Normalized bounds are invalid: min={minimum}, max={maximum}.")

    cracked_skin_images = load_cracked_skin_images(source_blend)
    replace_materials(texture_dir, meshes, cracked_skin_images)
    texture_dimensions = normalize_material_images()
    for obj in meshes:
        obj["asset_id"] = ASSET_ID

    anchor_bones = add_anatomy_anchors(armature, body)

    bpy.context.scene.render.fps = 30
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 49
    add_humanoid_locomotion_actions(armature)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output_blend.expanduser().resolve()))

    bpy.ops.object.select_all(action="DESELECT")
    for obj in [armature, *meshes, *(obj for obj in bpy.context.scene.objects if obj.get("semantic_anchor"))]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(args.output_glb.expanduser().resolve()),
        export_format="GLB",
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_skins=True,
        export_apply=True,
        export_image_format="AUTO",
    )
    verified = verify_export(args.output_glb, anchor_bones)
    runtime_glb_bytes = args.output_glb.stat().st_size
    if runtime_glb_bytes >= MAX_RUNTIME_GLB_BYTES:
        raise RuntimeError(
            f"Runtime GLB is {runtime_glb_bytes} bytes; it must stay below the {MAX_RUNTIME_GLB_BYTES}-byte GitHub file limit.")
    verified["exported_genital_geometry"] = verify_exported_anatomy(args.output_glb, anchor_bones)
    output_geometry = {
        obj.name: {"vertices": len(obj.data.vertices), "faces": len(obj.data.polygons)}
        for obj in meshes
    }
    if source_topology != output_geometry:
        raise RuntimeError("The Radagon source mesh topology changed during import adaptation.")
    triangles = sum(sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons) for obj in meshes)
    metadata = {
        "asset_id": ASSET_ID,
        "display_name": "Radagon Male Human NPC",
        "source_asset": "Radagon FullClothes FBX",
        "source_creator": "Ryan Reos",
        "source_url": "https://ryanreos.artstation.com/store/1W91v/blender-model-radagon-of-the-golden-order",
        "source_sha256": source_hash,
        "source_blend_sha256": source_blend_hash,
        "owner_use_confirmation": "User confirmed this acquired asset is valid for use in the public game and this project.",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "target_height_m": TARGET_HEIGHT_M,
        "measured_height_m": round(measured_height, 5),
        "floor_z_m": round(minimum.z, 5),
        "source_rig": "Original Radagon FBX deform rig preserved",
        "project_bone_map": PROJECT_BONE_MAP,
        "canonical_pose": "Original Radagon FBX rest pose; whole-asset scale is normalized and the retained genital surface is angled down and slightly tucked behind the clothing silhouette",
        "bone_count": len(armature.data.bones),
        "required_bones_verified": sorted(REQUIRED_BONES),
        "mesh_count": len(meshes),
        "vertex_count": sum(len(obj.data.vertices) for obj in meshes),
        "triangle_count": triangles,
        "source_topology": source_topology,
        "genital_pose": genital_pose,
        "skin_weight_limit": skin_weight_report,
        "exported_genital_geometry": verified["exported_genital_geometry"],
        "texture_max_dimensions": TEXTURE_SIZES,
        "texture_dimensions": texture_dimensions,
        "runtime_glb_bytes": runtime_glb_bytes,
        "animation_clips": verified["animations"],
        "locomotion": {
            "controller": "NPC simulation movement; clips are in-place",
            "clips": {"idle": "human_idle", "walk": "human_walk", "run": "human_run"},
            "frame_rate": 30,
            "root_motion": False,
        },
        "interaction_anchors": {
            "locomotion": ["anchor_root", "anchor_foot_l", "anchor_foot_r"],
            "equipment": ["anchor_hand_l", "anchor_hand_r", "anchor_carry"],
            "interaction": ["anchor_mouth", "anchor_anus", "anchor_genital", "anchor_urethral", "anchor_navel", "anchor_penile", "anchor_testicular", "anchor_breast_l", "anchor_breast_r", "anchor_hand_l", "anchor_hand_r"],
            "breeding": ["anchor_genital", "anchor_urethral", "anchor_penile", "anchor_testicular", "anchor_breast_l", "anchor_breast_r"],
        },
        "anchor_nodes": verified["anchors"],
        "runtime_path": str(args.output_glb.expanduser().resolve().relative_to(PROJECT_ROOT)).replace("\\", "/"),
        "output_blend": str(args.output_blend.expanduser().resolve().relative_to(PROJECT_ROOT)).replace("\\", "/"),
        "notes": [
            "Uses the FullClothes source; the separate nude FBX and hammer are excluded.",
            "Source mesh topology, clothing, and skeleton hierarchy are preserved; only the FBX Cube importer artifact is excluded. Vertex positions in the genital material region are adjusted for the requested down-and-tucked pose. Skin weights are reduced to the four strongest influences per vertex to match the glTF skin limit.",
            "The genital mesh and its source-specific skin material are retained. The same down-and-tuck adjustment is applied to the Basis and all 52 facial/body shape keys.",
            "Genital anchors are positioned from the adapted skinned mesh and verified against the reimported exported GLB geometry.",
            "Source materials are translated to glTF PBR using the included color, normal, roughness, opacity, cracked-skin, and genital maps.",
            "Runtime texture maps are resampled to the declared per-material limits and repacked before export; original source maps remain unchanged.",
            "Godot semantic anchors are non-deforming child nodes; source bone names remain unchanged.",
            "Includes project-authored, in-place human idle, walk, and run actions; simulation/controller movement remains authoritative.",
            "Simulation anatomy stays separate from the render skeleton; NPC sex comes from saved EntityRecord.Sex.",
            "The source asset is approved by the owner for use in this public game project.",
        ],
    }
    args.output_json.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print("RADAGON_ADAPTATION=" + json.dumps(metadata, ensure_ascii=False))


if __name__ == "__main__":
    main()
