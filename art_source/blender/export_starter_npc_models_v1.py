"""Export the ten curated starter NPC source models as normalized Godot GLBs."""
from __future__ import annotations

import argparse
import json
import math
import re
import struct
import sys
import tempfile
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from humanoid_locomotion_v1 import (
    OPTIONAL_LOCOMOTION_BONES,
    REQUIRED_LOCOMOTION_BONES,
    add_humanoid_locomotion_actions,
)


EXPECTED_ACTIONS = {"human_idle", "human_walk", "human_run"}
MAX_TEXTURE_EDGE = 1024
FACIAL_ACTION_FRAME_END = 30
FACIAL_EXPRESSION_ALTERNATIVES: dict[str, list[list[tuple[str, float]]]] = {
    "happy": [
        [(r"^eJCMSmile(?:Open)?FullFace(?:_HD_div2)?$", 0.8)],
        [(r"^Mout_Smile$", 0.75)],
        [(r"^Mouth_Soft_Smile$", 0.75)],
        [(r"^Mouth_Smile(?:_Open|_open)?$", 0.75)],
        [(r"^facs(?:_bs)?_MouthSmileLeft(?:_div2)?$", 0.65),
         (r"^facs(?:_bs)?_MouthSmileRight(?:_div2)?$", 0.65)],
        [(r"^smile_l$", 0.65), (r"^smile_r$", 0.65)],
    ],
    "sad": [
        [(r"^eJCMSad(?:_HD_div2)?$", 0.8)],
        [(r"^eJCMFrown(?:_HD_div2)?$", 0.75)],
        [(r"^sad_l$", 0.7), (r"^sad_r$", 0.7)],
        [(r"^facs(?:_bs)?_MouthFrownLeft(?:_div2)?$", 0.65),
         (r"^facs(?:_bs)?_MouthFrownRight(?:_div2)?$", 0.65)],
    ],
    "angry": [
        [(r"^eJCMAngry(?:_HD_div2)?$", 0.8)],
        [(r"^facs(?:_bs)?_BrowDownLeft(?:_div2)?$", 0.7),
         (r"^facs(?:_bs)?_BrowDownRight(?:_div2)?$", 0.7)],
        [(r"^brow_inner_down_l$", 0.7), (r"^brow_inner_down_r$", 0.7)],
        [(r"^facs(?:_bs)?_BrowSqueezeLeft(?:_div2)?$", 0.65),
         (r"^facs(?:_bs)?_BrowSqueezeRight(?:_div2)?$", 0.65)],
    ],
    "surprised": [
        [(r"^eJCMSurprise(?:_HD_div2)?$", 0.8)],
        [(r"^Eyebrows_Surprised$", 0.75)],
        [(r"^facs(?:_(?:bs|cbs))?_?EyesWideLeft(?:_div2)?$", 0.7),
         (r"^facs(?:_(?:bs|cbs))?_?EyesWideRight(?:_div2)?$", 0.7)],
        [(r"^facs(?:_bs)?_BrowOuterUpLeft(?:_div2)?$", 0.65),
         (r"^facs(?:_bs)?_BrowOuterUpRight(?:_div2)?$", 0.65)],
        [(r"^brow_outer_up_l$", 0.65), (r"^brow_outer_up_r$", 0.65)],
    ],
    "fear": [
        [(r"^eJCMFear(?:_HD_div2)?$", 0.8)],
        [(r"^facs(?:_(?:bs|cbs))?_?EyesWideLeft(?:_div2)?$", 0.65),
         (r"^facs(?:_(?:bs|cbs))?_?EyesWideRight(?:_div2)?$", 0.65),
         (r"^facs(?:_bs)?_BrowInnerUpLeft(?:_div2)?$", 0.5),
         (r"^facs(?:_bs)?_BrowInnerUpRight(?:_div2)?$", 0.5)],
        [(r"^facs(?:_bs)?_BrowInnerUpLeft(?:_div2)?$", 0.55),
         (r"^facs(?:_bs)?_BrowInnerUpRight(?:_div2)?$", 0.55)],
        [(r"^brow_inner_up_l$", 0.55), (r"^brow_inner_up_r$", 0.55)],
    ],
    "blink": [
        [(r"^facs(?:_(?:bs|cbs))?_?EyeBlinkLeft(?:_div2)?$", 1.0),
         (r"^facs(?:_(?:bs|cbs))?_?EyeBlinkRight(?:_div2)?$", 1.0)],
        [(r"^facs(?:_(?:bs|cbs))?_?EyelidCloseUpperLeft(?:_div2)?$", 0.9),
         (r"^facs(?:_(?:bs|cbs))?_?EyelidCloseUpperRight(?:_div2)?$", 0.9)],
        [(r"^lid_blink_l$", 1.0), (r"^lid_blink_r$", 1.0)],
    ],
}
FACIAL_SOURCE_ACTIONS: dict[str, list[str]] = {
    "happy": [r"^Mouth_Soft_Smile$", r"^Mout_Smile$", r"^Mouth.*Smile"],
    "sad": [r"^Eyebrows Sad$", r"^Eyebrows_Frown$", r"sad|frown"],
    "angry": [r"^Eyebrows_Angry$", r"angry"],
    "surprised": [r"^Eyebrows_Surprised$", r"surpris"],
    "fear": [r"fear"],
    "blink": [r"^Eyelids_Closed$", r"^rig_blink_top$", r"blink|eyelid.*close"],
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True,
                        help="Curated source model root, containing each roster slug folder.")
    parser.add_argument("--output-root", type=Path, required=True,
                        help="Godot runtime humanoid asset directory.")
    parser.add_argument("--roster", type=Path,
                        default=Path(__file__).with_name("starter_npc_model_roster_v1.json"))
    parser.add_argument("--model", help="Optional roster slug for a focused export.")
    parser.add_argument(
        "--preserve-base-from",
        type=Path,
        help="Optional directory of existing runtime GLBs whose rest meshes, node transforms, and skin binds must be preserved.",
    )
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def _texture_index(root: Path) -> dict[str, list[Path]]:
    index: dict[str, list[Path]] = {}
    for path in root.rglob("*"):
        if path.is_file() and path.suffix.lower() in {".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp"}:
            index.setdefault(path.name.casefold(), []).append(path)
    for paths in index.values():
        paths.sort(key=lambda path: (len(path.parts), str(path).casefold()))
    return index


def _material_images(objects: list[bpy.types.Object]) -> list[bpy.types.Image]:
    images: dict[int, bpy.types.Image] = {}
    visited_trees: set[int] = set()

    def collect(tree: bpy.types.NodeTree | None) -> None:
        if tree is None or tree.as_pointer() in visited_trees:
            return
        visited_trees.add(tree.as_pointer())
        for node in tree.nodes:
            image = getattr(node, "image", None)
            if image is not None:
                images[image.as_pointer()] = image
            if node.type == "GROUP":
                collect(node.node_tree)

    for obj in objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if material is None or not material.use_nodes or material.node_tree is None:
                continue
            collect(material.node_tree)
    return list(images.values())


def _resolve_and_pack_textures(objects: list[bpy.types.Object], model_dir: Path,
                               global_index: dict[str, list[Path]],
                               texture_workspace: Path) -> list[str]:
    unresolved: list[str] = []
    texture_workspace.mkdir(parents=True, exist_ok=True)
    replacements: dict[int, bpy.types.Image] = {}
    for image_index, image in enumerate(_material_images(objects)):
        if image.source not in {"FILE", "TILED"}:
            continue
        try:
            # Packed source textures are common in Blender libraries. They still
            # need normalization; skipping them can create multi-hundred-MB GLBs.
            if image.packed_file is None:
                current = Path(bpy.path.abspath(image.filepath)) if image.filepath else None
                if image.source == "TILED":
                    pattern = str(current) if current is not None else ""
                    tile_numbers = [tile.number for tile in image.tiles]
                    tile_paths = [Path(pattern.replace("<UDIM>", str(number)))
                                  for number in tile_numbers]
                    if not tile_paths or not all(path.is_file() for path in tile_paths):
                        resolved_tiles: list[tuple[int, Path]] = []
                        texture_name = Path(image.filepath).name if image.filepath else ""
                        for number, tile_path in zip(tile_numbers, tile_paths):
                            if tile_path.is_file():
                                resolved_tiles.append((number, tile_path))
                                continue
                            tile_name = texture_name.replace("<UDIM>", str(number))
                            matches = [path for path in model_dir.rglob(tile_name) if path.is_file()]
                            if not matches:
                                matches = global_index.get(tile_name.casefold(), [])
                            if not matches:
                                unresolved.append(f"{image.name}: missing UDIM tile {number} ({image.filepath})")
                                break
                            resolved_tiles.append((number, matches[0]))
                        if len(resolved_tiles) != len(tile_numbers):
                            continue
                        first_number, first_path = resolved_tiles[0]
                        template_name = first_path.name.replace(str(first_number), "<UDIM>", 1)
                        resolved_pattern = first_path.with_name(template_name)
                        if not all(resolved_pattern.with_name(
                                resolved_pattern.name.replace("<UDIM>", str(number))).is_file()
                                   for number in tile_numbers):
                            unresolved.append(f"{image.name}: UDIM tiles do not share a source pattern")
                            continue
                        pattern = str(resolved_pattern)
                    image.filepath = pattern
                    image.reload()
                elif current is not None and current.is_file():
                    image.filepath = str(current)
                    image.reload()
                else:
                    local_matches = list(model_dir.rglob(Path(image.filepath).name)) if image.filepath else []
                    matches = [path for path in local_matches if path.is_file()]
                    if not matches:
                        matches = global_index.get(Path(image.filepath).name.casefold(), []) if image.filepath else []
                    if not matches:
                        unresolved.append(f"{image.name}: {image.filepath}")
                        continue
                    image.filepath = str(matches[0])
                    image.reload()
            width, height = image.size
            if max(width, height) > MAX_TEXTURE_EDGE:
                ratio = MAX_TEXTURE_EDGE / max(width, height)
                image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
                # Blender's glTF exporter reads the image filepath, not always
                # the resized in-memory pixels. Save a temporary PNG so the
                # runtime asset actually receives the normalized dimensions;
                # the user's original texture and .blend remain untouched.
                if image.packed_file is not None:
                    image.unpack(method="REMOVE")
                udim_suffix = "_<UDIM>" if image.source == "TILED" else ""
                source_name = str(image.get("npc_source_texture_name", image.name))
                safe_source_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", source_name)
                scaled_path = texture_workspace / (
                    f"texture_{image_index:03d}_{safe_source_name}{udim_suffix}.png")
                image.filepath_raw = str(scaled_path)
                image.file_format = "PNG"
                image.save()
                # Use a newly loaded datablock so the exporter cannot reuse the
                # original file or stale packed bytes from the source image.
                resized_image = bpy.data.images.load(str(scaled_path), check_existing=False)
                resized_image.name = source_name
                resized_image["npc_source_texture_name"] = source_name
                resized_image.colorspace_settings.name = image.colorspace_settings.name
                resized_image.alpha_mode = image.alpha_mode
                replacements[image.as_pointer()] = resized_image
            else:
                image.pack()
        except Exception as exception:
            unresolved.append(f"{image.name}: {image.filepath} ({exception})")

    if replacements:
        visited_trees: set[int] = set()

        def replace_images(tree: bpy.types.NodeTree | None) -> None:
            if tree is None or tree.as_pointer() in visited_trees:
                return
            visited_trees.add(tree.as_pointer())
            for node in tree.nodes:
                image = getattr(node, "image", None)
                if image is not None and image.as_pointer() in replacements:
                    node.image = replacements[image.as_pointer()]
                if node.type == "GROUP":
                    replace_images(node.node_tree)

        for obj in objects:
            for slot in obj.material_slots:
                material = slot.material
                if material is None or not material.use_nodes or material.node_tree is None:
                    continue
                replace_images(material.node_tree)
    return unresolved


