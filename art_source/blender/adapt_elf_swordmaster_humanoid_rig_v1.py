"""Adapt the locally acquired Elf Swordmaster into a project Humanoid Rig v1 prototype.

The original purchase file stays outside the repository and is never saved by this
script. Invoke Blender with --disable-autoexec and pass --source when the source
file is not at ../ElfSwordmaster_Rigged.blend.

Example:
  blender --background --disable-autoexec --python \
    art_source/blender/adapt_elf_swordmaster_humanoid_rig_v1.py -- \
    --source "C:/path/ElfSwordmaster_Rigged.blend"
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ASSET_ID = "elf_swordmaster_humanoid_rig_v1"
RIG_NAME = "HumanoidRig_v1_ElfSwordmaster"
OUT_BLEND = PROJECT_ROOT / "art_source/reference/generated_humanoids/elf_swordmaster_humanoid_rig_v1.blend"
OUT_GLB = PROJECT_ROOT / "game/assets/humanoids/elf/elf_swordmaster_humanoid_rig_v1.glb"
OUT_JSON = PROJECT_ROOT / "game/assets/humanoids/elf/elf_swordmaster_humanoid_rig_v1.json"
OUT_PREVIEW_DIR = PROJECT_ROOT / "art_source/reference/generated_humanoids/elf_swordmaster_humanoid_rig_v1_previews"

REQUIRED_BONES = {
    "root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l", "toe_l", "thigh_r", "calf_r", "foot_r", "toe_r",
    *{f"{finger}_{segment}_{side}" for side in ("l", "r")
      for finger in ("thumb", "index", "middle", "ring", "pinky")
      for segment in ("01", "02", "03")},
}
CORE_BONES = {
    "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l", "toe_l", "thigh_r", "calf_r", "foot_r", "toe_r",
}
SIDES = {"L": "l", "R": "r"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path,
                        default=PROJECT_ROOT.parent / "ElfSwordmaster_Rigged.blend")
    parser.add_argument("--skip-previews", action="store_true")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def image_named(*needles: str) -> bpy.types.Image | None:
    lowered = tuple(n.lower() for n in needles)
    return next((image for image in bpy.data.images
                 if any(needle in image.name.lower() for needle in lowered)), None)


def linked_image(material: bpy.types.Material, socket_name: str) -> bpy.types.Image | None:
    if not material.use_nodes or not material.node_tree:
        return None
    output = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if not output:
        return None
    socket = output.inputs.get(socket_name)
    if not socket or not socket.is_linked:
        return None
    node = socket.links[0].from_node
    if node.type == "TEX_IMAGE":
        return node.image
    for input_socket in node.inputs:
        if input_socket.is_linked:
            linked = input_socket.links[0].from_node
            if linked.type == "TEX_IMAGE":
                return linked.image
    return None


def set_non_color(image: bpy.types.Image | None) -> None:
    if image:
        image.colorspace_settings.name = "Non-Color"


def new_pbr_material(source: bpy.types.Material) -> bpy.types.Material:
    key = source.name.lower()
    material = bpy.data.materials.new("UP_Elf_" + source.name.replace("ElfSwordmaster_", ""))
    material.use_nodes = True
    material.diffuse_color = source.diffuse_color
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    output.location = (620, 80)
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (360, 80)
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "UVChannel_1"
    uv.location = (-240, 120)

    def texture_node(image: bpy.types.Image, location: tuple[float, float]) -> bpy.types.ShaderNodeTexImage:
        node = tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        node.location = location
        tree.links.new(uv.outputs["UV"], node.inputs["Vector"])
        return node

    if "armor_arms" in key:
        base = image_named("Armor_Arms_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Armor_Arms_Normal")
        rough = image_named("Armor_Arms_Roughness")
        metal = image_named("Armor_Arms_Metallic")
    elif "armor_chest" in key:
        base = image_named("Armor_Chest_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Armor_Chest_Normal")
        rough = image_named("Armor_Arms_Roughness")
        metal = image_named("Armor_Chest_Metallic")
    elif "armor_legs" in key:
        base = image_named("Armor_Legs_Variaton_01", "Armor_Legs_BaseColor", "Armor_Legs") or linked_image(source, "Base Color")
        normal = image_named("Armor_Legs_Normal")
        rough = image_named("Armor_Legs_Roughness")
        metal = image_named("Armor_Legs_Metallic")
    elif "body" in key:
        base = image_named("Body_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Body_Normal")
        rough = image_named("Body_Roughness.tga")
        metal = None
    elif "face" in key:
        base = image_named("Face_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Face_Normal")
        rough = image_named("Face_Roughness.tga")
        metal = None
    elif "eye" in key:
        base = image_named("Eyes_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Eyes_Normal")
        rough = image_named("Eyes_Roughness")
        metal = None
    elif "hair" in key or "lash" in key:
        base = image_named("Hair_BaseColor") or linked_image(source, "Base Color")
        normal = image_named("Hair_Normal")
        rough = image_named("Hair_Roughness")
        metal = None
    else:
        base = linked_image(source, "Base Color")
        if not base and source.use_nodes and source.node_tree:
            base = next((n.image for n in source.node_tree.nodes
                         if n.type == "TEX_IMAGE" and n.image), None)
        normal = None
        rough = None
        metal = None

    if base:
        base.colorspace_settings.name = "sRGB"
        node = texture_node(base, (0, 220))
        tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
        if ("hair" in key or "lash" in key) and "Alpha" in shader.inputs:
            tree.links.new(node.outputs["Alpha"], shader.inputs["Alpha"])
            material.surface_render_method = "DITHERED"
    else:
        shader.inputs["Base Color"].default_value = source.diffuse_color

    if normal:
        set_non_color(normal)
        texture = texture_node(normal, (0, -40))
        normal_node = tree.nodes.new("ShaderNodeNormalMap")
        normal_node.location = (200, -40)
        tree.links.new(texture.outputs["Color"], normal_node.inputs["Color"])
        tree.links.new(normal_node.outputs["Normal"], shader.inputs["Normal"])
    if rough:
        set_non_color(rough)
        node = texture_node(rough, (0, -260))
        tree.links.new(node.outputs["Color"], shader.inputs["Roughness"])
    else:
        shader.inputs["Roughness"].default_value = 0.48 if "armor" in key else 0.72
    if metal:
        set_non_color(metal)
        node = texture_node(metal, (0, -460))
        tree.links.new(node.outputs["Color"], shader.inputs["Metallic"])
    elif "armor" in key:
        shader.inputs["Metallic"].default_value = 0.35
    elif "eye" in key:
        shader.inputs["Metallic"].default_value = 0.0
        shader.inputs["Roughness"].default_value = 0.22
    elif "body" in key or "face" in key:
        shader.inputs["Subsurface Weight"].default_value = 0.06

    material["source_material"] = source.name
    material["asset_id"] = ASSET_ID
    return material


def make_materials(meshes: list[bpy.types.Object]) -> None:
    converted: dict[str, bpy.types.Material] = {}
    for obj in meshes:
        slots = list(obj.data.materials)
        valid_source = next((source for source in slots if source is not None), None)
        if valid_source is None:
            continue
        if valid_source.name not in converted:
            converted[valid_source.name] = new_pbr_material(valid_source)
        fallback = converted[valid_source.name]
        for index, source in enumerate(slots):
            if source is None:
                obj.data.materials[index] = fallback
                continue
            if source.name not in converted:
                converted[source.name] = new_pbr_material(source)
            obj.data.materials[index] = converted[source.name]

    # Limit texture memory to the project's 1024 default; preserve eye detail at 512.
    used_images = {node.image for material in converted.values() if material.node_tree
                   for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image}
    for image in used_images:
        target = 512 if "eyes" in image.name.lower() else 1024
        factor = min(1.0, target / max(image.size[0], image.size[1]))
        if factor < 1.0:
            image.scale(max(1, round(image.size[0] * factor)), max(1, round(image.size[1] * factor)))
        # Repack after resizing. Otherwise Blender can retain the original
        # full-resolution TGA payload even though the in-memory image changed.
        image.pack()
    for image in list(bpy.data.images):
        if image not in used_images:
            bpy.data.images.remove(image, do_unlink=True)


def source_point(armature: bpy.types.Object, bone_name: str, endpoint: str) -> Vector:
    bone = armature.data.bones.get(bone_name)
    if bone is None:
        raise RuntimeError(f"Source rig is missing required reference bone {bone_name}.")
    return Vector(bone.head_local if endpoint == "head" else bone.tail_local)


def mean_point(*points: Vector) -> Vector:
    return sum(points, Vector()) / len(points)


def build_target_bones(source: bpy.types.Object) -> tuple[dict[str, tuple[Vector, Vector]], dict[str, str | None]]:
    bones: dict[str, tuple[Vector, Vector]] = {}
    parents: dict[str, str | None] = {}

    def define(name: str, head: Vector, tail: Vector, parent: str | None) -> None:
        if (tail - head).length < 0.005:
            tail = head + Vector((0.0, 0.0, 0.02))
        bones[name] = (head.copy(), tail.copy())
        parents[name] = parent

    root_head = Vector((0.0, 0.0, 0.0))
    define("root", root_head, root_head + Vector((0.0, 0.0, 0.1)), None)

    spine_sources = [f"DEF-spine{suffix}" for suffix in ("", ".001", ".002", ".003", ".004", ".005", ".006")]
    define("pelvis", source_point(source, spine_sources[0], "head"),
           source_point(source, spine_sources[0], "tail"), "root")
    define("spine_01", source_point(source, spine_sources[1], "head"),
           source_point(source, spine_sources[1], "tail"), "pelvis")
    define("spine_02", source_point(source, spine_sources[2], "head"),
           source_point(source, spine_sources[2], "tail"), "spine_01")
    define("spine_03", source_point(source, spine_sources[3], "head"),
           source_point(source, spine_sources[3], "tail"), "spine_02")
    define("neck_01", source_point(source, spine_sources[4], "head"),
           source_point(source, spine_sources[5], "tail"), "spine_03")
    define("head", source_point(source, spine_sources[6], "head"),
           source_point(source, spine_sources[6], "tail"), "neck_01")
    define("head_end", bones["head"][1], bones["head"][1] + Vector((0.0, 0.0, 0.035)), "head")

    for old_side, side in SIDES.items():
        upper = f"DEF-upper_arm.{old_side}"
        forearm = f"DEF-forearm.{old_side}"
        thigh = f"DEF-thigh.{old_side}"
        shin = f"DEF-shin.{old_side}"
        clav_head = Vector((0.0, 0.0, 1.44))
        arm_head = source_point(source, upper, "head")
        define(f"clavicle_{side}", clav_head, arm_head, "spine_03")
        define(f"upperarm_{side}", arm_head,
               source_point(source, f"{upper}.001", "tail"), f"clavicle_{side}")
        define(f"lowerarm_{side}", source_point(source, forearm, "head"),
               source_point(source, f"{forearm}.001", "tail"), f"upperarm_{side}")
        define(f"hand_{side}", source_point(source, f"DEF-hand.{old_side}", "head"),
               source_point(source, f"DEF-hand.{old_side}", "tail"), f"lowerarm_{side}")

        define(f"thigh_{side}", source_point(source, thigh, "head"),
               source_point(source, f"{thigh}.001", "tail"), "pelvis")
        define(f"calf_{side}", source_point(source, shin, "head"),
               source_point(source, f"{shin}.001", "tail"), f"thigh_{side}")
        define(f"foot_{side}", source_point(source, f"DEF-foot.{old_side}", "head"),
               source_point(source, f"DEF-foot.{old_side}", "tail"), f"calf_{side}")
        define(f"toe_{side}", source_point(source, f"DEF-toe.{old_side}", "head"),
               source_point(source, f"DEF-toe.{old_side}", "tail"), f"foot_{side}")
        end = bones[f"toe_{side}"][1]
        define(f"ball_{side}_end", end, end + Vector((0.0, -0.02, 0.0)), f"toe_{side}")

        for source_finger, target_finger in (("f_index", "index"), ("f_middle", "middle"),
                                             ("f_ring", "ring"), ("f_pinky", "pinky"),
                                             ("thumb", "thumb")):
            for segment in ("01", "02", "03"):
                source_name = f"DEF-{source_finger}.{segment}.{old_side}"
                target_name = f"{target_finger}_{segment}_{side}"
                define(target_name, source_point(source, source_name, "head"),
                       source_point(source, source_name, "tail"),
                       f"hand_{side}" if segment == "01" else f"{target_finger}_{int(segment)-1:02d}_{side}")
            end_name = f"{target_finger}_03_{side}_end"
            end_point = bones[f"{target_finger}_03_{side}"][1]
            direction = (end_point - bones[f"{target_finger}_02_{side}"][1]).normalized()
            define(end_name, end_point, end_point + direction * 0.015, f"{target_finger}_03_{side}")

        ear_src = f"DEF-ear.{old_side}"
        ear_tip = f"DEF-ear.{old_side}.001"
        define(f"ear_{side}", source_point(source, ear_src, "head"),
               source_point(source, ear_tip, "tail"), "head")

        breast = source.data.bones.get(f"DEF-breast.{old_side}")
        if breast:
            define(f"breast_{side}", Vector(breast.head_local), Vector(breast.tail_local), "spine_03")

    jaw = source.data.bones.get("DEF-jaw")
    if jaw:
        define("jaw", Vector(jaw.head_local), Vector(jaw.tail_local), "head")

    missing = REQUIRED_BONES - bones.keys()
    if missing:
        raise RuntimeError(f"Target rig is missing canonical bones: {sorted(missing)}")
    return bones, parents


def create_target_armature(name: str, bones: dict[str, tuple[Vector, Vector]],
                           parents: dict[str, str | None]) -> bpy.types.Object:
    data = bpy.data.armatures.new(name + "Data")
    armature = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = data.edit_bones
    for bone in list(edit_bones):
        edit_bones.remove(bone)
    for name, (head, tail) in bones.items():
        bone = edit_bones.new(name)
        bone.head = head
        bone.tail = tail
    for name, parent_name in parents.items():
        bone = edit_bones[name]
        bone.parent = edit_bones.get(parent_name) if parent_name else None
        if bone.parent and (bone.head - bone.parent.tail).length <= 0.001:
            bone.use_connect = True
    bpy.ops.object.mode_set(mode="OBJECT")
    armature["asset_id"] = ASSET_ID
    armature["humanoid_rig_version"] = 1
    armature["canonical_pose"] = "A-pose source; project Humanoid Rig v1 semantic hierarchy"
    armature["simulation_anatomy_separate"] = True
    return armature


def classify_side(group_name: str) -> str | None:
    upper = group_name.upper()
    if upper.endswith(".L") or upper.endswith(".L.001") or upper.endswith(".L.002") or upper.endswith(".L.003") or upper.endswith(".L.004"):
        return "l"
    if upper.endswith(".R") or upper.endswith(".R.001") or upper.endswith(".R.002") or upper.endswith(".R.003") or upper.endswith(".R.004"):
        return "r"
    for suffix, side in (("_L", "l"), ("_R", "r"), (".L", "l"), (".R", "r")):
        if upper.endswith(suffix):
            return side
    return None


def group_target(name: str, source: bpy.types.Object, bones: dict[str, tuple[Vector, Vector]]) -> str | None:
    lower = name.lower()
    direct = name.lower()
    side = classify_side(name)
    if direct in bones:
        return direct
    if lower in {"hide arms", "hide legs"}:
        return None
    if lower in {"eye_left", "eye_right", "head", "neck", "spine", "tongue"}:
        return "head" if lower != "spine" else "spine_03"
    if "hair" in lower or "lash" in lower or "brow" in lower or "lid" in lower or "forehead" in lower or "cheek" in lower or "nose" in lower or "lip" in lower or "chin" in lower or "tongue" in lower:
        return "head"
    # "forearm" contains the letters "ear"; remove that substring before
    # looking for an anatomical ear group so forearm weights stay on the arm.
    ear_name = lower.replace("forearm", "")
    if "ear" in ear_name or "earring" in lower:
        return f"ear_{side}" if side and f"ear_{side}" in bones else "head"
    if "breast" in lower:
        return f"breast_{side}" if side and f"breast_{side}" in bones else "spine_03"
    if "jaw" in lower:
        return "jaw" if "jaw" in bones else "head"
    if "upper_arm" in lower or "upperarm" in lower or "arm" in lower and "forearm" not in lower:
        return f"upperarm_{side}" if side else None
    if "forearm" in lower or "lower_arm" in lower or "lowerarm" in lower or "elbow" in lower:
        return f"lowerarm_{side}" if side else None
    if "shoulder" in lower or "clavicle" in lower:
        return f"clavicle_{side}" if side else None
    if "hand" in lower or "palm" in lower or "finger" in lower:
        return f"hand_{side}" if side else None
    for source_prefix, target_prefix in (("f_index", "index"), ("f_middle", "middle"),
                                        ("f_ring", "ring"), ("f_pinky", "pinky"), ("thumb", "thumb")):
        if source_prefix in lower and side:
            segment = next((s for s in ("01", "02", "03") if f".{s}." in name), "01")
            target = f"{target_prefix}_{segment}_{side}"
            if target in bones:
                return target
    if "thigh" in lower or "hip" in lower or "butt" in lower or "pelvis" in lower:
        return f"thigh_{side}" if side and ("thigh" in lower or "thighfix" in lower) else "pelvis"
    if "shin" in lower or "calf" in lower or "knee" in lower:
        return f"calf_{side}" if side else None
    if "foot" in lower:
        return f"foot_{side}" if side else None
    if "toe" in lower or "ball" in lower or "ankle" in lower:
        return f"toe_{side}" if side else None
    if "spine" in lower or "chest" in lower or "rib" in lower:
        if ".006" in lower or "head" in lower:
            return "head"
        if ".005" in lower or ".004" in lower or "neck" in lower:
            return "neck_01"
        if ".003" in lower:
            return "spine_03"
        if ".002" in lower:
            return "spine_02"
        if ".001" in lower:
            return "spine_01"
        return "pelvis"

    source_bone = source.data.bones.get(name)
    if source_bone:
        if side:
            candidates = [target for target in bones if target.endswith("_" + side) and target not in {
                f"ball_{side}_end", f"head_end", f"ear_{side}", f"breast_{side}"}]
            candidates += [f"breast_{side}", f"ear_{side}"]
        else:
            candidates = list(CORE_BONES) + ["jaw", "head", "pelvis", "spine_02", "spine_03"]
        point = (Vector(source_bone.head_local) + Vector(source_bone.tail_local)) * 0.5
        def segment_distance(target: str) -> float:
            head, tail = bones[target]
            direction = tail - head
            factor = max(0.0, min(1.0, (point - head).dot(direction) / max(direction.length_squared, 1e-12)))
            return (point - (head + factor * direction)).length
        candidates = [candidate for candidate in candidates if candidate in bones]
        if candidates:
            return min(candidates, key=segment_distance)
    return None


def nearest_bone(point: Vector, bones: dict[str, tuple[Vector, Vector]]) -> str:
    candidates = sorted(name for name in CORE_BONES if name in bones)
    def distance(name: str) -> float:
        head, tail = bones[name]
        direction = tail - head
        factor = max(0.0, min(1.0, (point - head).dot(direction) / max(direction.length_squared, 1e-12)))
        return (point - (head + factor * direction)).length
    return min(candidates, key=distance)


def reweight_mesh(obj: bpy.types.Object, old_armature: bpy.types.Object,
                  new_armature: bpy.types.Object, bones: dict[str, tuple[Vector, Vector]]) -> None:
    old_groups = list(obj.vertex_groups)
    mask_names = set()
    for modifier in obj.modifiers:
        if modifier.type == "MASK" and modifier.vertex_group:
            mask_names.add(modifier.vertex_group)
    mask_weights: dict[str, list[tuple[int, float]]] = {}
    influences: list[dict[str, float]] = [defaultdict(float) for _ in obj.data.vertices]
    groups_by_index = {group.index: group for group in old_groups}
    targets_by_index = {
        group.index: (None if group.name in mask_names else group_target(group.name, old_armature, bones))
        for group in old_groups
    }
    mask_values: dict[str, list[tuple[int, float]]] = defaultdict(list)
    for vertex in obj.data.vertices:
        for item in vertex.groups:
            group = groups_by_index[item.group]
            if group.name in mask_names:
                mask_values[group.name].append((vertex.index, item.weight))
                continue
            target = targets_by_index[group.index]
            if target is not None:
                influences[vertex.index][target] += item.weight
    mask_weights.update(mask_values)

    # Fill vertices affected only by non-deforming source groups (for example a
    # generic garment helper group) using their nearest canonical body region.
    inverse_armature = old_armature.matrix_world.inverted()
    for vertex in obj.data.vertices:
        mapped = influences[vertex.index]
        if not mapped:
            point = inverse_armature @ (obj.matrix_world @ vertex.co)
            mapped[nearest_bone(point, bones)] = 1.0
        sorted_weights = sorted(mapped.items(), key=lambda pair: pair[1], reverse=True)[:4]
        total = sum(weight for _, weight in sorted_weights)
        if total <= 1e-8:
            raise RuntimeError(f"No usable skin weight for {obj.name} vertex {vertex.index}.")
        influences[vertex.index] = {name: weight / total for name, weight in sorted_weights}

    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)
    needed = sorted({name for vertex_weights in influences for name in vertex_weights})
    groups = {name: obj.vertex_groups.new(name=name) for name in needed}
    for index, weights in enumerate(influences):
        for name, weight in weights.items():
            groups[name].add([index], weight, "REPLACE")
    for name, weights in mask_weights.items():
        group = obj.vertex_groups.new(name=name)
        for vertex_index, weight in weights:
            group.add([vertex_index], weight, "REPLACE")

    for modifier in list(obj.modifiers):
        if modifier.type == "ARMATURE":
            modifier.object = new_armature
        elif modifier.type in {"MASK", "CORRECTIVE_SMOOTH"}:
            # Retain body masks (they keep skin hidden under armor) but remove
            # source corrective helpers that refer to the control rig.
            if modifier.type == "CORRECTIVE_SMOOTH":
                obj.modifiers.remove(modifier)
        elif modifier.type not in {"SUBSURF", "MIRROR"}:
            obj.modifiers.remove(modifier)


def rename_meshes(meshes: list[bpy.types.Object]) -> None:
    renames = {
        "ElfSwordmaster_Body": "Body",
        "ElfSwordmaster_Eyeslash": "Lashes",
        "ElfSwordmaster_Hair": "Hair",
        "ElfSwordmaster_Eyes_L": "Eye_L",
        "ElfSwordmaster_Eyes_R": "Eye_R",
        "ElfSwordmaster_Mouth_Teeth.bottom": "Teeth_Lower",
        "ElfSwordmaster_Mouth_Teeth.top": "Teeth_Upper",
        "ElfSwordmaster_Mouth_Tounge": "Tongue",
        "ElfSwordmaster_Armor_Arms": "Armor_Arms",
        "ElfSwordmaster_Armor_Chest": "Armor_Chest",
        "ElfSwordmaster_Armor_Earings": "Armor_Earrings",
        "ElfSwordmaster_Armor_Hips": "Armor_Hips",
        "ElfSwordmaster_Armor_Hips_Cloth_Back": "Armor_Cloth_Back",
        "ElfSwordmaster_Armor_Hips_Cloth_Front": "Armor_Cloth_Front",
        "ElfSwordmaster_Armor_Panties": "Armor_Liner",
        "ElfSwordmaster_Armor_Pauldron.R": "Armor_Pauldron_R",
        "ElfSwordmaster_Armor_Pauldron.L": "Armor_Pauldron_L",
        "ElfSwordmaster_Armor_Elbows:R": "Armor_Elbow_R",
        "ElfSwordmaster_Armor_Elbows:L": "Armor_Elbow_L",
        "ElfSwordmaster_Armor_Knee.Top.L": "Armor_Knee_Top_L",
        "ElfSwordmaster_Armor_Knee.Top.R": "Armor_Knee_Top_R",
        "ElfSwordmaster_Armor_Knee.Bottom.L": "Armor_Knee_Bottom_L",
        "ElfSwordmaster_Armor_Knee.Bottom.R": "Armor_Knee_Bottom_R",
        "ElfSwordmaster_Armor_Legs.L": "Armor_Leg_L",
        "ElfSwordmaster_Armor_Legs.R": "Armor_Leg_R",
    }
    for obj in meshes:
        old_name = obj.name
        if old_name in renames:
            obj.name = renames[old_name]
            obj.data.name = obj.name + "Mesh"
        obj["asset_id"] = ASSET_ID
        obj["source_mesh"] = old_name


def parent_to_bone_preserving_world(obj: bpy.types.Object, armature: bpy.types.Object,
                                    bone: str, old_world: Matrix) -> None:
    obj.parent = None
    obj.matrix_world = old_world
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_world = old_world
    bpy.context.view_layer.update()


def add_anchors(armature: bpy.types.Object) -> dict[str, tuple[str, tuple[float, float, float] | None]]:
    # Values are in the normalized Blender model space. The model wears armor;
    # these are interaction locators, not visible anatomy or simulation state.
    positions = {
        "anchor_root": ("root", (0.0, 0.0, 0.0)),
        "anchor_foot_l": ("foot_l", (0.166, -0.06, 0.08)),
        "anchor_foot_r": ("foot_r", (-0.166, -0.06, 0.08)),
        "anchor_hand_l": ("hand_l", None),
        "anchor_hand_r": ("hand_r", None),
        "anchor_carry": ("pelvis", (0.0, -0.16, 0.98)),
        "anchor_mouth": ("head", (0.0, -0.10, 1.66)),
        "anchor_anus": ("pelvis", (0.0, 0.075, 0.88)),
        "anchor_genital": ("pelvis", (0.0, -0.08, 0.88)),
        "anchor_urethral": ("pelvis", (0.0, -0.10, 0.89)),
        "anchor_navel": ("spine_01", (0.0, -0.105, 1.10)),
        "anchor_vaginal": ("pelvis", (0.0, -0.09, 0.875)),
        "anchor_breast_l": ("breast_l", (0.085, -0.13, 1.34)),
        "anchor_breast_r": ("breast_r", (-0.085, -0.13, 1.34)),
    }
    for name, (bone_name, position) in positions.items():
        if armature.data.bones.get(bone_name) is None:
            raise RuntimeError(f"Semantic anchor {name} references missing bone {bone_name}.")
        obj = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(obj)
        obj.empty_display_type = "PLAIN_AXES"
        obj.empty_display_size = 0.025
        obj.parent = armature
        obj.parent_type = "BONE"
        obj.parent_bone = bone_name
        obj.matrix_parent_inverse = Matrix.Identity(4)
        bone = armature.data.bones[bone_name]
        tail_offset = Matrix.Translation(Vector((0.0, -bone.length, 0.0)))
        if position is None:
            obj.matrix_basis = tail_offset
        else:
            bone_world_rest = armature.matrix_world @ bone.matrix_local
            obj.matrix_basis = tail_offset @ bone_world_rest.inverted() @ Matrix.Translation(Vector(position))
        obj["semantic_anchor"] = True
        obj["anchor_id"] = name
    bpy.context.view_layer.update()
    return positions


def evaluated_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        points.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
    if not points:
        raise RuntimeError("The adapted character has no visible mesh geometry.")
    return (Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
            Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))))


def add_preview_rig(scene: bpy.types.Scene) -> list[bpy.types.Object]:
    created = []
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    world = scene.world or bpy.data.worlds.new("Elf Prototype Preview World")
    scene.world = world
    world.color = (0.055, 0.065, 0.08)
    for name, location, energy, size in (
        ("Preview_Key", (-2.8, -3.5, 3.1), 500, 3.0),
        ("Preview_Fill", (2.5, -2.0, 2.2), 280, 2.6),
        ("Preview_Rim", (0.0, 2.2, 2.5), 380, 2.0),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = location
        created.append(light)
    return created


def render_views(meshes: list[bpy.types.Object], preview_lighting: list[bpy.types.Object]) -> None:
    OUT_PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    target = Vector((0.0, 0.0, 0.88))
    views = {
        "front": Vector((0.0, -4.2, 1.03)),
        "three_quarter": Vector((2.7, -4.6, 1.4)),
        "side": Vector((4.2, 0.0, 1.03)),
        "back": Vector((0.0, 4.2, 1.03)),
    }
    for view_name, location in views.items():
        camera_data = bpy.data.cameras.new("PreviewCamera_" + view_name)
        camera_data.type = "ORTHO"
        camera_data.ortho_scale = 2.0
        camera = bpy.data.objects.new("PreviewCamera_" + view_name, camera_data)
        scene.collection.objects.link(camera)
        camera.location = location
        camera.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()
        scene.camera = camera
        scene.render.filepath = str(OUT_PREVIEW_DIR / f"elf_swordmaster_{view_name}.png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(camera, do_unlink=True)
    scene.camera = None
    for light in preview_lighting:
        bpy.data.objects.remove(light, do_unlink=True)


def export_gltf(armature: bpy.types.Object, meshes: list[bpy.types.Object], anchors: list[bpy.types.Object]) -> None:
    OUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    selected = [armature, *meshes, *anchors]
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB), export_format="GLB", use_selection=True,
        export_animations=False, export_skins=True, export_yup=True,
        export_apply=True, export_image_format="AUTO", export_materials="EXPORT",
        export_normals=True, export_tangents=True, export_texcoords=True,
    )


def glb_world_matrices(path: Path) -> tuple[dict[str, list[int]], dict[int, int], list[dict]]:
    blob = path.read_bytes()
    if blob[:4] != b"glTF":
        raise RuntimeError("Runtime export is not a binary glTF file.")
    length = struct.unpack_from("<I", blob, 12)[0]
    doc = json.loads(blob[20:20 + length].decode("utf-8"))
    nodes = doc.get("nodes", [])
    named: dict[str, list[int]] = defaultdict(list)
    parents: dict[int, int] = {}
    for index, node in enumerate(nodes):
        named[node.get("name", "")].append(index)
        for child in node.get("children", []):
            parents[child] = index

    def local_matrix(node: dict) -> Matrix:
        if "matrix" in node:
            values = node["matrix"]
            return Matrix(tuple(tuple(values[column * 4 + row] for column in range(4)) for row in range(4)))
        translation = Vector(node.get("translation", (0.0, 0.0, 0.0)))
        rotation = node.get("rotation", (0.0, 0.0, 0.0, 1.0))
        quaternion = Quaternion((rotation[3], rotation[0], rotation[1], rotation[2]))
        scale = node.get("scale", (1.0, 1.0, 1.0))
        return Matrix.Translation(translation) @ quaternion.to_matrix().to_4x4() @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))

    cache: dict[int, Matrix] = {}
    def world(index: int) -> Matrix:
        if index not in cache:
            parent = parents.get(index)
            cache[index] = (world(parent) @ local_matrix(nodes[index]) if parent is not None else local_matrix(nodes[index]))
        return cache[index]
    matrices = {name: [world(index) for index in indices] for name, indices in named.items()}
    return named, parents, (nodes, matrices)


def validate_limb_deformation(armature: bpy.types.Object, body: bpy.types.Object) -> dict[str, float]:
    """Pose each canonical limb bone and verify the skinned body responds.

    The source body's Hide arms/Hide legs masks intentionally hide skin under
    armor. Disable those masks for this probe so they cannot hide valid skin
    deformation from the check; restore their state before returning.
    """
    masks = [(modifier, modifier.show_viewport) for modifier in body.modifiers
             if modifier.type == "MASK"]
    for modifier, _ in masks:
        modifier.show_viewport = False

    def evaluated_world_vertices() -> list[Vector]:
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        try:
            return [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        finally:
            evaluated.to_mesh_clear()

    results: dict[str, float] = {}
    try:
        rest = evaluated_world_vertices()
        probe_bones = (
            "upperarm_l", "lowerarm_l", "hand_l", "thigh_l", "calf_l", "foot_l", "toe_l",
            "upperarm_r", "lowerarm_r", "hand_r", "thigh_r", "calf_r", "foot_r", "toe_r",
        )
        for name in probe_bones:
            pose_bone = armature.pose.bones[name]
            previous_mode = pose_bone.rotation_mode
            previous_basis = pose_bone.matrix_basis.copy()
            max_displacement = 0.0
            try:
                pose_bone.rotation_mode = "QUATERNION"
                for axis in (Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0))):
                    pose_bone.rotation_quaternion = Quaternion(axis, math.radians(25.0))
                    moved = evaluated_world_vertices()
                    if len(moved) != len(rest):
                        raise RuntimeError(f"Body topology changed while probing {name} deformation.")
                    max_displacement = max(
                        max_displacement,
                        max((before - after).length for before, after in zip(rest, moved)),
                    )
                    pose_bone.matrix_basis = previous_basis
                results[name] = max_displacement
                if max_displacement <= 0.001:
                    raise RuntimeError(f"Canonical bone {name} does not deform the body mesh.")
            finally:
                pose_bone.matrix_basis = previous_basis
                pose_bone.rotation_mode = previous_mode
                bpy.context.view_layer.update()
    finally:
        for modifier, was_visible in masks:
            modifier.show_viewport = was_visible
        bpy.context.view_layer.update()
    return results


def validate_export(armature: bpy.types.Object, meshes: list[bpy.types.Object], anchors: dict) -> dict:
    bone_names = {bone.name for bone in armature.data.bones}
    target_group_names = bone_names
    missing = REQUIRED_BONES - bone_names
    if missing:
        raise RuntimeError(f"Export rig lacks canonical bones: {sorted(missing)}")
    body_weight_totals: dict[str, int] = defaultdict(int)
    for obj in meshes:
        if any(mod.type == "ARMATURE" and mod.object != armature for mod in obj.modifiers):
            raise RuntimeError(f"{obj.name} still references a source armature.")
        for group in obj.vertex_groups:
            if group.name in {"Hide arms", "Hide legs"}:
                continue
            if group.name not in bone_names:
                raise RuntimeError(f"{obj.name} has a vertex group without a target bone: {group.name}.")
        group_names = {group.index: group.name for group in obj.vertex_groups}
        for vertex in obj.data.vertices:
            weighted = [group_names[item.group] for item in vertex.groups
                        if item.weight > 0.0 and group_names[item.group] in target_group_names]
            if obj.modifiers and any(mod.type == "ARMATURE" for mod in obj.modifiers) and not weighted:
                raise RuntimeError(f"{obj.name} vertex {vertex.index} has no target deform weight.")
            if len(weighted) > 4:
                raise RuntimeError(f"{obj.name} vertex {vertex.index} exceeds four influences.")
            if obj.name == "Body":
                for group_name in weighted:
                    body_weight_totals[group_name] += 1

    required_body_regions = {
        "upperarm_l", "lowerarm_l", "hand_l", "thigh_l", "calf_l", "foot_l", "toe_l",
        "upperarm_r", "lowerarm_r", "hand_r", "thigh_r", "calf_r", "foot_r", "toe_r",
    }
    missing_weight_regions = sorted(
        name for name in required_body_regions if body_weight_totals.get(name, 0) == 0
    )
    if missing_weight_regions:
        raise RuntimeError(f"Body lacks skin weights for canonical regions: {missing_weight_regions}.")
    body = next((obj for obj in meshes if obj.name == "Body"), None)
    if body is None:
        raise RuntimeError("The normalized humanoid body mesh is missing.")
    deformation_probe = validate_limb_deformation(armature, body)

    minimum, maximum = evaluated_bounds(meshes)
    height = maximum.z - minimum.z
    if not (1.65 <= height <= 1.85):
        raise RuntimeError(f"Character height is outside the 1.65-1.85 m humanoid range: {height:.3f}.")
    if minimum.z < -0.03 or minimum.z > 0.06:
        raise RuntimeError(f"Character feet do not sit near the project ground plane: {minimum.z:.3f} m.")

    named, parent_indices, glb_data = glb_world_matrices(OUT_GLB)
    nodes, matrices = glb_data
    def unique(name: str) -> tuple[int, Matrix]:
        indices = named.get(name, [])
        if len(indices) != 1:
            raise RuntimeError(f"Expected one GLB node {name!r}; found {len(indices)}.")
        return indices[0], matrices[name][0]
    for name, (bone_name, position) in anchors.items():
        index, world = unique(name)
        parent = parent_indices.get(index)
        if parent is None or nodes[parent].get("name") != bone_name:
            raise RuntimeError(f"Exported anchor {name} is not parented to {bone_name}.")
        if position is not None:
            # Blender XYZ -> glTF Y-up coordinates: (x, z, -y).
            expected = Vector((position[0], position[2], -position[1]))
            actual = world.translation
            if (actual - expected).length > 0.015:
                raise RuntimeError(f"Exported anchor {name} is misplaced by {(actual - expected).length:.3f} m.")
    for side in ("l", "r"):
        _, hand = unique(f"hand_{side}")
        _, anchor = unique(f"anchor_hand_{side}")
        if (hand.translation - anchor.translation).length > 0.01:
            raise RuntimeError(f"anchor_hand_{side} is not aligned to its exported hand bone.")
    if len(bone_names) < len(REQUIRED_BONES):
        raise RuntimeError("Canonical skeleton contains fewer bones than required.")

    return {
        "source_bounds_m": {"min": list(minimum), "max": list(maximum)},
        "normalized_height_m": round(height, 5),
        "floor_z_m": round(minimum.z, 5),
        "bone_count": len(bone_names),
        "required_bones_verified": True,
        "limb_deformation_verified": deformation_probe,
        "mesh_count": len(meshes),
        "vertex_count": sum(len(obj.data.vertices) for obj in meshes),
        "triangle_count": sum(sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons) for obj in meshes),
        "hand_anchor_alignment_verified": True,
        "anchor_count": len(anchors),
        "glb_bytes": OUT_GLB.stat().st_size,
    }


def main() -> None:
    args = parse_args()
    source_path = args.source.expanduser().resolve()
    if not source_path.is_file():
        raise FileNotFoundError(f"Source Blender file not found: {source_path}")
    source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)

    old_armature = bpy.data.objects.get("Elf_Swordmaster")
    if old_armature is None or old_armature.type != "ARMATURE":
        raise RuntimeError("Source armature Elf_Swordmaster was not found.")
    for pose_bone in old_armature.pose.bones:
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()

    remove_names = {
        "ElfSwordmaster_Weapon", "ElfSwordmaster_Armor_Chest.Exposed",
        "ElfSwordmaster_Armor_Hips_Exposed", "WGTS_rig", "Text.001",
    }
    for obj in list(bpy.data.objects):
        if obj.name in remove_names or (obj.type == "MESH" and obj.data and not len(obj.data.vertices)):
            bpy.data.objects.remove(obj, do_unlink=True)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"
              and obj.name.startswith("ElfSwordmaster_")
              and not obj.hide_render and not obj.hide_viewport and obj != old_armature]
    if not meshes:
        raise RuntimeError("No visible character meshes were found in the source scene.")

    bones, parents = build_target_bones(old_armature)
    new_armature = create_target_armature(RIG_NAME, bones, parents)

    # Save source parenting and world transforms before changing any object.
    old_worlds = {obj: obj.matrix_world.copy() for obj in meshes}
    rigid_parent_targets = {
        "MCH-eye.L": "head", "MCH-eye.R": "head", "ORG-teeth.B": "jaw", "ORG-teeth.T": "jaw",
        "Pauldron.L": "upperarm_l", "Pauldron.R": "upperarm_r",
        "elbowProtection.L": "lowerarm_l", "elbowProtection.R": "lowerarm_r",
    }
    old_bone_parents = {obj: obj.parent_bone for obj in meshes
                        if obj.parent == old_armature and obj.parent_type == "BONE"}
    # Weighted meshes are remapped onto the canonical semantic bone set.
    for obj in meshes:
        if obj in old_bone_parents:
            target = rigid_parent_targets.get(old_bone_parents[obj])
            if target is None or new_armature.data.bones.get(target) is None:
                raise RuntimeError(f"No canonical parent mapping for source bone {old_bone_parents[obj]!r} on {obj.name}.")
            for modifier in list(obj.modifiers):
                obj.modifiers.remove(modifier)
            for group in list(obj.vertex_groups):
                obj.vertex_groups.remove(group)
            parent_to_bone_preserving_world(obj, new_armature, target, old_worlds[obj])
            continue
        if len(obj.vertex_groups) > 0:
            reweight_mesh(obj, old_armature, new_armature, bones)
        world = old_worlds[obj]
        obj.parent = None
        obj.matrix_world = world
        obj.parent = new_armature
        obj.parent_type = "OBJECT"
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_world = world
        if any(group.name in {bone.name for bone in new_armature.data.bones}
               for group in obj.vertex_groups):
            modifier = next((mod for mod in obj.modifiers if mod.type == "ARMATURE"), None)
            if modifier is None:
                modifier = obj.modifiers.new("HumanoidRig_v1_Deform", "ARMATURE")
            modifier.object = new_armature
            modifier.use_vertex_groups = True
        obj["source_status"] = "Prototype Approved; derived from user-attested commercial source"

    # Remove source armature helpers and actions; only the project rig is exported.
    for obj in list(bpy.data.objects):
        if obj.type == "ARMATURE" and obj != new_armature:
            bpy.data.objects.remove(obj, do_unlink=True)
        elif obj.type not in {"MESH", "ARMATURE", "EMPTY"}:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    # Drop unused source control materials, packed textures, meshes, and rigs.
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures):
        for block in list(datablocks):
            if block.users == 0 and not getattr(block, "use_fake_user", False):
                datablocks.remove(block)

    rename_meshes(meshes)
    make_materials(meshes)
    # Reassert the main skin mod after group remapping and retain the body mask
    # groups used to avoid visible skin under the armor.
    for obj in meshes:
        armature_mods = [mod for mod in obj.modifiers if mod.type == "ARMATURE"]
        for mod in armature_mods:
            mod.object = new_armature
        for modifier in list(obj.modifiers):
            if modifier.type == "MASK" and obj.name != "Body":
                obj.modifiers.remove(modifier)
    anchors = add_anchors(new_armature)
    anchor_objects = [obj for obj in bpy.context.scene.objects if obj.type == "EMPTY" and obj.get("semantic_anchor")]

    minimum, maximum = evaluated_bounds(meshes)
    height = maximum.z - minimum.z
    # The source model is already near 1.75 m. Do not bake a per-instance scale;
    # preserve its proportions and record measured dimensions for species data.
    if not (1.65 <= height <= 1.85):
        raise RuntimeError(f"Source-derived humanoid is outside project scale: {height:.3f} m.")
    if minimum.z < -0.03 or minimum.z > 0.06:
        raise RuntimeError(f"Source-derived model does not rest on the floor: z={minimum.z:.3f} m.")

    if not args.skip_previews:
        lighting = add_preview_rig(bpy.context.scene)
        render_views(meshes, lighting)

    export_gltf(new_armature, meshes, anchor_objects)
    checks = validate_export(new_armature, meshes, anchors)
    mesh_materials = sorted({material.name for obj in meshes for material in obj.data.materials if material})
    mesh_records = [{"name": obj.name, "vertices": len(obj.data.vertices), "faces": len(obj.data.polygons)}
                    for obj in meshes]
    metadata = {
        "asset_id": ASSET_ID,
        "display_name": "Elf Swordmaster Humanoid Rig v1 Prototype",
        "status": "Prototype Approved",
        "source": {
            "file": source_path.name,
            "sha256": source_hash,
            "owner_attestation": "User states the purchased model is licensed for commercial use; complete receipts are not available.",
            "status": "Prototype Approved; retain owner attestation and source correspondence in the intake record.",
        },
        "runtime_format": "glb",
        "godot_units": "1 unit = 1 meter",
        "runtime_path": "game/assets/humanoids/elf/elf_swordmaster_humanoid_rig_v1.glb",
        "editable_source_path": "art_source/reference/generated_humanoids/elf_swordmaster_humanoid_rig_v1.blend",
        "humanoid_rig_version": 1,
        "target_height_m": 1.78,
        "canonical_pose": "relaxed A-pose derived from source rest pose",
        "forward_axis_blender": "-Y",
        "up_axis_blender": "Z",
        "simulation_anatomy_separate": True,
        "animations": [],
        "interaction_anchors": {
            "locomotion": ["anchor_root", "anchor_foot_l", "anchor_foot_r"],
            "equipment": ["anchor_hand_l", "anchor_hand_r", "anchor_carry"],
            "interaction": ["anchor_mouth", "anchor_anus", "anchor_genital", "anchor_urethral", "anchor_navel", "anchor_vaginal", "anchor_breast_l", "anchor_breast_r", "anchor_hand_l", "anchor_hand_r"],
            "breeding": ["anchor_genital", "anchor_urethral", "anchor_vaginal", "anchor_breast_l", "anchor_breast_r"],
        },
        "anchor_nodes": [
            {"id": name, "bone": bone, "rest_position_blender": list(position) if position is not None else None}
            for name, (bone, position) in anchors.items()
        ],
        "mesh_objects": mesh_records,
        "materials": mesh_materials,
        "technical_validation": checks,
        "runtime_approval": "Prototype only; no animation clip was transferred and the model is not connected to NPC spawning.",
        "limitations": [
            "No source animation clip was transferred; movement remains controller-driven and needs authored retargeted clips.",
            "Hair and facial helper weights are simplified onto the head; facial expression and secondary hair motion are not supported by this prototype.",
            "The source sword was excluded from the character scene so weapons can be attached through equipment sockets.",
            "Source license approval relies on the user's commercial-use attestation; keep this prototype out of public builds until the purchase terms are preserved in the asset record.",
        ],
    }
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    with OUT_JSON.open("w", encoding="utf-8", newline="\n") as output:
        output.write(json.dumps(metadata, indent=2) + "\n")
    OUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene["asset_id"] = ASSET_ID
    bpy.context.scene["source_sha256"] = source_hash
    bpy.context.scene["source_license_status"] = "Prototype Approved; user attests commercial-use permission"
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND), compress=True)
    print(json.dumps({"blend": str(OUT_BLEND), "glb": str(OUT_GLB), "metadata": str(OUT_JSON),
                      "previews": str(OUT_PREVIEW_DIR), "checks": checks}, indent=2))


if __name__ == "__main__":
    main()
