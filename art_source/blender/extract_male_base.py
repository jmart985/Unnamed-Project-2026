"""Extract the bare male source directly from Blender Human Base Meshes.

Run Blender with --background --factory-startup --disable-autoexec and pass
--source <human_base_meshes_bundle.blend> after the script's -- separator.
This is a source checkpoint, not a clothing or player-rig generator.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art_source/reference/blender_base"
NAMES = (
    "GEO-body_male_realistic",
    "GEO-body_male_realistic.eye.L",
    "GEO-body_male_realistic.eye.R",
)


def extract(source):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
        missing = set(NAMES) - set(available.objects)
        if missing:
            raise RuntimeError(f"The original Blender bundle is missing {sorted(missing)}.")
        loaded.objects = list(NAMES)
    objects = list(loaded.objects)
    for obj in objects:
        bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()
    body = objects[0]
    original = [body.matrix_world @ v.co for v in body.data.vertices]
    low = Vector(tuple(min(p[i] for p in original) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in original) for i in range(3)))
    scale = 1.75 / (high.z - low.z)
    center = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    report = {
        "source_file": source.name,
        "source_file_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "source_body": NAMES[0],
        "purpose": "Bare source checkpoint before rigging or clothing",
        "normalization": {"uniform_scale": scale, "source_center": list(center),
                          "height_m": 1.75, "floor_m": 0.0},
        "objects": [],
    }
    expected = {}
    for obj in objects:
        transform = obj.matrix_world.copy()
        mesh = obj.data
        # Keep the source cage and Multires displacement untouched. Uniform
        # object normalization scales both together, preserving authored detail.
        obj.parent = None
        obj.matrix_world = Matrix.Translation(-center * scale) @ Matrix.Scale(scale, 4) @ transform
        report["objects"].append({"name": obj.name, "vertices": len(mesh.vertices),
                                  "faces": len(mesh.polygons),
                                  "modifiers": [{"type": m.type,
                                                 "levels": getattr(m, "levels", None),
                                                 "render_levels": getattr(m, "render_levels", None),
                                                 "total_levels": getattr(m, "total_levels", None)}
                                                for m in obj.modifiers]})
    if any(obj.type != "MESH" for obj in bpy.context.scene.objects):
        raise RuntimeError("The bare source scene contains an unexpected non-mesh object.")
    if any(mod.type != "MULTIRES" for obj in objects for mod in obj.modifiers):
        raise RuntimeError(f"Unexpected source modifiers: {report['objects']}")
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    for obj, entry in zip(objects, report["objects"]):
        evaluated = obj.evaluated_get(graph)
        mesh = evaluated.to_mesh()
        expected[obj.name] = [evaluated.matrix_world @ v.co for v in mesh.vertices]
        entry["evaluated_vertices"] = len(mesh.vertices)
        entry["evaluated_faces"] = len(mesh.polygons)
        evaluated.to_mesh_clear()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    bpy.context.scene["source_asset"] = "Blender Human Base Meshes v1.4.1 / " + NAMES[0]
    bpy.context.scene["asset_stage"] = "Bare source; no clothing, hair, rig, or animation"
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT / "male_base.blend"), compress=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = body
    bpy.ops.export_scene.gltf(filepath=str(OUTPUT / "male_base.glb"),
                             export_format="GLB", use_selection=True,
                             export_animations=False, export_skins=False,
                             export_apply=True, export_yup=True)
    return expected, report


def validate_and_render(expected, report):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT / "male_base.glb"))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if {obj.name for obj in meshes} != set(NAMES) or len(meshes) != 3:
        raise RuntimeError("Bare GLB must contain only the source body and two eyes.")
    if any(obj.type == "ARMATURE" for obj in bpy.context.scene.objects) or bpy.data.actions:
        raise RuntimeError("Bare GLB unexpectedly contains a rig or animation.")
    maximum_error = 0.0
    for obj in meshes:
        tree = KDTree(len(expected[obj.name]))
        for i, point in enumerate(expected[obj.name]):
            tree.insert(point, i)
        tree.balance()
        found = set()
        for vertex in obj.data.vertices:
            _, index, error = tree.find(obj.matrix_world @ vertex.co)
            maximum_error = max(maximum_error, error)
            found.add(index)
        if len(found) != len(expected[obj.name]):
            raise RuntimeError(f"GLB lost source vertices on {obj.name}.")
    if maximum_error > 0.00001:
        raise RuntimeError(f"GLB changed source shape by {maximum_error:.8f} m.")
    report["glb_round_trip"] = {"status": "PASS", "max_position_error_m": maximum_error,
                               "mesh_count": 3, "armatures": 0, "animations": 0}
    (OUTPUT / "male_base.json").write_text(json.dumps(report, indent=2) + "\n",
                                           encoding="utf-8", newline="\n")

    # Render the reimported GLB. These temporary studio objects and clay
    # materials are never saved back into the bare .blend or GLB.
    for obj in meshes:
        mat = bpy.data.materials.new("Preview clay")
        mat.diffuse_color = (0.48, 0.48, 0.48, 1) if obj.name == NAMES[0] else (0.16, 0.16, 0.16, 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = mat.diffuse_color
        shader.inputs["Roughness"].default_value = 0.72
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT" if bpy.app.version < (5, 0, 0) else "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("Preview world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.12, 1)
    for location, energy, size in (((-3, -4, 5), 600, 4), ((3, -1, 3), 300, 3), ((1, 4, 4), 500, 3)):
        data = bpy.data.lights.new("Preview light", "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new("Preview light", data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0, 0, .9)) - light.location).to_track_quat("-Z", "Y").to_euler()
    data = bpy.data.cameras.new("Preview camera")
    camera = bpy.data.objects.new("Preview camera", data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type, data.ortho_scale = "ORTHO", 1.98
    for view, location in (("front", (0, -5, .875)), ("side", (5, 0, .875)), ("back", (0, 5, .875))):
        camera.location = location
        camera.rotation_euler = (Vector((0, 0, .875)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(OUTPUT / f"male_base_{view}.png")
        bpy.ops.render.render(write_still=True)
    print(f"BARE BLENDER MALE PASSED: three source meshes; no rig, clothing, hair, or animations; "
          f"GLB round-trip error {maximum_error:.8f} m.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    expected, report = extract(args.source.resolve(strict=True))
    validate_and_render(expected, report)
