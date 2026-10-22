"""Adapt the reviewed elf humanoid into a neutral project-owned human prototype.

The source archive remains reference-only. This script deliberately removes source
materials, source costume identifiers, simulation modifiers, and elf ear silhouette
cues while retaining the reviewed mesh and Humanoid Rig v1 skeleton.
"""
from __future__ import annotations

import json
import os
import shutil
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT_ROOT / "art_source" / "reference" / "generated_humanoids" / "elf.blend"
REFERENCE_ROOT = PROJECT_ROOT / "art_source" / "reference" / "generated_humanoids"
RUNTIME_ROOT = PROJECT_ROOT / "game" / "assets" / "humanoids" / "human"
ASSET_ID = "human_elf_adapted_rig_v1"
RUNTIME_ID = "human_humanoid_rig_v1"
TARGET_HEIGHT = 1.75
REQUIRED_BONES = [
    "root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l", "toe_l",
    "thigh_r", "calf_r", "foot_r", "toe_r",
]
FINGER_BONES = [f"{finger}_{segment}_{side}" for side in ("l", "r")
                for finger in ("thumb", "index", "middle", "ring", "pinky")
                for segment in ("01", "02", "03")]


def make_material(name: str, color: tuple[float, float, float], roughness: float = 0.8) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return material


def assign_material(obj: bpy.types.Object, materials: list[bpy.types.Material]) -> None:
    obj.data.materials.clear()
    for material in materials:
        obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    return (
        Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points))),
        Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points))),
    )


def remove_source_identifiers() -> None:
    # The cloak carries the most recognizable source-character silhouette and is
    # not needed for a neutral human base. Keep the body, hair, shirt, skirt and
    # boots as editable derived geometry.
    for name in ("Cloak.002",):
        obj = bpy.data.objects.get(name)
        if obj:
            bpy.data.objects.remove(obj, do_unlink=True)

    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    armature.name = "HumanoidRig_v1_Human"
    armature.data.name = "HumanoidRig_v1_HumanData"

    rename = {
        "AvatarBodyLibContainer.001": "HumanBody",
        "HairTails": "HumanHair",
        "Eyes": "HumanEyes",
        "Shirt": "HumanShirt",
        "Skirt": "HumanLowerClothing",
        "Boot": "HumanBoots",
        "Lash": "HumanLashes",
        "Brow": "HumanBrows",
    }
    for old, new in rename.items():
        obj = bpy.data.objects.get(old)
        if obj:
            obj.name = new
            obj.data.name = new + "Mesh"

    skin = make_material("Human_Skin", (0.58, 0.30, 0.19))
    hair = make_material("Human_Hair", (0.07, 0.025, 0.012))
    eye = make_material("Human_Eye", (0.08, 0.16, 0.12), 0.25)
    clothing = make_material("Human_Clothing", (0.12, 0.24, 0.38))
    lower = make_material("Human_LowerClothing", (0.10, 0.12, 0.17))
    boot = make_material("Human_Boots", (0.10, 0.055, 0.025))
    for name, materials in {
        "HumanBody": [skin],
        "HumanHair": [hair],
        "HumanEyes": [eye],
        "HumanShirt": [clothing],
        "HumanLowerClothing": [lower],
        "HumanBoots": [boot],
        "HumanLashes": [hair],
        "HumanBrows": [hair],
    }.items():
        obj = bpy.data.objects.get(name)
        if obj:
            assign_material(obj, materials)
            # Source cloth/soft-body modifiers are presentation helpers, not
            # stable runtime behavior. Keep only the canonical armature skin.
            for modifier in list(obj.modifiers):
                if modifier.type != "ARMATURE":
                    obj.modifiers.remove(modifier)

    # Remove pointed-ear silhouette from the head while retaining the reviewed
    # body topology. The source body coordinates are already in world space
    # relative to the armature; this only clamps lateral head vertices.
    body = bpy.data.objects.get("HumanBody")
    if body:
        inverse = body.matrix_world.inverted()
        flattened_ears = 0
        for vertex in body.data.vertices:
            world = body.matrix_world @ vertex.co
            # In the source body the pointed ear triangles sit lateral to the
            # head, just below the eye line, and on the rear-facing side.
            # Shoulder vertices occupy a different depth band and remain
            # untouched.
            if 0.16 < world.z < 0.27 and world.y > -0.02 and abs(world.x) > 0.12:
                world.x = 0.05 if world.x > 0 else -0.05
                world.z = 0.22
                vertex.co = inverse @ world
                flattened_ears += 1
            elif 0.32 < world.z < 0.46 and world.y > 0.08 and abs(world.x) > 0.13:
                world.x = 0.02 if world.x > 0 else -0.02
                vertex.co = inverse @ world
                flattened_ears += 1
        # Remove the remaining narrow source-ear faces. Their centers occupy a
        # small lateral band; excluding the wider shoulder/upper-arm geometry
        # keeps the neutral human torso intact.
        mesh = body.data
        bm = bmesh.new()
        bm.from_mesh(mesh)
        ear_faces = []
        for face in bm.faces:
            world = body.matrix_world @ face.calc_center_median()
            if 0.16 < world.z < 0.27 and world.y > -0.02 and 0.12 < abs(world.x) < 0.30:
                ear_faces.append(face)
        if ear_faces:
            bmesh.ops.delete(bm, geom=ear_faces, context="FACES")
            bm.to_mesh(mesh)
        bm.free()
        print(f"Flattened {flattened_ears} pointed-ear candidate vertices")

    # Make the source-derived data clearly project-owned and remove dependency
    # on the source character's naming.
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj["asset_role"] = "derived_human_mesh"
            obj["simulation_anatomy_separate"] = True
    armature["asset_id"] = ASSET_ID
    armature["humanoid_rig_version"] = "1"
    armature["canonical_pose"] = "A-pose-compatible source pose"
    armature["source_status"] = "Reference Only; derived prototype"
    armature["license_status"] = "Unknown; verify source rights before production"
    armature["simulation_anatomy_separate"] = True