def _source_actions_by_armature(
    nodes: list[bpy.types.Object],
) -> dict[str, list[tuple[bpy.types.Action, bpy.types.ActionSlot | None]]]:
    """Collect assigned actions and compatible library actions for exported rigs."""
    def has_channels(action: bpy.types.Action, slot: bpy.types.ActionSlot | None) -> bool:
        slots = [slot] if slot is not None else list(action.slots)
        for candidate in slots:
            for layer in action.layers:
                for strip in layer.strips:
                    channelbag = strip.channelbag(candidate)
                    if channelbag is not None and len(channelbag.fcurves) > 0:
                        return True
        return False

    actions_by_armature: dict[str, list[tuple[bpy.types.Action, bpy.types.ActionSlot | None]]] = {}
    armatures = [node for node in nodes if node.type == "ARMATURE"]
    for node in nodes:
        if node.type != "ARMATURE":
            continue
        animation_data = node.animation_data
        if animation_data is None:
            continue
        actions: list[tuple[bpy.types.Action, bpy.types.ActionSlot | None]] = []
        if animation_data.action is not None:
            actions.append((animation_data.action, animation_data.action_slot))
        for track in animation_data.nla_tracks:
            for strip in track.strips:
                if strip.action is not None:
                    actions.append((strip.action, getattr(strip, "action_slot", None)))
        actions_by_armature[node.name] = actions

    # Many Blender character packs keep their facial/pose actions as unassigned
    # library Actions instead of active actions or NLA strips. Match their bone
    # paths to the selected armature so the original content is retained.
    for action in bpy.data.actions:
        for slot in action.slots:
            if slot.target_id_type != "OBJECT":
                continue
            best_armature: bpy.types.Object | None = None
            best_score = 0
            paths: list[str] = []
            for layer in action.layers:
                for strip in layer.strips:
                    channelbag = strip.channelbag(slot)
                    if channelbag is not None:
                        paths.extend(curve.data_path for curve in channelbag.fcurves)
            animated_bones = {
                bone_name
                for path in paths
                for bone_name in re.findall(r'pose\.bones\["([^\"]+)"\]', path)
            }
            if not animated_bones:
                continue
            # Facial shape-key expressions are exported separately as the
            # compact npc_face_* clips. Retaining each source viseme would add
            # dozens of redundant clips, most of them targeting control bones
            # intentionally omitted from the runtime skeleton.
            facial_markers = (
                "brow", "eyebrow", "cheek", "eyelid", "eye_", "lid_", "lash",
                "ulash", "lip", "mouth", "mout_", "tong", "jaw", "nostril",
                "teeth", "facs", "blink",
            )
            if all(any(marker in bone_name.casefold() for marker in facial_markers)
                   for bone_name in animated_bones):
                continue
            for armature in armatures:
                score = len(animated_bones.intersection(armature.data.bones.keys()))
                if score > best_score:
                    best_armature = armature
                    best_score = score
            if best_armature is not None:
                actions_by_armature.setdefault(best_armature.name, []).append((action, slot))

    # Preserve source order while avoiding duplicate action-slot exports.
    for name, actions in list(actions_by_armature.items()):
        unique = {
            (action.as_pointer(), slot.handle if slot is not None else None): (action, slot)
            for action, slot in actions
            if has_channels(action, slot)
        }
        actions_by_armature[name] = list(unique.values())
    return actions_by_armature


def _put_action_on_track(armature: bpy.types.Object, action: bpy.types.Action,
                         track_name: str,
                         action_slot: bpy.types.ActionSlot | None = None) -> None:
    """Make one independently selectable glTF clip from a Blender action."""
    animation_data = armature.animation_data_create()
    track = animation_data.nla_tracks.new()
    track.name = track_name
    start = max(1, int(round(action.frame_range[0])))
    strip = track.strips.new(track_name, start, action)
    slot = action_slot or (action.slots[0] if action.slots else None)
    if slot is not None:
        strip.action_slot = slot
    strip.blend_type = "REPLACE"
    strip.extrapolation = "NOTHING"
    track.mute = False


def _prepare_animation_tracks(nodes: list[bpy.types.Object],
                              source_actions: dict[str, list[tuple[bpy.types.Action,
                                                                   bpy.types.ActionSlot | None]]],
                              generated_actions: list[str],
                              locomotion_armature: bpy.types.Object) -> set[str]:
    """Expose source and generated actions as separate named NLA export tracks."""
    source_names: set[str] = set()
    for node in nodes:
        if node.type != "ARMATURE":
            continue
        animation_data = node.animation_data_create()
        source_actions_for_node = source_actions.get(node.name, [])
        # Keep source tracks in Blender but mute them; export clean, single-clip
        # tracks below so stacked/hidden authoring tracks cannot affect samples.
        for track in animation_data.nla_tracks:
            track.mute = True
        for action, action_slot in source_actions_for_node:
            if action.name in EXPECTED_ACTIONS:
                continue
            source_names.add(action.name)
            track_name = action.name
            _put_action_on_track(node, action, track_name, action_slot)

    # The locomotion helper leaves idle as the active action. Move each cycle to
    # an explicit NLA track because rigs with separate hair/accessory armatures
    # do not receive Blender's single-armature implicit action export.
    for action_name in generated_actions:
        action = bpy.data.actions.get(action_name)
        if action is not None:
            _put_action_on_track(locomotion_armature, action, action_name)
    for node in nodes:
        if node.type == "ARMATURE" and node.animation_data is not None:
            node.animation_data.action = None
    return source_names


def _select_facial_targets(
    meshes: list[bpy.types.Object],
    alternatives: list[list[tuple[str, float]]],
) -> list[tuple[bpy.types.Object, bpy.types.ShapeKey, float]]:
    """Choose the first complete facial-key pattern supported by this model."""
    for alternative in alternatives:
        selected: list[tuple[bpy.types.Object, bpy.types.ShapeKey, float]] = []
        complete = True
        for pattern, weight in alternative:
            expression = re.compile(pattern, re.IGNORECASE)
            matches: list[tuple[bpy.types.Object, bpy.types.ShapeKey, float]] = []
            for mesh in meshes:
                shape_keys = mesh.data.shape_keys
                if shape_keys is None:
                    continue
                target = next((key for key in list(shape_keys.key_blocks)[1:]
                               if expression.fullmatch(key.name)), None)
                if target is not None:
                    matches.append((mesh, target, weight))
            if not matches:
                complete = False
                break
            selected.extend(matches)
        if complete:
            unique: dict[tuple[int, str], tuple[bpy.types.Object, bpy.types.ShapeKey, float]] = {}
            for mesh, key, weight in selected:
                unique[(mesh.as_pointer(), key.name)] = (mesh, key, weight)
            return list(unique.values())
    return []


def _capture_and_freeze_shape_key_values(
    meshes: list[bpy.types.Object],
) -> dict[int, dict[str, float]]:
    """Freeze source driver results so export keeps the source model's current appearance."""
    values: dict[int, dict[str, float]] = {}
    for mesh in meshes:
        shape_keys = mesh.data.shape_keys
        if shape_keys is None:
            continue
        values[shape_keys.as_pointer()] = {
            key.name: float(key.value) for key in list(shape_keys.key_blocks)[1:]
        }
        animation_data = shape_keys.animation_data
        if animation_data is not None:
            for driver in list(animation_data.drivers):
                animation_data.drivers.remove(driver)
        for key in list(shape_keys.key_blocks)[1:]:
            key.value = values[shape_keys.as_pointer()][key.name]
    return values


