"""Build a first Deluxe Bunny outfit on the project Humanoid Rig v1 bodies.

The project shirt and trousers already have measured fit and canonical skin
weights. The outfit reuses those meshes with a new material palette and adds
the source bunny ears as a head accessory.
"""
from __future__ import annotations

import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[4]
ASSET_ROOT = Path(__file__).resolve().parent
SOURCE_BLEND = ASSET_ROOT / "deluxe_bunny_suit_source_v1.blend"
FIT_PROFILE_PATH = ASSET_ROOT / "deluxe_bunny_suit_fit_v1.json"
PROJECT_FIT_PROFILE = ROOT / "art_source/reference/generated_humanoids/human_humanoid_clothing_fit_v1.json"
PROJECT_FIT = json.loads(PROJECT_FIT_PROFILE.read_text(encoding="utf-8"))
TARGET_HEIGHT_M = 1.75
TARGET_BODY_Y_M = -0.0076
TARGET_FLOOR_Z_M = 0.01

PLAYER_PIECES = {
    "HumanMaleShirt": {
        "runtime_name": "HumanDeluxeBunnySuitJacket",
        "group": "top",
        "material": "Deluxe Bunny Plum Fabric",
        "color": (0.115, 0.027, 0.105, 1.0),
        "roughness": 0.54,
        "metallic": 0.0,
    },
    "HumanMaleShirtButtons": {
        "runtime_name": "HumanDeluxeBunnySuitVest",
        "group": "top",
        "material": "Deluxe Bunny Brass Details",
        "color": (0.48, 0.245, 0.055, 1.0),
        "roughness": 0.34,
        "metallic": 0.55,
    },
    "HumanTrousers": {
        "runtime_name": "HumanDeluxeBunnySuitStockings",
        "group": "legs",
        "material": "Deluxe Bunny Charcoal Fabric",
        "color": (0.025, 0.021, 0.032, 1.0),
        "roughness": 0.62,
        "metallic": 0.0,
    },
}

ACCESSORIES = {
    "SourceBunnyEars": {
        "runtime_name": "HumanDeluxeBunnySuitEars",
        "group": "top",
        "material": "Deluxe Bunny Ears",
        "color": (0.018, 0.012, 0.02, 1.0),
        "roughness": 0.64,
        "metallic": 0.0,
        "rigid_bone": "head",
    },
}

GARMENTS = {
    "player_" + source_name: {
        **specification,
        "source": source_name,
        "kind": "project-fit-mesh-copy",
    }
    for source_name, specification in PLAYER_PIECES.items()
}
GARMENTS.update({
    source_name: {**specification, "source": source_name, "kind": "source-accessory"}
    for source_name, specification in ACCESSORIES.items()
})
SOURCE_OBJECTS = ("SourceBodyReference", *ACCESSORIES.keys())
FIT_REPORT: dict[str, dict] = {}


