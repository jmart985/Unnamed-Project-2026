"""Create the project-curated source subset for the Deluxe Bunny Suit.

Run with Blender 5.x:
  blender --background --factory-startup --python prepare_source_v1.py -- \
    --source-blend <path-to-DeluxeBunnySuit.blend> \
    --source-textures <path-to-extracted-TGA-textures>

The original archive is never modified. The curated blend retains only the
selected garment meshes and the source body used to measure garment fit.
Textures are converted to portable project PNGs at bounded resolutions.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


ASSET_ROOT = Path(__file__).resolve().parent
SOURCE_OBJECTS = {
    "Primrose_Body_FullNude": "SourceBodyReference",
    "DeluxeBunnySuit_Jacket": "SourceJacket",
    "DeluxeBunnySuit_Collar": "SourceCollar",
    "DeluxeBunnySuit_Gloves": "SourceCuffs",
    "DeluxeBunnySuit_Vest": "SourceVest",
    "DeluxeBunnySuit_Leotard": "SourceLeotard",
    "DeluxeBunnySuit_Stockings": "SourceStockings",
    "DeluxeBunnySuit_BunnyEars": "SourceBunnyEars",
}
TEXTURES = {
    "DeluxeBunnySuit_BaseColor": ("DeluxeBunnySuit_BaseColor.tga", 2048, "sRGB"),
    "DeluxeBunnySuit_Normal": ("DeluxeBunnySuit_Normal.tga", 2048, "Non-Color"),
    "DeluxeBunnySuit_Metallic": ("DeluxeBunnySuit_Metallic.tga", 1024, "Non-Color"),
    "DeluxeBunnySuit_Roughness": ("DeluxeBunnySuit_Roughness.tga", 1024, "Non-Color"),
    "DeluxeBunnySuit_AO": ("DeluxeBunnySuit_AO.tga", 1024, "Non-Color"),
    "DeluxeBunnySuit_Stockings_BaseColor.tga": (
        "DeluxeBunnySuit_Stockings_BaseColor.tga", 2048, "sRGB"
    ),
    "DeluxeBunnySuit_Stockings_Normal.tga": (
        "DeluxeBunnySuit_Stockings_Normal.tga", 2048, "Non-Color"
    ),
    "DeluxeBunnySuit_Stockings_Roughness.tga": (
        "DeluxeBunnySuit_Stockings_Roughness.tga", 1024, "Non-Color"
    ),
    "DeluxeBunnySuit_Stockings_AO.tga": (
        "DeluxeBunnySuit_Stockings_AO.tga", 1024, "Non-Color"
    ),
}


def arguments() -> argparse.Namespace:
    values = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-blend", required=True, type=Path)
    parser.add_argument("--source-textures", required=True, type=Path)
    return parser.parse_args(values)


def save_texture(source: Path, destination: Path, maximum_size: int, colorspace: str) -> None:
    image = bpy.data.images.load(str(source), check_existing=False)
    image.colorspace_settings.name = colorspace
    width, height = image.size
    if max(width, height) > maximum_size:
        ratio = maximum_size / max(width, height)
        image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    image.filepath = "//textures/" + destination.name
    image.name = destination.name
    print(f"Texture {destination.name}: {width}x{height} -> {tuple(image.size)}")


def main() -> None:
    args = arguments()
    if not args.source_blend.is_file():
        raise FileNotFoundError(args.source_blend)
    ASSET_ROOT.mkdir(parents=True, exist_ok=True)
    texture_output = ASSET_ROOT / "textures"
    texture_output.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.open_mainfile(filepath=str(args.source_blend))
    missing = set(SOURCE_OBJECTS) - set(bpy.data.objects.keys())
    if missing:
        raise RuntimeError(f"The source Blender scene is missing objects: {sorted(missing)}")

    for source_name in list(bpy.data.objects.keys()):
        if source_name not in SOURCE_OBJECTS:
            bpy.data.objects.remove(bpy.data.objects[source_name], do_unlink=True)
    for source_name, project_name in SOURCE_OBJECTS.items():
        obj = bpy.data.objects[source_name]
        obj.name = project_name
        if obj.type != "MESH":
            raise RuntimeError(f"Expected a mesh for {source_name}.")
        for group in list(obj.vertex_groups):
            obj.vertex_groups.remove(group)
        if project_name == "SourceBodyReference":
            for modifier in list(obj.modifiers):
                obj.modifiers.remove(modifier)
            for collection in list(obj.users_collection):
                collection.objects.unlink(obj)
            bpy.context.scene.collection.objects.link(obj)
            if obj.data.shape_keys is not None:
                bpy.context.view_layer.objects.active = obj
                obj.select_set(True)
                bpy.ops.object.shape_key_remove(all=True)
                obj.select_set(False)
            obj.data.materials.clear()
            obj.hide_render = True
            obj.hide_set(True)
        else:
            for modifier in list(obj.modifiers):
                if modifier.type == "ARMATURE":
                    obj.modifiers.remove(modifier)
            obj.hide_render = False
            obj.hide_set(False)

    image_bindings = []
    for material in bpy.data.materials:
        if material.users == 0:
            continue
        if not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.type != "TEX_IMAGE" or node.image is None:
                continue
            spec = TEXTURES.get(node.image.name)
            if spec is None:
                spec = TEXTURES.get(Path(node.image.name).stem)
            if spec is None:
                continue
            image_bindings.append((node, spec))

    # The source .blend contains image stubs with broken paths; remove those
    # datablocks after recording node-to-map links so a fresh disk load is used.
    for image in list(bpy.data.images):
        bpy.data.images.remove(image, do_unlink=True)
    converted_images = {}
    for _, (filename, maximum_size, colorspace) in image_bindings:
        if filename in converted_images:
            continue
        source = args.source_textures / filename
        if not source.is_file():
            raise FileNotFoundError(source)
        destination = texture_output / (Path(filename).stem + ".png")
        if not destination.is_file():
            save_texture(source, destination, maximum_size, colorspace)
        image = bpy.data.images.load(str(destination), check_existing=False)
        image.colorspace_settings.name = colorspace
        image.filepath = str(destination)
        converted_images[filename] = image
    for node, (filename, _, _) in image_bindings:
        node.image = converted_images[filename]

    keep_materials = {
        material for obj in bpy.data.objects.values() if obj.type == "MESH"
        for material in obj.data.materials if material is not None
    }
    for material in list(bpy.data.materials):
        if material not in keep_materials:
            bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)

    for obj in bpy.data.objects:
        obj.select_set(False)
    bpy.context.scene.render.resolution_x = 960
    bpy.context.scene.render.resolution_y = 960
    bpy.context.scene.render.resolution_percentage = 100
    output = ASSET_ROOT / "deluxe_bunny_suit_source_v1.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    for filename, image in converted_images.items():
        texture_name = Path(filename).stem + ".png"
        image.filepath = "//textures/" + texture_name
        image.filepath_raw = str(texture_output / texture_name)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print(f"Curated source written: {output}")
    print("Source meshes: " + ", ".join(SOURCE_OBJECTS.values()))


if __name__ == "__main__":
    main()