def normalize_scale() -> tuple[float, float, float]:
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    before_min, before_max = world_bounds(meshes)
    source_height = before_max.z - before_min.z
    factor = TARGET_HEIGHT / source_height
    armature.scale = Vector(armature.scale) * factor
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    armature.select_set(False)

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    floor_min, _ = world_bounds(meshes)
    floor_offset = -floor_min.z
    # The mesh objects are parented to the armature with parent-inverse
    # transforms. Move and apply the armature object so the complete skinned
    # character reaches the floor without translating vertices twice.
    armature.location.z += floor_offset
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    armature.select_set(False)
    bpy.context.view_layer.update()
    after_min, after_max = world_bounds(meshes)
    return factor, after_min.z, after_max.z


def rename_animation() -> list[str]:
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    names: list[str] = []
    for action in bpy.data.actions:
        action.name = "human_idle"
        action.use_fake_user = True
        action.use_cyclic = True
        names.append(action.name)
        break
    if not names:
        # Keep the asset valid for runtime while making the missing animation
        # explicit in metadata. The procedural gait remains the fallback.
        names = []
    return names


def purge_unused_source_data() -> None:
    for datablocks in (bpy.data.materials, bpy.data.images):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def add_preview() -> None:
    bpy.ops.object.camera_add(location=(2.9, -4.6, 1.5))
    camera = bpy.context.object
    camera.name = "HumanPreviewCamera"
    camera.rotation_euler = (Vector((0, -0.02, 0.82)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 58
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.4, 3.4))
    key = bpy.context.object
    key.name = "HumanPreviewKey"
    key.data.energy = 800
    key.data.shape = "DISK"
    key.data.size = 3.5
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.5, 1.6))
    fill = bpy.context.object
    fill.name = "HumanPreviewFill"
    fill.data.energy = 300
    fill.data.size = 3
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("HumanPreviewWorld") if scene.world is None else scene.world
    scene.world.color = (0.035, 0.035, 0.045)