def _shape_key_coordinates(shape_keys: bpy.types.Key, values: dict[str, float]) -> list[Vector]:
    """Evaluate relative shape-key offsets in local mesh space without modifiers."""
    blocks = list(shape_keys.key_blocks)
    basis = blocks[0]
    coordinates = [vertex.co.copy() for vertex in basis.data]
    for key in blocks[1:]:
        weight = values.get(key.name, 0.0)
        if abs(weight) <= 1e-8:
            continue
        relative = key.relative_key or basis
        for index, vertex in enumerate(key.data):
            coordinates[index] += (vertex.co - relative.data[index].co) * weight
    return coordinates


def _add_facial_expression_actions(
    meshes: list[bpy.types.Object],
) -> tuple[dict[str, str], dict[str, list[dict[str, str]]], int, int]:
    """Preserve the runtime base mesh and create compact facial clips from source-key deltas."""
    baseline_values = _capture_and_freeze_shape_key_values(meshes)
    selected_by_expression: dict[str, list[tuple[bpy.types.Object, bpy.types.ShapeKey, float]]] = {}
    for emotion, alternatives in FACIAL_EXPRESSION_ALTERNATIVES.items():
        selected = _select_facial_targets(meshes, alternatives)
        if selected:
            selected_by_expression[emotion] = selected

    facial_clips: dict[str, str] = {}
    facial_targets: dict[str, list[dict[str, str]]] = {}
    mesh_data: dict[int, tuple[bpy.types.Object, bpy.types.Key, list[Vector], list[Vector]]] = {}
    for mesh in meshes:
        shape_keys = mesh.data.shape_keys
        if shape_keys is None or shape_keys.as_pointer() in mesh_data:
            continue
        pointer = shape_keys.as_pointer()
        source_baseline = _shape_key_coordinates(shape_keys, baseline_values[pointer])
        runtime_basis = [vertex.co.copy() for vertex in shape_keys.key_blocks[0].data]
        mesh_data[pointer] = (mesh, shape_keys, runtime_basis, source_baseline)

    selected_by_data: dict[int, dict[str, list[tuple[bpy.types.ShapeKey, float]]]] = {}
    for emotion, selected in selected_by_expression.items():
        for mesh, key, peak in selected:
            pointer = mesh.data.shape_keys.as_pointer()
            selected_by_data.setdefault(pointer, {}).setdefault(emotion, []).append((key, peak))

    expression_coordinates: dict[int, dict[str, list[Vector]]] = {}
    for pointer, expressions in selected_by_data.items():
        mesh, shape_keys, _, _ = mesh_data[pointer]
        for emotion, keys in expressions.items():
            values = dict(baseline_values[pointer])
            for key, peak in keys:
                values[key.name] = min(1.0, max(0.0, peak))
            coordinates = _shape_key_coordinates(shape_keys, values)
            expression_coordinates.setdefault(pointer, {})[emotion] = coordinates

    source_target_count = sum(len(values) for values in baseline_values.values())
    runtime_target_count = 0
    action_by_emotion: dict[str, bpy.types.Action] = {}
    for emotion in {emotion for expressions in expression_coordinates.values()
                    for emotion in expressions}:
        action_name = f"npc_face_{emotion}"
        while bpy.data.actions.get(action_name) is not None:
            action_name += "_generated"
        action_by_emotion[emotion] = bpy.data.actions.new(action_name)

    for pointer, (mesh, shape_keys, runtime_basis, source_baseline) in mesh_data.items():
        basis = shape_keys.key_blocks[0]
        for vertex, coordinate in zip(basis.data, runtime_basis):
            vertex.co = coordinate
        for key in list(shape_keys.key_blocks)[1:]:
            mesh.shape_key_remove(key)

        for emotion, coordinates in expression_coordinates.get(pointer, {}).items():
            action = action_by_emotion[emotion]
            name = action.name
            expression_key = mesh.shape_key_add(name=name, from_mix=False)
            expression_key.relative_key = basis
            for vertex, base_coordinate, source_baseline_coordinate, expression_coordinate in zip(
                    expression_key.data, runtime_basis, source_baseline, coordinates):
                vertex.co = base_coordinate + (expression_coordinate - source_baseline_coordinate)
            expression_key.value = 0.0

            animation_data = shape_keys.animation_data_create()
            for track in animation_data.nla_tracks:
                track.mute = True
            animation_data.action = action
            action_slot = animation_data.action_slot
            for frame, value in ((1, 0.0), (8, 1.0), (20, 1.0),
                                 (FACIAL_ACTION_FRAME_END, 0.0)):
                expression_key.value = value
                expression_key.keyframe_insert(data_path="value", frame=frame)
            expression_key.value = 0.0
            _put_action_on_track(shape_keys, action, name, action_slot)
            animation_data.action = None
            for track in animation_data.nla_tracks:
                track.mute = False

            facial_clips[emotion] = name
            facial_targets.setdefault(name, []).append({
                "mesh": mesh.name,
                "shape_key": expression_key.name,
            })
            runtime_target_count += 1

    return facial_clips, facial_targets, source_target_count, runtime_target_count


def _source_facial_clip_map(source_action_names: set[str]) -> dict[str, str]:
    """Map source-authored face clips to the project's stable expression labels."""
    result: dict[str, str] = {}
    names = sorted(source_action_names, key=str.casefold)
    for emotion, patterns in FACIAL_SOURCE_ACTIONS.items():
        for pattern in patterns:
            expression = re.compile(pattern, re.IGNORECASE)
            match = next((name for name in names if expression.search(name)), None)
            if match is not None:
                result[emotion] = match
                break
    return result


def _material_nodes(material: bpy.types.Material) -> list[bpy.types.Node]:
    result: list[bpy.types.Node] = []
    visited: set[int] = set()

    def collect(tree: bpy.types.NodeTree | None) -> None:
        if tree is None or tree.as_pointer() in visited:
            return
        visited.add(tree.as_pointer())
        for node in tree.nodes:
            result.append(node)
            if node.type == "GROUP":
                collect(node.node_tree)

    if material.use_nodes and material.node_tree is not None:
        collect(material.node_tree)
    return result


def _gltf_native_base_color(material: bpy.types.Material) -> bool:
    """Keep simple Principled colors and images; bake node graphs glTF cannot represent."""
    if not material.use_nodes or material.node_tree is None:
        return True
    output = next((node for node in material.node_tree.nodes
                   if node.type == "OUTPUT_MATERIAL" and node.is_active_output), None)
    surface = output.inputs.get("Surface") if output is not None else None
    if surface is None or len(surface.links) != 1:
        return False
    shader = surface.links[0].from_node
    if shader.type != "BSDF_PRINCIPLED":
        return False
    base_color = shader.inputs.get("Base Color")
    if base_color is None or not base_color.is_linked:
        return True
    return len(base_color.links) == 1 and base_color.links[0].from_node.type in {
        "TEX_IMAGE", "VERTEX_COLOR", "ATTRIBUTE"
    }


def _best_material_image(material: bpy.types.Material, kind: str,
                         source_images: list[bpy.types.Image] | None = None) -> bpy.types.Image | None:
    images = source_images or [node.image for node in _material_nodes(material)
                               if node.type == "TEX_IMAGE" and node.image is not None]
    if not images:
        return None

    def score(image: bpy.types.Image) -> tuple[int, str]:
        name = str(image.get("npc_source_texture_name", image.name)).casefold()
        non_color = image.colorspace_settings.name.casefold() in {"non-color", "non-colour"}
        if kind == "base":
            positive = ("basecolor", "base_color", "albedo", "diffuse", "color", "colour",
                        "_bc", "_d0", "_d_")
            negative = ("normal", "rough", "gloss", "metal", "spec", "bump", "height",
                        "opacity", "trans", "mask", "ambient", "ao", "displace")
            preference = 100 if any(token in name for token in positive) else 0
            # Source packs often flag albedo maps as Non-Color by mistake.
            # The semantic filename is stronger evidence; the export path
            # explicitly restores base color to sRGB before using the image.
            penalty = 20 if non_color else 0
        elif kind == "normal":
            positive = ("normal", "_n.", "_n_", "_n-", "normalmap")
            negative = ("rough", "gloss", "metal", "spec", "opacity", "mask")
            preference = 100 if any(token in name for token in positive) else 0
            penalty = 0 if non_color else 100
        elif kind == "roughness":
            positive = ("rough", "gloss", "_r.", "_r_", "_r-")
            negative = ("normal", "metal", "spec", "opacity", "mask")
            preference = 100 if any(token in name for token in positive) else 0
            penalty = 0 if non_color else 100
        else:
            positive = ("metal", "_m.", "_m_", "_m-")
            negative = ("normal", "rough", "gloss", "spec", "opacity", "mask")
            preference = 100 if any(token in name for token in positive) else 0
            penalty = 0 if non_color else 100
        if any(token in name for token in negative):
            preference -= 200
        return preference - penalty, name

    ranked = sorted(images, key=lambda image: score(image), reverse=True)
    best = ranked[0]
    return best if score(best)[0] > 0 else None


def _material_is_transparent(material: bpy.types.Material) -> bool:
    if float(material.diffuse_color[3]) < 0.999:
        return True
    for node in _material_nodes(material):
        if node.type == "BSDF_TRANSPARENT":
            return True
        if node.type == "TEX_IMAGE" and node.image is not None:
            name = node.image.name.casefold()
            if any(token in name for token in ("opacity", "transparency", "alpha", "cutout")):
                return True
        if node.type == "GROUP":
            for socket in node.inputs:
                if any(token in socket.name.casefold() for token in ("opacity", "alpha", "transparency")):
                    try:
                        default_alpha = float(socket.default_value)
                    except (TypeError, ValueError, AttributeError):
                        default_alpha = 1.0
                    if socket.is_linked or default_alpha < 0.999:
                        return True
    return False