def _make_source_normalizer(source_body, target_body):
    points = [source_body.matrix_world @ vertex.co for vertex in source_body.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    height = maximum.z - minimum.z
    if height <= 0.1:
        raise RuntimeError("The clothing source body has invalid vertical dimensions.")
    scale = TARGET_HEIGHT_M / height
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    target_inverse = target_body.matrix_world.inverted()

    def normalize(point: Vector) -> Vector:
        normalized = Vector((
            (point.x - center_x) * scale,
            (point.y - center_y) * scale + TARGET_BODY_Y_M,
            (point.z - minimum.z) * scale + TARGET_FLOOR_Z_M,
        ))
        return target_inverse @ normalized

    return normalize


def _build_target_surface(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    index_layer_name = "deluxe_bunny_original_vertex_index"
    index_layer = bm.verts.layers.int.new(index_layer_name)
    for vertex in bm.verts:
        vertex[index_layer] = vertex.index
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    return bm, BVHTree.FromBMesh(bm), index_layer_name


def _barycentric(point: Vector, a: Vector, b: Vector, c: Vector):
    edge_a = b - a
    edge_b = c - a
    offset = point - a
    aa = edge_a.dot(edge_a)
    ab = edge_a.dot(edge_b)
    bb = edge_b.dot(edge_b)
    ap = offset.dot(edge_a)
    bp = offset.dot(edge_b)
    denominator = aa * bb - ab * ab
    if abs(denominator) <= 1e-12:
        return (1.0, 0.0, 0.0)
    weight_b = (bb * ap - ab * bp) / denominator
    weight_c = (aa * bp - ab * ap) / denominator
    weights = [max(0.0, weight_a) for weight_a in (1.0 - weight_b - weight_c, weight_b, weight_c)]
    total = sum(weights)
    return tuple(value / total for value in weights)


def _body_vertex_weights(body):
    group_names = {group.index: group.name for group in body.vertex_groups}
    return [
        {group_names[assignment.group]: assignment.weight for assignment in vertex.groups}
        for vertex in body.data.vertices
    ]


def _weights_at(point, body_bm, body_tree, body_vertex_weights, index_layer_name):
    hit = body_tree.find_nearest(point)
    if hit is None:
        raise RuntimeError("Could not map clothing vertex to the player body's skin surface.")
    face = body_bm.faces[hit[2]]
    if len(face.verts) != 3:
        raise RuntimeError("Clothing weight transfer requires a triangulated body surface.")
    index_layer = body_bm.verts.layers.int[index_layer_name]
    vertices = list(face.verts)
    blend = _barycentric(hit[0], *(vertex.co for vertex in vertices))
    result = {}
    for vertex, amount in zip(vertices, blend):
        original_index = vertex[index_layer]
        for bone_name, weight in body_vertex_weights[original_index].items():
            result[bone_name] = result.get(bone_name, 0.0) + amount * weight
    retained = sorted(
        ((name, weight) for name, weight in result.items() if weight > 1e-5),
        key=lambda item: item[1], reverse=True
    )[:4]
    total = sum(weight for _, weight in retained)
    if total <= 1e-8:
        raise RuntimeError("Clothing weight transfer produced a vertex without bone influence.")
    return {name: weight / total for name, weight in retained}


def _material(specification):
    material = bpy.data.materials.new(specification["material"])
    material.diffuse_color = specification["color"]
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = specification["color"]
    shader.inputs["Roughness"].default_value = specification["roughness"]
    shader.inputs["Metallic"].default_value = specification["metallic"]
    return material


def _clone_player_piece(source_name, specification):
    source = bpy.data.objects.get(source_name)
    if source is None or source.type != "MESH":
        raise RuntimeError(f"The player model is missing its fitted clothing mesh '{source_name}'.")
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = specification["runtime_name"]
    obj.data.name = obj.name + "Mesh"
    bpy.context.collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(_material(specification))
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    obj["fit_source"] = source_name
    obj["fit_profile_id"] = PROJECT_FIT["profile_id"]
    return obj


def _load_source_objects():
    if not SOURCE_BLEND.is_file():
        raise FileNotFoundError(SOURCE_BLEND)
    with bpy.data.libraries.load(str(SOURCE_BLEND), link=False) as (source, target):
        missing = set(SOURCE_OBJECTS) - set(source.objects)
        if missing:
            raise RuntimeError(f"The curated clothing source is missing {sorted(missing)}.")
        target.objects = list(SOURCE_OBJECTS)
    result = {}
    for obj in target.objects:
        if obj is None:
            continue
        bpy.context.collection.objects.link(obj)
        result[obj.name] = obj
    return result


def _add_source_accessory(source_obj, specification, normalize, body, body_bm,
                          body_tree, body_vertex_weights, body_index_layer, armature):
    evaluated = source_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    source_mesh = evaluated.to_mesh()
    if source_mesh is None or not source_mesh.vertices or not source_mesh.polygons:
        raise RuntimeError(f"Source accessory {source_obj.name} evaluated to empty geometry.")
    try:
        mesh = source_mesh.copy()
        mesh.materials.clear()
        obj = bpy.data.objects.new(specification["runtime_name"], mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = body.parent
        obj.matrix_world = body.matrix_world.copy()
        mesh.materials.append(_material(specification))
        groups = {group.name: obj.vertex_groups.new(name=group.name) for group in body.vertex_groups}
        rigid_bone = specification.get("rigid_bone")
        for vertex in mesh.vertices:
            vertex.co = normalize(source_obj.matrix_world @ vertex.co)
        mesh.update()
        for index, vertex in enumerate(mesh.vertices):
            weights = ({rigid_bone: 1.0} if rigid_bone else _weights_at(
                vertex.co, body_bm, body_tree, body_vertex_weights, body_index_layer
            ))
            for bone_name, weight in weights.items():
                if bone_name not in groups:
                    raise RuntimeError(f"Source accessory references unknown rig bone '{bone_name}'.")
                groups[bone_name].add([index], weight, "REPLACE")
        skin = obj.modifiers.new("Humanoid Rig v1 accessory skin", "ARMATURE")
        skin.object = armature
        skin.use_vertex_groups = True
        for polygon in mesh.polygons:
            polygon.material_index = 0
            polygon.use_smooth = True
        obj["fit_source"] = source_obj.name
        obj["fit_profile_id"] = PROJECT_FIT["profile_id"]
        return obj
    finally:
        evaluated.to_mesh_clear()


def add_to_player_model(sex: str, body, armature) -> None:
    if sex not in {"male", "female"}:
        raise ValueError(f"Unsupported player body variant '{sex}'.")
    created = []
    for source_name, specification in PLAYER_PIECES.items():
        created.append(_clone_player_piece(source_name, specification))

    source = _load_source_objects()
    normalize = _make_source_normalizer(source["SourceBodyReference"], body)
    target_bm, target_tree, index_layer = _build_target_surface(body.data)
    vertex_weights = _body_vertex_weights(body)
    try:
        for source_name, specification in ACCESSORIES.items():
            created.append(_add_source_accessory(
                source[source_name], specification, normalize, body, target_bm,
                target_tree, vertex_weights, index_layer, armature
            ))
    finally:
        target_bm.free()
        for obj in source.values():
            bpy.data.objects.remove(obj, do_unlink=True)
    FIT_REPORT[sex] = {
        "target_body_mesh": body.name,
        "target_height_m": body.dimensions.z,
        "fit_reference_profile_id": PROJECT_FIT["profile_id"],
        "garments": [{
            "mesh": obj.name,
            "source": obj.get("fit_source", ""),
            "kind": (
                "project-fit-mesh-copy"
                if obj.get("fit_source", "") in PLAYER_PIECES
                else "source-accessory"
            ),
            "skinned": True,
        } for obj in created],
        "runtime_nodes": [obj.name for obj in created],
    }
    print(f"Built Deluxe Bunny outfit on {sex} player using {len(created)} skinned meshes.")


def write_fit_profile() -> None:
    profile = {
        "profile_id": "deluxe_bunny_suit_fit_v1",
        "status": "First playable outfit fitted to Humanoid Rig v1 male and female player bodies",
        "source_asset": {
            "pack": "RyanReos Deluxe Bunny Suit",
            "archive": "Repo/Assets/Models/cloth/RyanReos_DeluxeBunnysuit.zip",
            "source_scene": "Blender/DeluxeBunnySuit.blend",
            "curated_scene": "deluxe_bunny_suit_source_v1.blend",
            "measurement_units": "meters",
        },
        "fit_reference_profile_id": PROJECT_FIT["profile_id"],
        "fit_method": (
            "Use copies of the project-measured player shirt and trousers for the "
            "garment surfaces, preserving their verified outer fit and Humanoid Rig v1 "
            "weights. Adapt the source bunny ears to 1.75 m and attach them to the "
            "canonical head bone. The source jacket, collar, cuffs, vest, leotard, and stockings "
            "are retained in the reference scene but not used as runtime garment surfaces "
            "because their shapes do not fit both player base meshes cleanly."
        ),
        "runtime_garments": GARMENTS,
        "measurements_by_player_variant": FIT_REPORT,
    }
    FIT_PROFILE_PATH.write_text(json.dumps(profile, indent=2) + "\n", encoding="utf-8")
    print(f"Clothing fit profile written: {FIT_PROFILE_PATH}")


def validate_exported_clothing(glb_path: Path, sex: str, expected_names=None) -> None:
    blob = glb_path.read_bytes()
    if blob[:4] != b"glTF":
        raise RuntimeError(f"{sex} player asset is not a GLB.")
    json_length = int.from_bytes(blob[12:16], "little")
    document = json.loads(blob[20:20 + json_length].decode("utf-8"))
    nodes = {}
    for node in document.get("nodes", []):
        nodes.setdefault(node.get("name", ""), []).append(node)
    specifications = [
        specification for specification in GARMENTS.values()
        if expected_names is None or specification["runtime_name"] in expected_names
    ]
    if expected_names is not None and len(specifications) != len(expected_names):
        raise RuntimeError(f"{sex} clothing export requested an unknown garment node.")
    for specification in specifications:
        name = specification["runtime_name"]
        matched = nodes.get(name, [])
        if len(matched) != 1:
            raise RuntimeError(f"Expected one {sex} player garment node '{name}', found {len(matched)}.")
        node = matched[0]
        if "mesh" not in node or "skin" not in node:
            raise RuntimeError(f"{sex} player garment '{name}' is missing mesh or skin data.")
        for primitive in document["meshes"][node["mesh"]]["primitives"]:
            attributes = primitive.get("attributes", {})
            if "JOINTS_0" not in attributes or "WEIGHTS_0" not in attributes:
                raise RuntimeError(f"{sex} player garment '{name}' lost its skin weights in GLB export.")
