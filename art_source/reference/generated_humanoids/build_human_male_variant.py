"""Build the prototype male-presenting human variant from the canonical source rig.

Run with Blender 5.x in background mode. The original source blend is never
modified; all products are written to new male-variant paths.
"""

from pathlib import Path
import math
import json
import struct
import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector


ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art_source/reference/generated_humanoids/human_elf_adapted_rig_v1.blend"
OUT_BLEND = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1.blend"
OUT_GLB = ROOT / "game/assets/humanoids/human/human_male_humanoid_rig_v1.glb"
OUT_PREVIEW = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1_preview.png"

ANCHOR_TARGETS = {
    "anchor_root": ("root", (0.0, 0.036, 0.0)),
    "anchor_foot_l": ("foot_l", (0.053, 0.044, 0.084)),
    "anchor_foot_r": ("foot_r", (-0.053, 0.044, 0.084)),
    "anchor_hand_l": ("hand_l", None),
    "anchor_hand_r": ("hand_r", None),
    "anchor_carry": ("pelvis", (0.0, 0.105, 0.98)),
    "anchor_mouth": ("head", (0.0, -0.135, 1.555)),
    "anchor_anus": ("pelvis", (0.0, 0.085, 0.89)),
    "anchor_genital": ("pelvis", (0.0, -0.125, 0.87)),
    "anchor_urethral": ("pelvis", (0.0, -0.145, 0.875)),
    "anchor_navel": ("spine_01", (0.0, -0.125, 1.055)),
    "anchor_penile": ("pelvis", (0.0, -0.15, 0.87)),
    "anchor_testicular": ("pelvis", (0.0, -0.105, 0.83)),
    "anchor_breast_l": ("spine_03", (0.085, -0.14, 1.25)),
    "anchor_breast_r": ("spine_03", (-0.085, -0.14, 1.25)),
}