def _make_principled_material(material: bpy.types.Material, base_image: bpy.types.Image,
                               source_images: list[bpy.types.Image], transparent: bool) -> None:
    base_color = tuple(material.diffuse_color)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.name = "Material Output"
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.name = "Principled BSDF"
    shader.inputs["Roughness"].default_value = 0.68
    shader.inputs["Metallic"].default_value = 0.0
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = base_image
    image_node.image.colorspace_settings.name = "sRGB"
    links.new(image_node.outputs["Color"], shader.inputs["Base Color"])
    if transparent:
        links.new(image_node.outputs["Alpha"], shader.inputs["Alpha"])
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "BLENDED"
        if hasattr(material, "blend_method"):
            material.blend_method = "BLEND"

    normal_image = _related_material_image(base_image, source_images, "normal")
    if normal_image is not None and normal_image != base_image:
        normal_image_node = nodes.new("ShaderNodeTexImage")
        normal_image_node.image = normal_image
        normal_image.colorspace_settings.name = "Non-Color"
        normal_map = nodes.new("ShaderNodeNormalMap")
        links.new(normal_image_node.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])

    roughness_image = _related_material_image(base_image, source_images, "roughness")
    if roughness_image is not None and roughness_image not in {base_image, normal_image}:
        roughness_node = nodes.new("ShaderNodeTexImage")
        roughness_node.image = roughness_image
        roughness_image.colorspace_settings.name = "Non-Color"
        links.new(roughness_node.outputs["Color"], shader.inputs["Roughness"])

    metallic_image = _related_material_image(base_image, source_images, "metallic")
    if metallic_image is not None and metallic_image not in {base_image, normal_image, roughness_image}:
        metallic_node = nodes.new("ShaderNodeTexImage")
        metallic_node.image = metallic_image
        metallic_image.colorspace_settings.name = "Non-Color"
        links.new(metallic_node.outputs["Color"], shader.inputs["Metallic"])

    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = base_color
    material.node_tree.nodes.active = image_node


def _related_material_image(base_image: bpy.types.Image,
                            source_images: list[bpy.types.Image], kind: str) -> bpy.types.Image | None:
    """Return an auxiliary map only when its filename matches this albedo family."""
    base_name = str(base_image.get("npc_source_texture_name", base_image.name))
    base_stem = Path(base_name).stem
    family = re.sub(r"(?i)(?:[_-](?:base.?color|albedo|diffuse|bc|d0?1))$", "", base_stem)
    if family == base_stem:
        return None

    patterns = {
        "normal": re.compile(r"^(?:[_-](?:n|normal|normalmap))(?:$|[_-])", re.IGNORECASE),
        "roughness": re.compile(r"^(?:[_-](?:roughness|rough|gloss))(?:$|[_-])", re.IGNORECASE),
        "metallic": re.compile(r"^(?:[_-](?:metallic|metal))(?:$|[_-])", re.IGNORECASE),
    }
    suffix_pattern = patterns[kind]
    for image in source_images:
        source_name = str(image.get("npc_source_texture_name", image.name))
        candidate_stem = Path(source_name).stem
        suffix = candidate_stem[len(family):] if candidate_stem.casefold().startswith(family.casefold()) else ""
        if suffix and suffix_pattern.match(suffix):
            return image
    return None


def _reconstruct_disconnected_material(material: bpy.types.Material) -> str:
    """Use named source maps when an intake material has no connected output shader."""
    source_images = [node.image for node in _material_nodes(material)
                     if node.type == "TEX_IMAGE" and node.image is not None]
    base_image = _best_material_image(material, "base", source_images)
    if base_image is None:
        base_image = bpy.data.images.new(
            f"{material.name}_BaseColor", width=1, height=1, alpha=True)
        base_image.pixels = [*material.diffuse_color[:3], material.diffuse_color[3]]
        base_image.pack()
    material.use_nodes = True
    _make_principled_material(material, base_image, source_images,
                               _material_is_transparent(material))
    return base_image.name