def export_runtime() -> dict:
    REFERENCE_ROOT.mkdir(parents=True, exist_ok=True)
    RUNTIME_ROOT.mkdir(parents=True, exist_ok=True)
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    minimum, maximum = world_bounds(meshes)
    bone_names = [bone.name for bone in armature.data.bones]
    animation_clips = rename_animation()

    blend_path = REFERENCE_ROOT / f"{ASSET_ID}.blend"
    glb_path = REFERENCE_ROOT / f"{ASSET_ID}.glb"
    json_path = REFERENCE_ROOT / f"{ASSET_ID}.json"
    runtime_glb = RUNTIME_ROOT / f"{RUNTIME_ID}.glb"
    runtime_json = RUNTIME_ROOT / f"{RUNTIME_ID}.json"

    # Save an editable project-derived blend before adding preview-only camera
    # and lights to the export selection.
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    for obj in bpy.context.scene.objects:
        obj.select_set(obj.type in {"ARMATURE", "MESH"})
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_skins=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=True,
        export_apply=True,
    )
    shutil.copy2(glb_path, runtime_glb)

    metadata = {
        "asset_id": ASSET_ID,
        "runtime_asset_id": RUNTIME_ID,
        "display_name": "Human / Elf-derived Humanoid Rig v1 Prototype",
        "status": "Prototype Approved",
        "source_asset": "art_source/reference/generated_humanoids/elf.blend",
        "source_archive": "art_source/downloaded/humanoids/elf.zip",
        "license": "Unknown; source remains Reference Only",
        "creator": "Unnamed Project 2026 derived adaptation",
        "format": "GLB",
        "godot_units": "1 unit = 1 meter",
        "target_height_m": TARGET_HEIGHT,
        "post_bounds_min": [round(value, 6) for value in minimum],
        "post_bounds_max": [round(value, 6) for value in maximum],
        "post_height_m": round(maximum.z - minimum.z, 6),
        "orientation": "Godot -Z forward, +Y up after import normalization",
        "humanoid_rig_version": "1",
        "canonical_pose": "A-pose-compatible source pose",
        "mesh_count": len(meshes),
        "armature_bone_count": len(bone_names),
        "armature_scale": [round(value, 6) for value in armature.scale],
        "required_bones_verified": all(name in bone_names for name in REQUIRED_BONES),
        "finger_bones_verified": all(name in bone_names for name in FINGER_BONES),
        "animation_clips": animation_clips,
        "runtime_path": f"game/assets/humanoids/human/{RUNTIME_ID}.glb",
        "runtime_approval": "Prototype only; do not ship until source license is verified",
        "fallback": "EntityView procedural human body if GLB import is unavailable",
        "notes": [
            "Derived from the reviewed elf model; source archive is not copied into the runtime tree.",
            "Source materials and costume-specific cloak were removed; project-owned neutral materials are used.",
            "Head lateral vertices were clamped to remove the pointed-ear silhouette.",
            "PlayerCharacter controller replacement remains out of scope for this slice.",
            "Simulation anatomy remains independent from visual skeleton names.",
        ],
        "output_blend": str(blend_path.relative_to(PROJECT_ROOT)).replace(os.sep, "/"),
        "output_glb": str(glb_path.relative_to(PROJECT_ROOT)).replace(os.sep, "/"),
    }
    metadata_text = json.dumps(metadata, indent=2) + "\n"
    json_path.write_text(metadata_text, encoding="utf-8")
    runtime_json.write_text(metadata_text, encoding="utf-8")

    add_preview()
    bpy.context.scene.render.filepath = str(REFERENCE_ROOT / f"{ASSET_ID}_preview.png")
    bpy.ops.render.render(write_still=True)
    return metadata


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    remove_source_identifiers()
    factor, _, _ = normalize_scale()
    purge_unused_source_data()
    metadata = export_runtime()
    print(json.dumps({"scale_factor": factor, "metadata": metadata}, indent=2))


if __name__ == "__main__":
    main()









