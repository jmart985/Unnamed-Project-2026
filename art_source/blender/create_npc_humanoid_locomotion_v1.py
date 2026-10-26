"""Add and export in-place NPC locomotion clips to an adapted humanoid blend."""
from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from humanoid_locomotion_v1 import add_humanoid_locomotion_actions


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend", type=Path, required=True, help="Adapted humanoid .blend to update in place.")
    parser.add_argument("--glb", type=Path, required=True, help="Runtime GLB output path.")
    parser.add_argument("--metadata", type=Path, required=True, help="Asset metadata JSON to update.")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def export_runtime(glb_path: Path, armature: bpy.types.Object) -> list[str]:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"ARMATURE", "MESH", "EMPTY"}:
            obj.select_set(True)
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
    data = glb_path.read_bytes()
    if data[:4] != b"glTF":
        raise RuntimeError(f"Blender did not create a GLB file: {glb_path}")
    json_size = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20:20 + json_size])
    binary_chunk_header = 20 + json_size
    binary_size = struct.unpack_from("<I", data, binary_chunk_header)[0]
    binary_data = memoryview(data)[binary_chunk_header + 8:binary_chunk_header + 8 + binary_size]
    animations = document.get("animations", [])
    names = [animation.get("name", "") for animation in animations]
    expected = {"human_idle", "human_walk", "human_run"}
    if not expected.issubset(names):
        raise RuntimeError(f"Runtime GLB is missing NPC locomotion clips: {sorted(expected - set(names))}")
    if len(names) != len(set(names)):
        raise RuntimeError(f"Runtime GLB contains duplicate animation names: {names}")
    animated_paths = set()
    for animation in animations:
        for channel in animation.get("channels", []):
            path = channel.get("target", {}).get("path")
            animated_paths.add(path)
            if path == "translation":
                accessor = document["accessors"][animation["samplers"][channel["sampler"]]["output"]]
                view = document["bufferViews"][accessor["bufferView"]]
                stride = view.get("byteStride", 12)
                first_byte = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
                translations = [struct.unpack_from("<3f", binary_data, first_byte + index * stride)
                                for index in range(accessor["count"])]
                if any(any(abs(current - initial) > 1e-5 for current, initial in zip(value, translations[0]))
                       for value in translations[1:]):
                    raise RuntimeError("NPC locomotion GLB contains varying translation; simulation owns movement.")
    if not animated_paths.issubset({"rotation", "scale", "translation"}):
        raise RuntimeError(f"NPC locomotion contains unsupported animation channels: {sorted(animated_paths)}")
    return names


def main() -> None:
    args = parse_args()
    blend_path = args.blend.expanduser().resolve()
    glb_path = args.glb.expanduser().resolve()
    metadata_path = args.metadata.expanduser().resolve()
    if not blend_path.is_file() or not metadata_path.is_file():
        raise FileNotFoundError(blend_path if not blend_path.is_file() else metadata_path)
    glb_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError(f"No armature in adapted humanoid: {blend_path}")
    clip_names = add_humanoid_locomotion_actions(armature)
    bpy.context.scene.render.fps = 30
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 49
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    exported_names = export_runtime(glb_path, armature)

    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    metadata["animation_clips"] = exported_names
    metadata["locomotion"] = {
        "controller": "NPC simulation movement; clips are in-place",
        "clips": {"idle": "human_idle", "walk": "human_walk", "run": "human_run"},
        "frame_rate": 30,
        "root_motion": False,
    }
    metadata["notes"] = [
        note for note in metadata.get("notes", [])
        if "contains no animation clips" not in note.lower()
    ]
    authored_note = "Includes project-authored, in-place human idle, walk, and run actions; simulation/controller movement remains authoritative."
    if authored_note not in metadata["notes"]:
        metadata["notes"].append(authored_note)
    metadata_path.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"blend": str(blend_path), "glb": str(glb_path), "clips": clip_names,
                      "exported_clips": exported_names,
                      "animated_channels": sorted({"rotation", "translation", "scale"})}, indent=2))


if __name__ == "__main__":
    main()