def _image_sample_signature(image: bpy.types.Image) -> tuple[float, ...]:
    width, height = int(image.size[0]), int(image.size[1])
    sample_points = ((0, 0), (width // 2, height // 2),
                     (width // 4, height // 4),
                     (3 * width // 4, 3 * height // 4))
    return tuple(
        round(float(image.pixels[(y * width + x) * 4 + channel]), 5)
        for x, y in sample_points for channel in range(3)
    )


def _bake_unsupported_materials(meshes: list[bpy.types.Object], model_slug: str) -> dict[str, list[str]]:
    """Bake unsupported source shaders to portable base-color maps for the glTF export."""
    converted: dict[str, list[str]] = {"baked": [], "reconstructed": [], "native": []}
    scene = bpy.context.scene
    previous_engine = scene.render.engine
    targets: list[tuple[bpy.types.Material, bpy.types.Image, list[bpy.types.Image], bool, str]] = []

    for mesh in meshes:
        used_slots = {polygon.material_index for polygon in mesh.data.polygons}
        for slot_index in sorted(used_slots):
            if slot_index >= len(mesh.material_slots):
                continue
            material = mesh.material_slots[slot_index].material
            if material is None:
                continue
            if _gltf_native_base_color(material):
                converted["native"].append(material.name)
                continue
            if not material.use_nodes or material.node_tree is None:
                converted["reconstructed"].append(
                    _reconstruct_disconnected_material(material))
                continue
            output = next((node for node in material.node_tree.nodes
                           if node.type == "OUTPUT_MATERIAL" and node.is_active_output), None)
            surface = output.inputs.get("Surface") if output is not None else None
            if surface is None or not surface.is_linked:
                copied = material.copy()
                copied.name = f"{model_slug}_{mesh.name}_{slot_index}_{material.name}"
                if mesh.material_slots[slot_index].link == "OBJECT":
                    mesh.material_slots[slot_index].material = copied
                else:
                    mesh.data.materials[slot_index] = copied
                converted["reconstructed"].append(_reconstruct_disconnected_material(copied))
                continue

            copied = material.copy()
            copied.name = f"{model_slug}_{mesh.name}_{slot_index}_{material.name}"
            if mesh.material_slots[slot_index].link == "OBJECT":
                mesh.material_slots[slot_index].material = copied
            else:
                mesh.data.materials[slot_index] = copied
            source_images = [node.image for node in _material_nodes(copied)
                             if node.type == "TEX_IMAGE" and node.image is not None]
            transparent = _material_is_transparent(copied)
            base_image = _best_material_image(copied, "base", source_images)
            if base_image is not None:
                _make_principled_material(copied, base_image, source_images, transparent)
                converted["reconstructed"].append(copied.name)
                continue
            nodes = copied.node_tree.nodes
            for node in nodes:
                node.select = False
            image = bpy.data.images.new(
                f"NPC_{model_slug}_{mesh.name}_{slot_index}_BaseColor",
                width=MAX_TEXTURE_EDGE,
                height=MAX_TEXTURE_EDGE,
                alpha=True,
                float_buffer=False,
            )
            image.alpha_mode = "STRAIGHT"
            target = nodes.new("ShaderNodeTexImage")
            target.image = image
            target.select = True
            nodes.active = target
            targets.append((copied, image, source_images, transparent, mesh.name))

    scene.render.engine = "CYCLES"
    scene.cycles.samples = 4
    scene.render.bake.margin = 8
    scene.render.bake.use_pass_color = True
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    for mesh in meshes:
        selected_materials = [record for record in targets if record[4] == mesh.name]
        if not selected_materials:
            continue
        if not mesh.data.uv_layers:
            raise RuntimeError(f"{model_slug}:{mesh.name} has no UV map for material conversion.")

        # Blender bakes every material slot on the active mesh. A slot that was
        # reconstructed directly from an existing color map still needs a
        # separate bake target while another slot is being baked; otherwise
        # Cycles can choose that source map as the active target and overwrite
        # it with the bake result. Keep disposable targets on all non-baked
        # slots so source maps remain intact.
        baked_material_ids = {id(record[0]) for record in selected_materials}
        bake_target_image_ids = {record[1].as_pointer() for record in selected_materials}
        temporary_targets: list[tuple[bpy.types.Material, bpy.types.Node,
                                      bpy.types.Image]] = []
        protected_images: dict[int, tuple[bpy.types.Image, tuple[float, ...]]] = {}
        for slot in mesh.material_slots:
            if slot.material is None or slot.material.node_tree is None:
                continue
            for node in _material_nodes(slot.material):
                image = node.image if node.type == "TEX_IMAGE" else None
                if image is None or image.source == "TILED" or not image.size[0] or not image.size[1]:
                    continue
                if image.as_pointer() in bake_target_image_ids:
                    continue
                protected_images[image.as_pointer()] = (image, _image_sample_signature(image))

        for slot_index, slot in enumerate(mesh.material_slots):
            material = slot.material
            if material is None or id(material) in baked_material_ids:
                continue
            if not material.use_nodes or material.node_tree is None:
                material.use_nodes = True
            nodes = material.node_tree.nodes
            for node in nodes:
                node.select = False
            image = bpy.data.images.new(
                f"NPC_{model_slug}_{mesh.name}_{slot_index}_DiscardBakeTarget",
                width=1,
                height=1,
                alpha=True,
                float_buffer=False,
            )
            target = nodes.new("ShaderNodeTexImage")
            target.image = image
            target.select = True
            nodes.active = target
            temporary_targets.append((material, target, image))

        for view_object in bpy.context.view_layer.objects:
            view_object.select_set(False)
        mesh.hide_set(False)
        mesh.select_set(True)
        for selected in list(bpy.context.selected_objects):
            if selected != mesh:
                selected.select_set(False)
        bpy.context.view_layer.objects.active = mesh
        if bpy.context.view_layer.objects.active.type != "MESH" or any(
            selected.type != "MESH" for selected in bpy.context.selected_objects
        ):
            raise RuntimeError(
                f"Bake for {model_slug}:{mesh.name} requires mesh-only selection; "
                f"active={bpy.context.view_layer.objects.active.name}, "
                f"selected={[selected.name for selected in bpy.context.selected_objects]}"
            )
        try:
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
        finally:
            for material, target, image in temporary_targets:
                if material.node_tree is not None and target.name in material.node_tree.nodes:
                    material.node_tree.nodes.remove(target)
                if image.name in bpy.data.images:
                    bpy.data.images.remove(image, do_unlink=True)
        changed_images = [image.name for image, before in protected_images.values()
                          if _image_sample_signature(image) != before]
        if changed_images:
            raise RuntimeError(
                f"Baking {model_slug}:{mesh.name} changed source color images: "
                f"{', '.join(changed_images)}."
            )
        for material, image, source_images, transparent, mesh_name in selected_materials:
            image.pack()
            _make_principled_material(material, image, source_images, transparent)
            converted["baked"].append(material.name)

    scene.render.engine = previous_engine
    return converted


def _enable_mesh(mesh: bpy.types.Object) -> None:
    # Some source rigs drive outfit visibility from an authoring-only outfit
    # selector. The roster has already chosen the runtime meshes, so remove
    # those visibility drivers in this export-only scene before exposing them.
    for data_path in ("hide_viewport", "hide_render"):
        if mesh.animation_data and any(
            driver.data_path == data_path for driver in mesh.animation_data.drivers
        ):
            mesh.driver_remove(data_path)
    mesh.hide_set(False)
    mesh.hide_viewport = False
    mesh.hide_render = False
    for collection in mesh.users_collection:
        collection.hide_viewport = False
        collection.hide_render = False


def _enable_export_node(node: bpy.types.Object) -> None:
    node.hide_set(False)
    node.hide_viewport = False
    node.hide_render = False
    node.hide_select = False


def _selected_asset_nodes(meshes: list[bpy.types.Object]) -> list[bpy.types.Object]:
    nodes: dict[str, bpy.types.Object] = {obj.name: obj for obj in meshes}
    for mesh in meshes:
        for modifier in mesh.modifiers:
            if modifier.type == "ARMATURE" and modifier.object is not None:
                nodes[modifier.object.name] = modifier.object
        ancestor = mesh.parent
        while ancestor is not None:
            if ancestor.type in {"ARMATURE", "EMPTY"}:
                nodes[ancestor.name] = ancestor
            ancestor = ancestor.parent
    return list(nodes.values())


def _bounds(objects: list[bpy.types.Object]) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points: list[Vector] = []
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        for corner in evaluated.bound_box:
            points.append(obj.matrix_world @ Vector(corner))
    if not points:
        raise RuntimeError("The selected model has no measurable mesh bounds.")
    return points


def _normalize_with_root(nodes: list[bpy.types.Object], meshes: list[bpy.types.Object],
                         target_height: float) -> bpy.types.Object:
    points = _bounds(meshes)
    source_height = max(point.z for point in points) - min(point.z for point in points)
    if source_height < 0.05:
        raise RuntimeError(f"Selected model bounds have implausible height {source_height:.4f}.")
    scale = target_height / source_height
    rotation = Matrix.Rotation(math.pi, 4, "Z")
    rotated = [(rotation @ point.to_4d()).to_3d() * scale for point in points]
    min_corner = Vector((min(point.x for point in rotated), min(point.y for point in rotated),
                         min(point.z for point in rotated)))
    max_corner = Vector((max(point.x for point in rotated), max(point.y for point in rotated),
                         max(point.z for point in rotated)))
    translation = Vector((-(min_corner.x + max_corner.x) * 0.5,
                          -(min_corner.y + max_corner.y) * 0.5,
                          -min_corner.z))

    root = bpy.data.objects.new("NPC_ModelRoot", None)
    root.empty_display_type = "PLAIN_AXES"
    bpy.context.scene.collection.objects.link(root)
    root.rotation_euler.z = math.pi
    root.scale = (scale, scale, scale)
    root.location = translation

    node_names = {obj.name for obj in nodes}
    top_nodes = [obj for obj in nodes if obj.parent is None or obj.parent.name not in node_names]
    original_matrices = {obj.name: obj.matrix_world.copy() for obj in top_nodes}
    for obj in top_nodes:
        obj.parent = root
        obj.matrix_world = original_matrices[obj.name]
    return root


def _glb_json(path: Path) -> tuple[dict, memoryview]:
    data = path.read_bytes()
    if len(data) < 32 or data[:4] != b"glTF":
        raise RuntimeError(f"Blender did not create a valid GLB file: {path}")
    json_length = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20:20 + json_length])
    bin_header = 20 + json_length
    bin_length = struct.unpack_from("<I", data, bin_header)[0]
    return document, memoryview(data)[bin_header + 8:bin_header + 8 + bin_length]


def _accessor_elements(document: dict, binary: bytes | bytearray | memoryview,
                       accessor_index: int) -> list[bytes]:
    accessor = document["accessors"][accessor_index]
    if "sparse" in accessor:
        raise RuntimeError("Sparse accessors are not supported for base-mesh preservation.")
    component_sizes = {5121: 1, 5123: 2, 5125: 4, 5126: 4}
    component_size = component_sizes.get(accessor["componentType"])
    component_counts = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
                        "MAT2": 4, "MAT3": 9, "MAT4": 16}
    component_count = component_counts.get(accessor["type"])
    if component_size is None or component_count is None:
        raise RuntimeError("Unsupported accessor layout in base-mesh preservation.")
    element_size = component_size * component_count
    view = document["bufferViews"][accessor["bufferView"]]
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    stride = view.get("byteStride", element_size)
    if stride < element_size:
        raise RuntimeError("Accessor stride is smaller than its element size.")
    result = []
    for index in range(accessor["count"]):
        element_start = start + index * stride
        element_end = element_start + element_size
        if element_end > len(binary):
            raise RuntimeError("Accessor data exceeds the GLB binary buffer.")
        result.append(bytes(binary[element_start:element_end]))
    return result


def _copy_accessor_data(source_document: dict, source_binary: bytes | bytearray | memoryview,
                        source_index: int, target_document: dict,
                        target_binary: bytearray, target_index: int) -> None:
    source = source_document["accessors"][source_index]
    target = target_document["accessors"][target_index]
    if (source.get("componentType"), source.get("type"), source.get("count")) != (
            target.get("componentType"), target.get("type"), target.get("count")):
        raise RuntimeError("Reference and generated accessors have incompatible layouts.")
    elements = _accessor_elements(source_document, source_binary, source_index)
    view = target_document["bufferViews"][target["bufferView"]]
    target_start = view.get("byteOffset", 0) + target.get("byteOffset", 0)
    target_stride = view.get("byteStride", len(elements[0]) if elements else 0)
    for index, element in enumerate(elements):
        element_start = target_start + index * target_stride
        target_binary[element_start:element_start + len(element)] = element
    for key in ("min", "max"):
        if key in source:
            target[key] = source[key]
        else:
            target.pop(key, None)


def _write_glb(path: Path, document: dict, binary: bytearray) -> None:
    document["buffers"][0]["byteLength"] = len(binary)
    json_chunk = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    json_chunk += b" " * ((-len(json_chunk)) % 4)
    binary.extend(b"\0" * ((-len(binary)) % 4))
    output = bytearray(b"glTF" + struct.pack("<II", 2, 0))
    output.extend(struct.pack("<II", len(json_chunk), 0x4E4F534A))
    output.extend(json_chunk)
    output.extend(struct.pack("<II", len(binary), 0x004E4942))
    output.extend(binary)
    struct.pack_into("<I", output, 8, len(output))
    path.write_bytes(output)


def _preserve_reference_rest_pose(reference_path: Path, generated_path: Path) -> None:
    """Keep prior mesh rest data where compatible while allowing added meshes and compact rigs."""
    reference, reference_binary_view = _glb_json(reference_path)
    generated, generated_binary_view = _glb_json(generated_path)
    reference_binary = bytes(reference_binary_view)
    generated_binary = bytearray(generated_binary_view)

    def index_unique_nodes(document: dict, path: Path) -> dict[str, dict]:
        nodes = document.get("nodes", [])
        names = [node.get("name") for node in nodes]
        if any(not name for name in names) or len(names) != len(set(names)):
            raise RuntimeError(f"{path.name} has missing or duplicate node names; cannot match its rest pose.")
        return {node["name"]: node for node in nodes}

    reference_nodes = index_unique_nodes(reference, reference_path)
    generated_nodes = index_unique_nodes(generated, generated_path)
    missing_reference_nodes = reference_nodes.keys() - generated_nodes.keys()
    if missing_reference_nodes:
        missing_mesh_nodes = sorted(name for name in missing_reference_nodes
                                    if "mesh" in reference_nodes[name])
        if missing_mesh_nodes:
            raise RuntimeError(
                f"{generated_path.name} is missing existing mesh nodes: {missing_mesh_nodes[:20]}.")
    if "NPC_ModelRoot" not in reference_nodes or "NPC_ModelRoot" not in generated_nodes:
        raise RuntimeError(f"{generated_path.name} has no shared normalized root to preserve.")

    for name in reference_nodes.keys() & generated_nodes.keys():
        target_node = generated_nodes[name]
        source_node = reference_nodes[name]
        if ("mesh" in source_node) != ("mesh" in target_node):
            raise RuntimeError(f"Node {name!r} changed mesh assignment in {generated_path.name}.")
        for transform in ("translation", "rotation", "scale", "matrix"):
            target_node.pop(transform, None)
            if transform in source_node:
                target_node[transform] = source_node[transform]

    for name, target_node in generated_nodes.items():
        if "mesh" not in target_node:
            continue
        if name not in reference_nodes:
            continue
        source_mesh = reference["meshes"][reference_nodes[name]["mesh"]]
        target_mesh = generated["meshes"][target_node["mesh"]]
        if source_mesh.get("name") != target_mesh.get("name"):
            raise RuntimeError(f"Mesh assignment for node {name!r} changed in {generated_path.name}.")
        source_primitives = source_mesh.get("primitives", [])
        target_primitives = target_mesh.get("primitives", [])
        if len(source_primitives) != len(target_primitives):
            raise RuntimeError(f"Primitive count changed for mesh {source_mesh.get('name')!r}.")
        for source_primitive, target_primitive in zip(source_primitives, target_primitives):
            source_position = source_primitive.get("attributes", {}).get("POSITION")
            target_position = target_primitive.get("attributes", {}).get("POSITION")
            if source_position is None or target_position is None:
                raise RuntimeError(f"Mesh {source_mesh.get('name')!r} has no POSITION accessor.")
            if (source_primitive.get("indices") is None) != (target_primitive.get("indices") is None):
                raise RuntimeError(f"Index layout changed for mesh {source_mesh.get('name')!r}.")
            if source_primitive.get("indices") is not None:
                source_indices = _accessor_elements(reference, reference_binary, source_primitive["indices"])
                target_indices = _accessor_elements(generated, generated_binary, target_primitive["indices"])
                if source_indices != target_indices:
                    raise RuntimeError(f"Topology changed for mesh {source_mesh.get('name')!r}.")
            _copy_accessor_data(reference, reference_binary, source_position,
                                generated, generated_binary, target_position)

    reference_skins = {skin.get("name"): skin for skin in reference.get("skins", [])}
    generated_skins = {skin.get("name"): skin for skin in generated.get("skins", [])}
    preserved_skin_count = 0
    for name, target_skin in generated_skins.items():
        if name not in reference_skins:
            continue
        source_skin = reference_skins[name]
        source_joints = [reference["nodes"][index]["name"] for index in source_skin["joints"]]
        target_joints = [generated["nodes"][index]["name"] for index in target_skin["joints"]]
        source_joint_indices = {joint_name: index for index, joint_name in enumerate(source_joints)}
        missing_joints = [joint for joint in target_joints if joint not in source_joint_indices]
        if missing_joints:
            raise RuntimeError(
                f"{generated_path.name} has joints with no source rest bind for skin {name!r}: "
                f"{missing_joints[:20]}.")
        source_bind = source_skin.get("inverseBindMatrices")
        target_bind = target_skin.get("inverseBindMatrices")
        if (source_bind is None) != (target_bind is None):
            raise RuntimeError(f"Bind matrix layout changed for skin {name!r}.")
        if source_bind is not None:
            source_accessor = reference["accessors"][source_bind]
            target_accessor = generated["accessors"][target_bind]
            if (source_accessor.get("componentType"), source_accessor.get("type")) != (
                    target_accessor.get("componentType"), target_accessor.get("type")):
                raise RuntimeError(f"Bind matrix layout changed for skin {name!r}.")
            source_elements = _accessor_elements(reference, reference_binary, source_bind)
            target_view = generated["bufferViews"][target_accessor["bufferView"]]
            element_size = len(source_elements[0]) if source_elements else 0
            target_stride = target_view.get("byteStride", element_size)
            target_start = target_view.get("byteOffset", 0) + target_accessor.get("byteOffset", 0)
            for target_index, joint_name in enumerate(target_joints):
                source_element = source_elements[source_joint_indices[joint_name]]
                start = target_start + target_index * target_stride
                generated_binary[start:start + element_size] = source_element
        preserved_skin_count += 1
    if not preserved_skin_count:
        raise RuntimeError(f"{generated_path.name} has no matching source skin for rest-pose preservation.")

    _write_glb(generated_path, generated, generated_binary)


def _image_dimensions(data: bytes | memoryview) -> tuple[int, int]:
    data = bytes(data)
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return struct.unpack_from(">II", data, 16)
    if data.startswith(b"\xff\xd8"):
        offset = 2
        while offset < len(data) - 9:
            if data[offset] != 0xFF:
                offset += 1
                continue
            while offset < len(data) and data[offset] == 0xFF:
                offset += 1
            if offset >= len(data):
                break
            marker = data[offset]
            offset += 1
            if marker in {0xD8, 0xD9} or 0xD0 <= marker <= 0xD7:
                continue
            segment_length = int.from_bytes(data[offset:offset + 2], "big")
            if marker in {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7,
                          0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}:
                height, width = struct.unpack_from(">HH", data, offset + 3)
                return width, height
            offset += segment_length
    raise RuntimeError("GLB contains an image format whose dimensions cannot be validated.")


def _compact_glb(path: Path) -> None:
    """Drop exporter-created unreferenced accessors and compact the GLB buffer."""
    data = path.read_bytes()
    if len(data) < 20 or data[:4] != b"glTF":
        raise RuntimeError(f"Blender did not create a valid GLB file: {path}")
    version, total_length = struct.unpack_from("<II", data, 4)
    if version != 2 or total_length != len(data):
        raise RuntimeError(f"Unsupported GLB header in {path.name}.")

    chunks: list[tuple[int, bytes]] = []
    offset = 12
    while offset < len(data):
        length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunks.append((chunk_type, data[offset:offset + length]))
        offset += length
    json_chunk = next((chunk for kind, chunk in chunks if kind == 0x4E4F534A), None)
    binary_chunk = next((chunk for kind, chunk in chunks if kind == 0x004E4942), None)
    if json_chunk is None or binary_chunk is None:
        raise RuntimeError(f"GLB {path.name} must contain JSON and binary chunks.")
    document = json.loads(json_chunk)
    binary = memoryview(binary_chunk)

    accessor_references: set[int] = set()
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            accessor_references.update(primitive.get("attributes", {}).values())
            if isinstance(primitive.get("indices"), int):
                accessor_references.add(primitive["indices"])
            for target in primitive.get("targets", []):
                accessor_references.update(target.values())
    for skin in document.get("skins", []):
        if isinstance(skin.get("inverseBindMatrices"), int):
            accessor_references.add(skin["inverseBindMatrices"])
    for animation in document.get("animations", []):
        samplers = animation.get("samplers", [])
        used_sampler_indices = sorted({channel["sampler"] for channel in animation.get("channels", [])})
        if any(index < 0 or index >= len(samplers) for index in used_sampler_indices):
            raise RuntimeError(f"GLB {path.name} contains an invalid animation sampler reference.")
        animation["samplers"] = [samplers[index] for index in used_sampler_indices]
        sampler_map = {old: new for new, old in enumerate(used_sampler_indices)}
        for channel in animation.get("channels", []):
            channel["sampler"] = sampler_map[channel["sampler"]]
        for sampler in animation["samplers"]:
            accessor_references.add(sampler["input"])
            accessor_references.add(sampler["output"])

    accessors = document.get("accessors", [])
    if any(index < 0 or index >= len(accessors) for index in accessor_references):
        raise RuntimeError(f"GLB {path.name} contains an invalid accessor reference.")
    used_accessors = sorted(accessor_references)
    accessor_map = {old: new for new, old in enumerate(used_accessors)}
    compact_accessors = [accessors[index] for index in used_accessors]

    view_references: set[int] = set()
    for accessor in compact_accessors:
        if isinstance(accessor.get("bufferView"), int):
            view_references.add(accessor["bufferView"])
        sparse = accessor.get("sparse", {})
        for sparse_part in (sparse.get("indices", {}), sparse.get("values", {})):
            if isinstance(sparse_part.get("bufferView"), int):
                view_references.add(sparse_part["bufferView"])
    for image in document.get("images", []):
        if isinstance(image.get("bufferView"), int):
            view_references.add(image["bufferView"])

    # Extension-owned views, including Draco data if enabled by a local exporter.
    def collect_extension_views(value: object) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                if key == "bufferView" and isinstance(child, int):
                    view_references.add(child)
                else:
                    collect_extension_views(child)
        elif isinstance(value, list):
            for child in value:
                collect_extension_views(child)

    collect_extension_views(document.get("extensions", {}))
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            collect_extension_views(primitive.get("extensions", {}))

    views = document.get("bufferViews", [])
    if any(index < 0 or index >= len(views) for index in view_references):
        raise RuntimeError(f"GLB {path.name} contains an invalid buffer view reference.")
    used_views = sorted(view_references)
    view_map = {old: new for new, old in enumerate(used_views)}
    compact_views = []
    compact_binary = bytearray()
    for old_index in used_views:
        view = dict(views[old_index])
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        if start < 0 or end > len(binary):
            raise RuntimeError(f"GLB {path.name} has an out-of-range buffer view.")
        while len(compact_binary) % 4:
            compact_binary.append(0)
        view["buffer"] = 0
        view["byteOffset"] = len(compact_binary)
        compact_binary.extend(binary[start:end])
        compact_views.append(view)

    def remap_view_references(value: object) -> None:
        if isinstance(value, dict):
            for key, child in list(value.items()):
                if key == "bufferView" and isinstance(child, int):
                    value[key] = view_map[child]
                else:
                    remap_view_references(child)
        elif isinstance(value, list):
            for child in value:
                remap_view_references(child)

    for accessor in compact_accessors:
        remap_view_references(accessor)
    for image in document.get("images", []):
        remap_view_references(image)
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            remap_view_references(primitive.get("extensions", {}))
    remap_view_references(document.get("extensions", {}))

    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            primitive["attributes"] = {
                semantic: accessor_map[index]
                for semantic, index in primitive.get("attributes", {}).items()
            }
            if isinstance(primitive.get("indices"), int):
                primitive["indices"] = accessor_map[primitive["indices"]]
            primitive["targets"] = [
                {semantic: accessor_map[index] for semantic, index in target.items()}
                for target in primitive.get("targets", [])
            ]
    for skin in document.get("skins", []):
        if isinstance(skin.get("inverseBindMatrices"), int):
            skin["inverseBindMatrices"] = accessor_map[skin["inverseBindMatrices"]]
    for animation in document.get("animations", []):
        for sampler in animation.get("samplers", []):
            sampler["input"] = accessor_map[sampler["input"]]
            sampler["output"] = accessor_map[sampler["output"]]

    document["accessors"] = compact_accessors
    document["bufferViews"] = compact_views
    document["buffers"][0]["byteLength"] = len(compact_binary)
    json_bytes = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    json_bytes += b" " * ((-len(json_bytes)) % 4)
    compact_binary.extend(b"\0" * ((-len(compact_binary)) % 4))
    output = bytearray(b"glTF" + struct.pack("<II", 2, 0))
    output.extend(struct.pack("<II", len(json_bytes), 0x4E4F534A))
    output.extend(json_bytes)
    output.extend(struct.pack("<II", len(compact_binary), 0x004E4942))
    output.extend(compact_binary)
    struct.pack_into("<I", output, 8, len(output))
    path.write_bytes(output)


def _decode_accessor(document: dict, binary: bytes | bytearray | memoryview,
                     accessor_index: int) -> list[tuple[float, ...]]:
    accessor = document["accessors"][accessor_index]
    components = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
    formats = {5121: ("B", 1, 255), 5123: ("H", 2, 65535),
               5125: ("I", 4, 4294967295), 5126: ("f", 4, 1)}
    if accessor.get("componentType") not in formats or accessor.get("type") not in components:
        raise RuntimeError("Unsupported accessor layout in the generated GLB.")
    fmt, component_size, max_value = formats[accessor["componentType"]]
    component_count = components[accessor["type"]]
    values: list[tuple[float, ...]] = []
    for element in _accessor_elements(document, binary, accessor_index):
        value = struct.unpack("<" + fmt * component_count, element)
        if accessor.get("normalized") and accessor["componentType"] != 5126:
            values.append(tuple(component / max_value for component in value))
        else:
            values.append(tuple(float(component) for component in value))
    return values


def _weighted_joint_nodes(document: dict, binary: bytes | bytearray | memoryview) -> set[int]:
    weighted_nodes: set[int] = set()
    for node in document.get("nodes", []):
        mesh_index = node.get("mesh")
        skin_index = node.get("skin")
        if not isinstance(mesh_index, int) or not isinstance(skin_index, int):
            continue
        joints = document["skins"][skin_index]["joints"]
        for primitive in document["meshes"][mesh_index].get("primitives", []):
            attributes = primitive.get("attributes", {})
            for set_index in range(4):
                joint_accessor = attributes.get(f"JOINTS_{set_index}")
                weight_accessor = attributes.get(f"WEIGHTS_{set_index}")
                if joint_accessor is None or weight_accessor is None:
                    continue
                joint_rows = _decode_accessor(document, binary, joint_accessor)
                weight_rows = _decode_accessor(document, binary, weight_accessor)
                for joint_row, weight_row in zip(joint_rows, weight_rows):
                    for joint_index, weight in zip(joint_row, weight_row):
                        if weight > 0.001 and 0 <= int(joint_index) < len(joints):
                            weighted_nodes.add(joints[int(joint_index)])
    return weighted_nodes


def _weighted_animation_motion(document: dict, binary: bytes | bytearray | memoryview,
                               animation: dict, weighted_nodes: set[int]) -> dict[int, float]:
    motion: dict[int, float] = {}
    for channel in animation.get("channels", []):
        target = channel.get("target", {})
        node_index = target.get("node")
        if target.get("path") != "rotation" or node_index not in weighted_nodes:
            continue
        sampler = animation["samplers"][channel["sampler"]]
        accessor = document["accessors"][sampler["output"]]
        if accessor.get("componentType") != 5126 or accessor.get("type") != "VEC4":
            raise RuntimeError(
                f"{animation.get('name')} has an invalid skeletal rotation accessor.")
        rotations = _decode_accessor(document, binary, sampler["output"])
        if len(rotations) < 2:
            continue
        baseline = rotations[0]
        closest_dot = min(abs(sum(baseline[index] * rotation[index] for index in range(4)))
                          for rotation in rotations[1:])
        motion[node_index] = 2.0 * math.acos(max(0.0, min(1.0, closest_dot)))
    return motion


def _validate_glb(path: Path, expected_source_actions: set[str],
                  expected_facial_clips: set[str],
                  generated_facial_clips: set[str], required_facial_targets: set[str],
                  required_meshes: set[str], baked_materials: set[str]) -> list[str]:
    document, binary = _glb_json(path)
    for image in document.get("images", []):
        view = document["bufferViews"][image["bufferView"]]
        image_start = view.get("byteOffset", 0)
        image_data = binary[image_start:image_start + view["byteLength"]]
        width, height = _image_dimensions(image_data)
        if max(width, height) > MAX_TEXTURE_EDGE:
            raise RuntimeError(
                f"{path.name} has texture {image.get('name', '<unnamed>')} at {width}x{height}, "
                f"over the {MAX_TEXTURE_EDGE}px runtime limit.")
    animation_names = [animation.get("name", "") for animation in document.get("animations", [])]
    missing = EXPECTED_ACTIONS - set(animation_names)
    if missing:
        raise RuntimeError(f"{path.name} is missing locomotion clips: {sorted(missing)}")
    if len(animation_names) != len(set(animation_names)):
        raise RuntimeError(f"{path.name} has duplicate animation names: {animation_names}")
    missing_source = expected_source_actions - set(animation_names)
    if missing_source:
        raise RuntimeError(f"{path.name} omitted source animation clips: {sorted(missing_source)}")
    missing_facial = expected_facial_clips - set(animation_names)
    if missing_facial:
        raise RuntimeError(f"{path.name} omitted facial animation clips: {sorted(missing_facial)}")
    available_facial_targets = {
        name
        for mesh in document.get("meshes", [])
        for name in mesh.get("extras", {}).get("targetNames", [])
    }
    missing_targets = required_facial_targets - available_facial_targets
    if missing_targets:
        raise RuntimeError(f"{path.name} omitted facial morph targets: {sorted(missing_targets)}")
    animated_target_nodes = {
        index
        for index, node in enumerate(document.get("nodes", []))
        if isinstance(node.get("mesh"), int)
        and document["meshes"][node["mesh"]].get("primitives")
        and any(primitive.get("targets")
                for primitive in document["meshes"][node["mesh"]]["primitives"])
    }
    for animation in document.get("animations", []):
        if animation.get("name") not in generated_facial_clips:
            continue
        animated_weights = False
        for channel in animation.get("channels", []):
            target = channel.get("target", {})
            if target.get("path") != "weights" or target.get("node") not in animated_target_nodes:
                continue
            sampler = animation["samplers"][channel["sampler"]]
            accessor = document["accessors"][sampler["output"]]
            if accessor.get("componentType") != 5126 or accessor.get("type") != "SCALAR":
                raise RuntimeError(
                    f"{path.name}:{animation['name']} has an invalid facial weight accessor.")
            view = document["bufferViews"][accessor["bufferView"]]
            stride = view.get("byteStride", 4)
            first = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
            values = [struct.unpack_from("<f", binary, first + index * stride)[0]
                      for index in range(accessor["count"])]
            if values and max(values) - min(values) > 0.01:
                animated_weights = True
                break
        if not animated_weights:
            raise RuntimeError(
                f"{path.name}:{animation['name']} does not change any facial morph weight.")
    root_indices = {index for index, node in enumerate(document.get("nodes", []))
                    if node.get("name") == "NPC_ModelRoot"}
    if not root_indices:
        raise RuntimeError(f"{path.name} is missing its normalized model root.")

    for animation in document.get("animations", []):
        if animation.get("name") not in EXPECTED_ACTIONS:
            continue
        for channel in animation.get("channels", []):
            target = channel.get("target", {})
            if target.get("path") != "translation" or target.get("node") not in root_indices:
                continue
            sampler = animation["samplers"][channel["sampler"]]
            accessor = document["accessors"][sampler["output"]]
            view = document["bufferViews"][accessor["bufferView"]]
            stride = view.get("byteStride", 12)
            first = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
            keys = [struct.unpack_from("<3f", binary, first + index * stride)
                    for index in range(accessor["count"])]
            if keys and any(any(abs(value - keys[0][axis]) > 1e-5 for axis, value in enumerate(key))
                            for key in keys[1:]):
                raise RuntimeError(f"{path.name}:{animation['name']} moves the normalized model root.")
        rotation_targets = {channel.get("target", {}).get("node")
                            for channel in animation.get("channels", [])
                            if channel.get("target", {}).get("path") == "rotation"}
        if len(rotation_targets) < 12:
            raise RuntimeError(f"{path.name}:{animation['name']} animates too few articulated nodes.")
        # Source control bones are not necessarily exported: several assets
        # drive hidden deformation joints through constraints and FK/IK rigs.
        # Actual skin deformation is verified below against weighted joints.
    if not document.get("skins"):
        raise RuntimeError(f"{path.name} has no exported skinned armature.")
    exported_mesh_nodes = {node.get("name") for node in document.get("nodes", [])
                           if isinstance(node.get("mesh"), int)}
    missing_meshes = required_meshes - exported_mesh_nodes
    if missing_meshes:
        raise RuntimeError(f"{path.name} omitted roster mesh nodes: {sorted(missing_meshes)}")

    materials_by_name = {material.get("name"): material
                         for material in document.get("materials", [])}
    for material_name in baked_materials:
        material = materials_by_name.get(material_name)
        texture = material.get("pbrMetallicRoughness", {}).get("baseColorTexture") \
            if material is not None else None
        if texture is None:
            raise RuntimeError(f"{path.name} did not retain baked base color for {material_name!r}.")

    weighted_nodes = _weighted_joint_nodes(document, binary)
    if not weighted_nodes:
        raise RuntimeError(f"{path.name} has no weighted skin joints to validate.")
    for animation in document.get("animations", []):
        name = animation.get("name")
        if name not in {"human_idle", "human_walk", "human_run"}:
            continue
        motion = _weighted_animation_motion(document, binary, animation, weighted_nodes)
        threshold = 0.004 if name == "human_idle" else 0.035
        moving_weighted_joints = sum(value >= threshold for value in motion.values())
        minimum = 2 if name == "human_idle" else 8
        if moving_weighted_joints < minimum:
            raise RuntimeError(
                f"{path.name}:{name} visibly animates only {moving_weighted_joints} weighted joints; "
                f"expected at least {minimum} (threshold {threshold:.3f} rad).")
    if path.stat().st_size >= 100_000_000:
        raise RuntimeError(f"{path.name} exceeds GitHub's 100 MB file limit.")
    return animation_names


def _export_model(model: dict, args: argparse.Namespace, global_index: dict[str, list[Path]],
                  target_height: float) -> dict:
    with tempfile.TemporaryDirectory(prefix=f"unnamed-npc-{model['slug']}-textures-") as temp_dir:
        return _export_model_with_texture_workspace(
            model, args, global_index, target_height, Path(temp_dir))


def _export_model_with_texture_workspace(
    model: dict,
    args: argparse.Namespace,
    global_index: dict[str, list[Path]],
    target_height: float,
    texture_workspace: Path,
) -> dict:
    source = args.source_root / model["source_file"]
    if not source.is_file():
        raise FileNotFoundError(source)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    meshes = []
    missing_meshes = []
    for name in model["meshes"]:
        obj = bpy.context.scene.objects.get(name)
        if obj is None or obj.type != "MESH":
            missing_meshes.append(name)
        else:
            _enable_mesh(obj)
            if bpy.context.scene.collection not in obj.users_collection:
                bpy.context.scene.collection.objects.link(obj)
            meshes.append(obj)
    if missing_meshes:
        raise RuntimeError(f"{model['slug']} source mesh names not found: {missing_meshes}")

    armature = bpy.context.scene.objects.get(model["armature"])
    if armature is None or armature.type != "ARMATURE":
        raise RuntimeError(f"{model['slug']} rig not found: {model['armature']}")
    bone_map = model["bone_map"]
    locomotion_control_map = model.get("locomotion_control_map", bone_map)
    animation_switches = model.get("animation_switches", {})
    bone_animation_switches = model.get("bone_animation_switches", {})
    required = REQUIRED_LOCOMOTION_BONES
    if not (required - OPTIONAL_LOCOMOTION_BONES).issubset(bone_map):
        missing_semantic = required - OPTIONAL_LOCOMOTION_BONES - set(bone_map)
        raise RuntimeError(f"{model['slug']} has incomplete semantic bone map: {sorted(missing_semantic)}")
    if not (required - OPTIONAL_LOCOMOTION_BONES).issubset(locomotion_control_map):
        missing_semantic = required - OPTIONAL_LOCOMOTION_BONES - set(locomotion_control_map)
        raise RuntimeError(
            f"{model['slug']} has incomplete locomotion control map: {sorted(missing_semantic)}")
    missing_bones = (set(bone_map.values()) | set(locomotion_control_map.values())) \
        - set(armature.data.bones.keys())
    if missing_bones:
        raise RuntimeError(f"{model['slug']} rig is missing mapped bones or controls: {sorted(missing_bones)}")
    missing_switches = set(animation_switches) - set(armature.keys())
    if missing_switches:
        raise RuntimeError(f"{model['slug']} rig is missing animation switches: {sorted(missing_switches)}")
    missing_switch_bones = set(bone_animation_switches) - set(armature.pose.bones.keys())
    missing_switch_properties = {
        f'{bone_name}["{property_name}"]'
        for bone_name, properties in bone_animation_switches.items()
        if bone_name in armature.pose.bones
        for property_name in properties
        if property_name not in armature.pose.bones[bone_name]
    }
    if missing_switch_bones or missing_switch_properties:
        raise RuntimeError(
            f"{model['slug']} rig is missing bone animation switches: "
            f"bones={sorted(missing_switch_bones)}, "
            f"properties={sorted(missing_switch_properties)}")

    nodes = _selected_asset_nodes(meshes)
    nodes_by_name = {node.name: node for node in nodes}
    nodes_by_name[armature.name] = armature
    nodes = list(nodes_by_name.values())
    if armature not in nodes:
        nodes.append(armature)
    for node in nodes:
        _enable_export_node(node)
        if bpy.context.scene.collection not in node.users_collection:
            bpy.context.scene.collection.objects.link(node)

    texture_errors = _resolve_and_pack_textures(
        meshes, source.parent, global_index, texture_workspace)
    if texture_errors:
        raise RuntimeError(f"{model['slug']} has unresolved used textures: {texture_errors[:20]}")

    material_conversion = _bake_unsupported_materials(meshes, model["slug"])

    facial_clips, facial_targets, source_morph_target_count, runtime_morph_target_count = (
        _add_facial_expression_actions(meshes))

    for object_name, ratio in model.get("decimate_ratios", {}).items():
        obj = bpy.context.scene.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"{model['slug']} decimation mesh was not found: {object_name}")
        modifier = obj.modifiers.new("NPC_Runtime_Detail", "DECIMATE")
        modifier.ratio = float(ratio)

    source_actions = _source_actions_by_armature(nodes)
    actions = add_humanoid_locomotion_actions(
        armature, locomotion_control_map, animation_switches, bone_animation_switches)
    source_action_names = _prepare_animation_tracks(nodes, source_actions, actions, armature)
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = 49
    root = _normalize_with_root(nodes, meshes, target_height)
    nodes.append(root)

    # Avoid the UI-dependent select_all operator: some source scenes open in a
    # non-3D-editor context even in background mode. Object selection itself is
    # a data operation and behaves consistently across those scenes.
    for scene_object in bpy.context.view_layer.objects:
        scene_object.select_set(False)
    for obj in nodes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    output = args.output_root / f"{model['slug']}.glb"
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        # Applying the source Armature modifier bakes the rest pose into mesh
        # vertices and removes the skin in several downloaded model packages.
        export_apply=False,
        export_skins=True,
        export_morph=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=True,
        export_force_sampling=True,
        # Sample rig constraints and FK/IK switches into the exported skeleton.
        # Directly exporting authoring controls leaves weighted mesh joints at rest.
        export_bake_animation=True,
        # Keep the runtime skin compact while baking control motion onto its deform bones.
        export_def_bones=True,
        export_optimize_animation_size=True,
        export_image_format="AUTO",
        export_jpeg_quality=90,
    )
    _compact_glb(output)
    reference_path = None
    if args.preserve_base_from:
        reference_path = args.preserve_base_from / f"{model['slug']}.glb"
        if not reference_path.is_file():
            raise FileNotFoundError(f"Missing base-pose reference GLB: {reference_path}")
        _preserve_reference_rest_pose(reference_path, output)
    facial_clip_map = _source_facial_clip_map(source_action_names)
    facial_clip_map.update(facial_clips)
    if not facial_clip_map:
        raise RuntimeError(f"{model['slug']} has no usable facial actions or expression targets.")
    exported_actions = _validate_glb(
        output,
        source_action_names,
        set(facial_clip_map.values()),
        set(facial_targets),
        {target["shape_key"] for targets in facial_targets.values() for target in targets},
        required_meshes=set(model["meshes"]),
        baked_materials=set(material_conversion["baked"]),
    )
    result = {
        "npc_id": model["npc_id"],
        "slug": model["slug"],
        "sex": model["sex"],
        "source_file": model["source_file"],
        "rig": model["armature"],
        "source_meshes": model["meshes"],
        "bone_map": bone_map,
        "locomotion_control_map": locomotion_control_map,
        "animation_switches": animation_switches,
        "bone_animation_switches": bone_animation_switches,
        "material_conversion": material_conversion,
        "animation_clips": exported_actions,
        "locomotion_clips": sorted(EXPECTED_ACTIONS),
        "locomotion_root_motion": False,
        "facial_expression_clips": facial_clip_map,
        "facial_expression_targets": facial_targets,
        "target_height_m": target_height,
        "source_height_m": target_height / root.scale.x,
        "normalized_scale": root.scale.x,
        "glb_bytes": output.stat().st_size,
        "textures_max_edge": MAX_TEXTURE_EDGE,
        "source_animations_expected": sorted(source_action_names),
        "source_morph_target_count": source_morph_target_count,
        "source_morph_targets_omitted": source_morph_target_count,
        "runtime_expression_targets_generated": runtime_morph_target_count,
        "runtime_morph_targets_included": runtime_morph_target_count > 0,
        "runtime_morph_target_count": runtime_morph_target_count,
        "base_mesh_preserved_from": reference_path.name if reference_path else None,
    }
    output.with_suffix(".json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({**result, "output": str(output)}, indent=2))
    return result


def main() -> None:
    args = parse_args()
    args.source_root = args.source_root.expanduser().resolve()
    args.output_root = args.output_root.expanduser().resolve()
    if args.preserve_base_from:
        args.preserve_base_from = args.preserve_base_from.expanduser().resolve()
    roster = json.loads(args.roster.read_text(encoding="utf-8"))
    models = roster["models"]
    if args.model:
        models = [model for model in models if model["slug"] == args.model]
        if not models:
            raise ValueError(f"No roster model named '{args.model}'.")
    global_index = _texture_index(args.source_root)
    results = [_export_model(model, args, global_index, float(roster["target_height_m"]))
               for model in models]
    print(json.dumps({"export_count": len(results), "total_bytes": sum(item["glb_bytes"] for item in results)}, indent=2))


if __name__ == "__main__":
    main()