def smoothstep(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return t * t * (3.0 - 2.0 * t)


def deform_upper_body():
    body = bpy.data.objects["HumanBody"]
    shirt = bpy.data.objects["HumanShirt"]

    # Keep the base mesh recognizable while making the torso less hourglass-
    # shaped and giving the upper frame a modestly broader adult-male profile.
    for vertex in body.data.vertices:
        x, y, z = vertex.co
        torso_mask = 1.0 - smoothstep(0.17, 0.34, abs(x))
        waist = 1.0 - 0.025 * (1.0 - smoothstep(1.04, 1.18, z)) * smoothstep(0.88, 1.03, z)
        chest = 1.0 + 0.045 * smoothstep(1.19, 1.30, z) * (1.0 - smoothstep(1.43, 1.50, z))
        factor = 1.0 + (waist * chest - 1.0) * torso_mask
        vertex.co.x = x * factor

        # The source was elf-derived; shorten the lateral ear tips while
        # preserving the skull and face profile for the human variant.
        if 1.53 < z < 1.64 and 0.12 < abs(x) < 0.22:
            sign = 1.0 if x >= 0.0 else -1.0
            vertex.co.x = sign * (0.12 + (abs(x) - 0.12) * 0.08)

        # Slightly widen the lower face to avoid the source's very narrow jaw.
        jaw_mask = (1.0 - smoothstep(0.09, 0.16, abs(x))) * smoothstep(1.47, 1.52, z) * (1.0 - smoothstep(1.56, 1.61, z))
        vertex.co.x *= 1.0 + 0.12 * jaw_mask

    brows = bpy.data.objects["HumanBrows"]
    for vertex in brows.data.vertices:
        vertex.co.x *= 1.28
        vertex.co.z = 1.622 + (vertex.co.z - 1.622) * 1.30
        vertex.co.y -= 0.006

    lashes = bpy.data.objects.get("HumanLashes")
    if lashes is not None:
        bpy.data.objects.remove(lashes, do_unlink=True)

    for vertex in shirt.data.vertices:
        x, y, z = vertex.co
        torso_mask = 1.0 - smoothstep(0.20, 0.35, abs(x))
        if z < 1.14:
            factor = 0.91
        elif z < 1.27:
            factor = 0.91 + 0.09 * smoothstep(1.14, 1.27, z)
        else:
            factor = 1.0 + 0.12 * smoothstep(1.27, 1.39, z)
        vertex.co.x = x * (1.0 + (factor - 1.0) * torso_mask)
        if 0.20 < abs(x) < 0.68 and 1.10 < z < 1.48:
            # The reference blouse has soft, gathered sleeves with a flared
            # cuff. Keep a little ease at the shoulder, then taper the cloth
            # toward the wrist so the silhouette follows the underlying arm.
            cuff_taper = smoothstep(0.20, 0.68, abs(x))
            depth_scale = 0.84 - 0.06 * cuff_taper
            height_scale = 0.86 - 0.06 * cuff_taper
            vertex.co.y = 0.036 + (y - 0.036) * depth_scale
            vertex.co.z = 1.38 + (z - 1.38) * height_scale


def make_male_shirt_torso():
    source = bpy.data.objects["HumanShirt"]
    armature = source.parent
    transform = source.matrix_world.copy()
    vertices = []
    faces = []
    weights = []
    segments = 48
    center_y = -0.040
    # Build a clean outer shirt front over the source body contour. The profile
    # stays close at the waist, opens through the chest and shoulders, and
    # falls over the trousers like the layered tunic silhouette in the reference.
    rings = [
        (0.850, 0.132, 0.135, 0.108),
        (0.900, 0.135, 0.139, 0.111),
        (0.960, 0.138, 0.144, 0.114),
        (1.010, 0.134, 0.146, 0.116),
        (1.060, 0.141, 0.155, 0.116),
        (1.115, 0.150, 0.168, 0.120),
        (1.205, 0.159, 0.175, 0.125),
        (1.300, 0.169, 0.182, 0.128),
        (1.375, 0.179, 0.176, 0.126),
        (1.415, 0.160, 0.140, 0.110),
        (1.455, 0.082, 0.105, 0.085),
    ]

    def weights_at(z):
        if z < 1.14:
            blend = smoothstep(1.06, 1.14, z)
            return [("pelvis", 1.0 - blend), ("spine_01", blend)]
        if z < 1.28:
            blend = smoothstep(1.14, 1.28, z)
            return [("spine_01", 1.0 - blend), ("spine_02", blend)]
        blend = smoothstep(1.28, 1.40, z)
        return [("spine_02", 1.0 - blend), ("spine_03", blend)]

    for z, radius_x, front_depth, back_depth in rings:
        for step in range(segments):
            angle = 2.0 * math.pi * step / segments
            sine = math.sin(angle)
            depth = front_depth if sine < 0.0 else back_depth
            hem_weight = 1.0 - smoothstep(0.92, 1.16, z)
            fold = 1.0 + 0.014 * hem_weight * math.cos(angle * 8.0 + z * 2.5)
            hem_wave = 0.010 * hem_weight * math.cos(angle * 6.0)
            vertices.append((radius_x * math.cos(angle) * fold,
                             center_y + sine * depth * fold, z + hem_wave))
            weights.append(weights_at(z))
    for ring_index in range(len(rings) - 1):
        low = ring_index * segments
        high = low + segments
        for step in range(segments):
            nxt = (step + 1) % segments
            faces.append((low + step, low + nxt, high + nxt, high + step))

    mesh = bpy.data.meshes.new("HumanMaleShirtTorsoMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.materials.append(source.data.materials[0])

    torso = bpy.data.objects.new("HumanMaleShirtTorso", mesh)
    bpy.context.collection.objects.link(torso)
    torso.parent = armature
    torso.matrix_world = transform
    group_names = sorted({name for ring in weights for name, _ in ring})
    groups = {name: torso.vertex_groups.new(name=name) for name in group_names}
    for index, ring_weights in enumerate(weights):
        for name, weight in ring_weights:
            if weight > 0.001:
                groups[name].add([index], weight, "REPLACE")
    modifier = torso.modifiers.new("MaleTorsoArmature", "ARMATURE")
    modifier.object = armature


def make_short_hair():
    hair = bpy.data.objects["HumanHair"]
    bm = bmesh.new()
    bm.from_mesh(hair.data)
    adjacency = {vertex: set() for vertex in bm.verts}
    for edge in bm.edges:
        a, b = edge.verts
        adjacency[a].add(b)
        adjacency[b].add(a)

    # The source hairstyle is built from disconnected sculpted hair cards.
    # Keep the scalp/fringe cards and discard whole long-hair cards instead of
    # slicing them at a height that leaves blunt fragments.
    visited = set()
    remove_faces = set()
    for start in bm.verts:
        if start in visited:
            continue
        stack = [start]
        visited.add(start)
        component = []
        while stack:
            vertex = stack.pop()
            component.append(vertex)
            for neighbor in adjacency[vertex]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    stack.append(neighbor)
        if min(vertex.co.z for vertex in component) < 1.45 or len(component) < 300:
            component_set = set(component)
            remove_faces.update(face for face in bm.faces if face.verts[0] in component_set)
    if remove_faces:
        bmesh.ops.delete(bm, geom=list(remove_faces), context="FACES")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(hair.data)
    bm.free()
    hair.data.update()
    for polygon in hair.data.polygons:
        polygon.use_smooth = True
    hair.name = "HumanMaleShortHair"
    hair.data.name = "HumanMaleShortHairMesh"


def make_trousers():
    old = bpy.data.objects["HumanLowerClothing"]
    transform = old.matrix_world.copy()
    armature = old.parent
    armature_modifier = next(mod for mod in old.modifiers if mod.type == "ARMATURE")
    armature_modifier_name = armature_modifier.name
    old_material = old.data.materials[0]
    bpy.data.objects.remove(old, do_unlink=True)

    vertices = []
    faces = []
    groups = []
    sides = 24

    def add_loft(rings, group_for):
        first = len(vertices)
        for ring in rings:
            cx, cy, z, rx, ry = ring
            for step in range(sides):
                angle = (2.0 * math.pi * step) / sides
                vertices.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry, z))
                groups.append(group_for(cx, z))
        for r in range(len(rings) - 1):
            lo = first + r * sides
            hi = lo + sides
            for step in range(sides):
                nxt = (step + 1) % sides
                faces.append((lo + step, lo + nxt, hi + nxt, hi + step))

    for center_x, thigh, calf in ((0.086, "thigh_l", "calf_l"), (-0.086, "thigh_r", "calf_r")):
        leg_rings = [
            (center_x, -0.050, 1.105, 0.112, 0.145),
            (center_x * 0.98, -0.050, 0.950, 0.108, 0.142),
            (center_x * 0.96, -0.050, 0.880, 0.095, 0.138),
            (center_x * 0.94, -0.050, 0.740, 0.086, 0.128),
            (center_x * 0.90, -0.045, 0.600, 0.078, 0.112),
            (center_x * 0.90, -0.040, 0.535, 0.073, 0.102),
            (center_x * 0.80, -0.035, 0.475, 0.067, 0.094),
            (center_x * 0.76, -0.030, 0.422, 0.060, 0.088),
        ]

        def leg_weights(cx, z):
            calf_blend = 1.0 - smoothstep(0.49, 0.57, z)
            pelvis_blend = 0.75 * smoothstep(0.55, 1.10, z)
            return [("pelvis", pelvis_blend), (thigh, (1.0 - calf_blend) - pelvis_blend), (calf, calf_blend)]

        add_loft(leg_rings, leg_weights)

    mesh = bpy.data.meshes.new("HumanTrousersMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    trousers = bpy.data.objects.new("HumanTrousers", mesh)
    bpy.context.collection.objects.link(trousers)
    trousers.parent = armature
    trousers.matrix_world = transform
    for polygon in mesh.polygons:
        polygon.use_smooth = True

    group_names = sorted({name for vertex_groups in groups for name, _ in vertex_groups})
    vertex_groups = {name: trousers.vertex_groups.new(name=name) for name in group_names}
    for index, vertex_groups_with_weights in enumerate(groups):
        for name, weight in vertex_groups_with_weights:
            if weight > 0.001:
                vertex_groups[name].add([index], weight, "REPLACE")

    modifier = trousers.modifiers.new(armature_modifier_name, "ARMATURE")
    modifier.object = armature
    trousers.data.materials.append(old_material)
    material = old_material
    material.diffuse_color = (0.105, 0.145, 0.19, 1.0)
    if material.use_nodes:
        bsdf = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.105, 0.145, 0.19, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.82


def add_semantic_anchors():
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    # Anchors are presentation-only locators parented to the nearest anatomical
    # bone. Their stable names match the semantic IDs declared by the species.
    for name, (bone_name, position) in ANCHOR_TARGETS.items():
        if bone_name not in armature.data.bones:
            raise RuntimeError(f"Anchor {name} refers to missing bone {bone_name}.")
        locator = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(locator)
        locator.empty_display_type = "PLAIN_AXES"
        locator.empty_display_size = 0.045
        locator.parent = armature
        locator.parent_type = "BONE"
        locator.parent_bone = bone_name
        # Bone-parented objects must use a transform relative to the bone's
        # REST matrix. Assigning matrix_world here makes Blender show the
        # desired location but leaves a baked offset that glTF applies again
        # after the bone transform (notably putting hand sockets above the
        # head). Store the target in the bone's rest-local space instead.
        locator.matrix_parent_inverse = Matrix.Identity(4)
        bone = armature.data.bones[bone_name]
        # Blender's bone-parented object origin is at the bone tail; glTF's
        # bone node origin is at the head. Remove that implicit tail offset,
        # then apply the authored rest-world target in bone-local space.
        tail_offset = Matrix.Translation(Vector((0.0, -bone.length, 0.0)))
        if position is None:
            # The hand socket is exactly at the hand bone head.
            locator.matrix_basis = tail_offset
        else:
            bone_world_rest = armature.matrix_world @ bone.matrix_local
            target_world = Matrix.Translation(Vector(position))
            locator.matrix_basis = tail_offset @ bone_world_rest.inverted() @ target_world
    bpy.context.view_layer.update()


def configure_render():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    world = scene.world or bpy.data.worlds.new("Neutral Preview World")
    scene.world = world
    world.color = (0.08, 0.09, 0.11)

    # Set a clear front three-quarter view of the whole character.
    camera_data = bpy.data.cameras.new("MaleVariantPreviewCamera")
    camera = bpy.data.objects.new("MaleVariantPreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (2.7, -5.8, 2.25)
    target = Vector((0.0, 0.0, 0.88))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    scene.camera = camera

    for obj in list(scene.objects):
        if obj.type == "LIGHT":
            bpy.data.objects.remove(obj, do_unlink=True)
    light_data = bpy.data.lights.new("Preview Key", type="AREA")
    light_data.energy = 480.0
    light_data.size = 4.0
    light = bpy.data.objects.new("Preview Key", light_data)
    scene.collection.objects.link(light)
    light.location = (-3.0, -4.0, 5.5)
    light.rotation_euler = (math.radians(28), 0.0, math.radians(-30))


def validate_asset():
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one humanoid armature; found {len(armatures)}.")

    required_bones = {
        "root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
        "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
        "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
        "thigh_l", "calf_l", "foot_l", "toe_l", "thigh_r", "calf_r", "foot_r", "toe_r",
    }
    bones = set(armatures[0].data.bones.keys())
    missing_bones = required_bones - bones
    if missing_bones:
        raise RuntimeError(f"Missing required Humanoid Rig v1 bones: {sorted(missing_bones)}")

    anchor_names = {
        "anchor_root", "anchor_foot_l", "anchor_foot_r", "anchor_hand_l", "anchor_hand_r",
        "anchor_carry", "anchor_mouth", "anchor_anus", "anchor_genital", "anchor_urethral",
        "anchor_navel", "anchor_penile", "anchor_testicular", "anchor_breast_l", "anchor_breast_r",
    }
    for name in anchor_names:
        matches = [obj for obj in bpy.context.scene.objects if obj.name == name]
        if len(matches) != 1 or matches[0].parent_type != "BONE":
            raise RuntimeError(f"Expected exactly one bone-parented semantic anchor named {name}.")

    if not any(action.name == "human_idle" for action in bpy.data.actions):
        raise RuntimeError("The source human_idle animation clip is required.")

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("The male variant must contain at least one mesh.")


def validate_exported_anchors():
    """Check exported GLB world-space sockets against their hand bones."""
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    blob = OUT_GLB.read_bytes()
    if blob[:4] != b"glTF":
        raise RuntimeError("Exported runtime asset is not a binary glTF file.")
    json_length = struct.unpack_from("<I", blob, 12)[0]
    document = json.loads(blob[20:20 + json_length].decode("utf-8"))
    nodes = document.get("nodes", [])
    indices_by_name = {}
    parent_by_index = {}
    for index, node in enumerate(nodes):
        indices_by_name.setdefault(node.get("name", ""), []).append(index)
        for child_index in node.get("children", []):
            parent_by_index[child_index] = index

    def local_matrix(node):
        if "matrix" in node:
            values = node["matrix"]
            return Matrix(tuple(tuple(values[column * 4 + row] for column in range(4))
                                for row in range(4)))
        translation = Vector(node.get("translation", (0.0, 0.0, 0.0)))
        rotation = node.get("rotation", (0.0, 0.0, 0.0, 1.0))
        quaternion = Quaternion((rotation[3], rotation[0], rotation[1], rotation[2]))
        scale = node.get("scale", (1.0, 1.0, 1.0))
        return (Matrix.Translation(translation) @ quaternion.to_matrix().to_4x4()
                @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0)))

    world_cache = {}

    def world_matrix(index):
        if index not in world_cache:
            parent_index = parent_by_index.get(index)
            world_cache[index] = (world_matrix(parent_index) @ local_matrix(nodes[index])
                                  if parent_index is not None else local_matrix(nodes[index]))
        return world_cache[index]

    def unique_node(name):
        matches = indices_by_name.get(name, [])
        if len(matches) != 1:
            raise RuntimeError(f"Expected one exported GLB node named {name}; found {len(matches)}.")
        return matches[0]

    for anchor_name, (bone_name, _) in ANCHOR_TARGETS.items():
        anchor_index = unique_node(anchor_name)
        parent_index = parent_by_index.get(anchor_index)
        if parent_index is None or nodes[parent_index].get("name") != bone_name:
            raise RuntimeError(f"Exported anchor {anchor_name} is not parented to bone node {bone_name}.")

    max_hand_anchor_error = 0.0
    for side in ("l", "r"):
        hand_position = world_matrix(unique_node(f"hand_{side}")).translation
        anchor_position = world_matrix(unique_node(f"anchor_hand_{side}")).translation
        distance = (anchor_position - hand_position).length
        max_hand_anchor_error = max(max_hand_anchor_error, distance)
        if distance > 0.01:
            raise RuntimeError(
                f"anchor_hand_{side} is {distance:.3f} m from its hand bone in exported GLB; expected <= 0.010 m."
            )

    # Verify every explicit world-space target after the exporter has composed
    # the glTF node hierarchy, rather than merely checking names/parenting.
    max_target_error = 0.0
    for name, (_, position) in ANCHOR_TARGETS.items():
        if position is None:
            continue
        blender_position = armature.matrix_world @ Vector(position)
        expected_gltf_position = Vector((blender_position.x, blender_position.z, -blender_position.y))
        actual_gltf_position = world_matrix(unique_node(name)).translation
        error = (actual_gltf_position - expected_gltf_position).length
        max_target_error = max(max_target_error, error)
        if error > 0.02:
            raise RuntimeError(
                f"{name} is {error:.3f} m from its authored world-space target in exported GLB; expected <= 0.020 m."
            )

    print(
        "Exported anchor checks passed: all 15 nodes retain their bone parents; "
        f"hand-anchor offset {max_hand_anchor_error:.3f} m; "
        f"maximum authored world-position error {max_target_error:.4f} m."
    )


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    deform_upper_body()
    make_short_hair()
    make_male_shirt_torso()
    make_trousers()
    add_semantic_anchors()
    validate_asset()

    # Ensure the saved source contains an explicit male variant identity.
    bpy.context.scene["asset_id"] = "human_male_humanoid_rig_v1"
    bpy.context.scene["humanoid_rig_version"] = 1
    bpy.context.scene["source_asset"] = "human_elf_adapted_rig_v1"
    bpy.context.scene["variant"] = "male-presenting adult human prototype"

    OUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    OUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    OUT_PREVIEW.parent.mkdir(parents=True, exist_ok=True)

    configure_render()
    bpy.context.scene.render.filepath = str(OUT_PREVIEW)
    bpy.ops.render.render(write_still=True)

    # Export only the skinned humanoid rig and meshes, excluding preview camera
    # and lights. Preserve the original animation action and source hierarchy.
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [obj for obj in bpy.context.scene.objects if obj.type in {"ARMATURE", "MESH", "EMPTY"}]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next(obj for obj in export_objects if obj.type == "ARMATURE")
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_skins=True,
        export_yup=True,
        export_apply=False,
    )
    validate_exported_anchors()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    print(f"Male variant written: {OUT_BLEND}")
    print(f"Runtime GLB written: {OUT_GLB}")
    print(f"Preview written: {OUT_PREVIEW}")


if __name__ == "__main__":
    main()
