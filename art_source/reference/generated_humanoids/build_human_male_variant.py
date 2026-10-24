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

    # The source body is intentionally retained, but its proportions need to
    # read clearly at the game's normal camera distance. Broaden the chest and
    # shoulder transition, reduce the waist pinch, and widen/project the jaw.
    for vertex in body.data.vertices:
        x, y, z = vertex.co
        torso_mask = 1.0 - smoothstep(0.16, 0.31, abs(x))
        waist = 1.0 - 0.07 * smoothstep(0.88, 0.98, z) * (1.0 - smoothstep(1.00, 1.12, z))
        chest = 1.0 + 0.22 * smoothstep(1.12, 1.27, z) * (1.0 - smoothstep(1.40, 1.48, z))
        factor = 1.0 + (waist * chest - 1.0) * torso_mask
        vertex.co.x = x * factor

        # Flatten the paired chest forms in the elf-derived base mesh. This
        # keeps the unclothed body silhouette masculine as well as the shirt.
        chest_zone = smoothstep(1.11, 1.20, z) * (1.0 - smoothstep(1.32, 1.40, z))
        breast_lobe = math.exp(-((abs(x) - 0.085) / 0.052) ** 2)
        front_surface = smoothstep(0.055, 0.14, -y)
        vertex.co.y += 0.055 * chest_zone * breast_lobe * front_surface

        # The source was elf-derived; shorten the lateral ear tips while
        # preserving the skull and face profile for the human variant.
        if 1.53 < z < 1.64 and 0.12 < abs(x) < 0.22:
            sign = 1.0 if x >= 0.0 else -1.0
            vertex.co.x = sign * (0.12 + (abs(x) - 0.12) * 0.08)

        jaw_mask = (1.0 - smoothstep(0.07, 0.145, abs(x))) * smoothstep(1.46, 1.52, z) * (1.0 - smoothstep(1.57, 1.64, z))
        vertex.co.x *= 1.0 + 0.24 * jaw_mask
        if y < -0.055:
            vertex.co.y -= 0.016 * jaw_mask * smoothstep(0.055, 0.12, -y)

    brows = bpy.data.objects["HumanBrows"]
    brow_center_z = sum(vertex.co.z for vertex in brows.data.vertices) / max(1, len(brows.data.vertices))
    for vertex in brows.data.vertices:
        vertex.co.x *= 1.18
        vertex.co.z = brow_center_z + (vertex.co.z - brow_center_z) * 1.55
        vertex.co.y -= 0.006

    eyes = bpy.data.objects.get("HumanEyes")
    if eyes is not None and eyes.data.vertices:
        center = sum((vertex.co for vertex in eyes.data.vertices), Vector()) / len(eyes.data.vertices)
        for vertex in eyes.data.vertices:
            vertex.co = center + (vertex.co - center) * 0.90

    lashes = bpy.data.objects.get("HumanLashes")
    if lashes is not None:
        bpy.data.objects.remove(lashes, do_unlink=True)

    # Keep the source sleeves' skinning so they follow the idle and locomotion
    # poses. Reduce the gathered blouse volume and taper the cuff while leaving
    # enough ease for the shoulders and elbows to move without clipping.
    for vertex in shirt.data.vertices:
        x, y, z = vertex.co
        if 0.20 < abs(x) < 0.68 and 1.10 < z < 1.48:
            cuff_taper = smoothstep(0.20, 0.68, abs(x))
            depth_scale = 0.80 - 0.08 * cuff_taper
            height_scale = 0.82 - 0.08 * cuff_taper
            vertex.co.y = 0.036 + (y - 0.036) * depth_scale
            vertex.co.z = 1.38 + (z - 1.38) * height_scale


def make_male_shirt_torso():
    source = bpy.data.objects["HumanShirt"]
    armature = source.parent
    cloth_material = source.data.materials[0]

    vertices = []
    faces = []
    weights = []
    segments = 48
    center_y = -0.015
    # A shaped, waist-length work shirt overlays the source's animated sleeves.
    # Its chest and shoulder width is intentionally more pronounced than the
    # waist; the offset front/back profile follows the body without a rigid orb.
    rings = [
        (1.045, 0.137, 0.123, 0.116),
        (1.060, 0.140, 0.125, 0.118),
        (1.085, 0.143, 0.127, 0.120),
        (1.105, 0.141, 0.125, 0.118),
        (1.125, 0.151, 0.134, 0.124),
        (1.205, 0.190, 0.160, 0.145),
        (1.295, 0.215, 0.174, 0.155),
        (1.365, 0.225, 0.176, 0.155),
        (1.405, 0.194, 0.154, 0.137),
        (1.438, 0.102, 0.105, 0.096),
    ]

    def weights_at(z):
        if z < 1.16:
            blend = smoothstep(1.08, 1.16, z)
            return [("pelvis", 1.0 - blend), ("spine_01", blend)]
        if z < 1.31:
            blend = smoothstep(1.16, 1.31, z)
            return [("spine_01", 1.0 - blend), ("spine_02", blend)]
        blend = smoothstep(1.31, 1.41, z)
        return [("spine_02", 1.0 - blend), ("spine_03", blend)]

    for z, radius_x, front_depth, back_depth in rings:
        for step in range(segments):
            angle = 2.0 * math.pi * step / segments
            sine = math.sin(angle)
            depth = front_depth if sine < 0.0 else back_depth
            hem_weight = 1.0 - smoothstep(1.06, 1.14, z)
            fold = 1.0 + 0.008 * hem_weight * math.cos(angle * 8.0 + z * 2.5)
            hem_wave = 0.004 * hem_weight * math.cos(angle * 6.0)
            vertices.append((radius_x * math.cos(angle) * fold,
                             center_y + sine * depth * fold, z + hem_wave))
            weights.append(weights_at(z))
    for ring_index in range(len(rings) - 1):
        low = ring_index * segments
        high = low + segments
        for step in range(segments):
            nxt = (step + 1) % segments
            faces.append((low + step, low + nxt, high + nxt, high + step))

    mesh = bpy.data.meshes.new("HumanMaleWorkShirtMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.materials.append(cloth_material)

    torso = bpy.data.objects.new("HumanMaleWorkShirt", mesh)
    bpy.context.collection.objects.link(torso)
    torso.parent = armature
    torso.matrix_world = armature.matrix_world.copy()
    group_names = sorted({name for ring in weights for name, _ in ring})
    groups = {name: torso.vertex_groups.new(name=name) for name in group_names}
    for index, ring_weights in enumerate(weights):
        for name, weight in ring_weights:
            if weight > 0.001:
                groups[name].add([index], weight, "REPLACE")
    modifier = torso.modifiers.new("MaleWorkShirtArmature", "ARMATURE")
    modifier.object = armature

    cloth_material.name = "Human_Male_Workwear"
    cloth_material.diffuse_color = (0.24, 0.29, 0.16, 1.0)
    if cloth_material.node_tree:
        bsdf = next((node for node in cloth_material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.24, 0.29, 0.16, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.9


def make_shirt_buttons():
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    center_y = -0.015
    shirt_profile = [
        (1.125, 0.134), (1.205, 0.160), (1.295, 0.174),
        (1.365, 0.176), (1.405, 0.154), (1.438, 0.105),
    ]

    def front_y(z):
        for (z0, d0), (z1, d1) in zip(shirt_profile, shirt_profile[1:]):
            if z0 <= z <= z1:
                t = (z - z0) / (z1 - z0)
                return center_y - (d0 + (d1 - d0) * t)
        raise ValueError(f"Button height {z} is outside the shirt profile.")

    vertices = []
    faces = []
    button_heights = (1.18, 1.225, 1.27, 1.315, 1.36)
    sides = 12
    for z in button_heights:
        outer_front_y = front_y(z) - 0.006
        back_y = outer_front_y + 0.004
        front_y_value = outer_front_y
        start = len(vertices)
        for y in (back_y, front_y_value):
            for step in range(sides):
                angle = 2.0 * math.pi * step / sides
                vertices.append((math.cos(angle) * 0.008, y, z + math.sin(angle) * 0.008))
        for step in range(sides):
            nxt = (step + 1) % sides
            faces.append((start + step, start + nxt, start + sides + nxt, start + sides + step))
        faces.append(tuple(start + sides + step for step in range(sides)))
        faces.append(tuple(start + step for step in reversed(range(sides))))

    mesh = bpy.data.meshes.new("HumanMaleShirtButtonsMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    buttons = bpy.data.objects.new("HumanMaleShirtButtons", mesh)
    bpy.context.collection.objects.link(buttons)
    buttons.parent = armature
    buttons.matrix_world = armature.matrix_world.copy()
    spine_02 = buttons.vertex_groups.new(name="spine_02")
    spine_03 = buttons.vertex_groups.new(name="spine_03")
    for index, vertex in enumerate(vertices):
        blend = smoothstep(1.28, 1.36, vertex[2])
        if blend < 1.0:
            spine_02.add([index], 1.0 - blend, "REPLACE")
        if blend > 0.0:
            spine_03.add([index], blend, "REPLACE")
    modifier = buttons.modifiers.new("ShirtButtonsArmature", "ARMATURE")
    modifier.object = armature
    material = bpy.data.materials.new("Human_Male_Button_Brass")
    material.diffuse_color = (0.32, 0.22, 0.08, 1.0)
    material.use_nodes = True
    bsdf = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.32, 0.22, 0.08, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.35
    bsdf.inputs["Roughness"].default_value = 0.58
    mesh.materials.append(material)


def make_leather_belt():
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    center_y = -0.015
    segments = 48
    vertices = []
    faces = []
    for z in (1.055, 1.105):
        for step in range(segments):
            angle = 2.0 * math.pi * step / segments
            sine = math.sin(angle)
            depth = 0.138 if sine < 0.0 else 0.126
            vertices.append((0.154 * math.cos(angle), center_y + sine * depth, z))
    for step in range(segments):
        nxt = (step + 1) % segments
        faces.append((step, nxt, segments + nxt, segments + step))
    mesh = bpy.data.meshes.new("HumanMaleBeltMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    belt = bpy.data.objects.new("HumanMaleLeatherBelt", mesh)
    bpy.context.collection.objects.link(belt)
    belt.parent = armature
    belt_groups = [belt.vertex_groups.new(name="pelvis"), belt.vertex_groups.new(name="spine_01")]
    for index in range(len(vertices)):
        belt_groups[0].add([index], 0.8, "REPLACE")
        belt_groups[1].add([index], 0.2, "REPLACE")
    modifier = belt.modifiers.new("BeltArmature", "ARMATURE")
    modifier.object = armature
    material = bpy.data.materials.new("Human_Male_Leather")
    material.diffuse_color = (0.19, 0.075, 0.027, 1.0)
    material.use_nodes = True
    bsdf = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.19, 0.075, 0.027, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    mesh.materials.append(material)

    buckle_vertices = [
        (-0.030, -0.158, 1.065), (0.030, -0.158, 1.065),
        (0.030, -0.158, 1.095), (-0.030, -0.158, 1.095),
        (-0.030, -0.166, 1.065), (0.030, -0.166, 1.065),
        (0.030, -0.166, 1.095), (-0.030, -0.166, 1.095),
    ]
    buckle_faces = [
        (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
        (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0),
    ]
    buckle_mesh = bpy.data.meshes.new("HumanMaleBuckleMesh")
    buckle_mesh.from_pydata(buckle_vertices, [], buckle_faces)
    buckle_mesh.update()
    buckle = bpy.data.objects.new("HumanMaleBeltBuckle", buckle_mesh)
    bpy.context.collection.objects.link(buckle)
    buckle.parent = armature
    buckle_group = buckle.vertex_groups.new(name="pelvis")
    buckle_group.add(list(range(len(buckle_vertices))), 1.0, "REPLACE")
    buckle_modifier = buckle.modifiers.new("BuckleArmature", "ARMATURE")
    buckle_modifier.object = armature
    metal = bpy.data.materials.new("Human_Male_Buckle")
    metal.diffuse_color = (0.56, 0.38, 0.12, 1.0)
    metal.use_nodes = True
    metal_bsdf = next(node for node in metal.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    metal_bsdf.inputs["Base Color"].default_value = (0.56, 0.38, 0.12, 1.0)
    metal_bsdf.inputs["Metallic"].default_value = 0.55
    metal_bsdf.inputs["Roughness"].default_value = 0.48
    buckle_mesh.materials.append(metal)


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
    crop_faces = [
        face for face in bm.faces
        if face.calc_center_median().y > -0.02
        and face.calc_center_median().z < 1.67
    ]
    if crop_faces:
        bmesh.ops.delete(bm, geom=crop_faces, context="FACES")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(hair.data)
    bm.free()
    hair.data.update()
    # Trim the back into a short crop instead of preserving the source model's
    # small tied-back silhouette. Keep the crown and face-framing fringe.
    for vertex in hair.data.vertices:
        x, y, z = vertex.co
        if y > 0.02 and z < 1.70:
            back_mask = smoothstep(0.02, 0.10, y) * (1.0 - smoothstep(1.54, 1.70, z))
            vertex.co.y -= 0.045 * back_mask
            vertex.co.z += 0.035 * back_mask
    for polygon in hair.data.polygons:
        polygon.use_smooth = True
    hair.name = "HumanMaleShortHair"
    hair.data.name = "HumanMaleShortHairMesh"


def make_trousers():
    source_clothing = bpy.data.objects["HumanLowerClothing"]
    body = bpy.data.objects["HumanBody"]
    bpy.data.objects.remove(source_clothing, do_unlink=True)

    # Build trousers from the actual skinned body surface. Independent leg
    # tubes drift away from the thighs in the idle pose and during animation.
    trousers = body.copy()
    trousers.data = body.data.copy()
    trousers.name = "HumanTrousers"
    trousers.data.name = "HumanTrousersMesh"
    bpy.context.collection.objects.link(trousers)

    bm = bmesh.new()
    bm.from_mesh(trousers.data)
    bm.verts.index_update()
    garment_faces = [
        face for face in bm.faces
        if 0.30 <= face.calc_center_median().z <= 1.15
        and abs(face.calc_center_median().x) <= 0.22
    ]
    if len(garment_faces) < 250:
        bm.free()
        raise RuntimeError(f"Trousers body-surface selection is unexpectedly small: {len(garment_faces)} faces.")
    garment_face_set = set(garment_faces)
    bmesh.ops.delete(bm, geom=[face for face in bm.faces if face not in garment_face_set], context="FACES")
    loose_vertices = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose_vertices:
        bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    # Expand the garment radially from each leg/pelvis centerline. The source
    # mesh has a few inconsistent normals, so using them directly can push cloth
    # into the skin and make the body show through.
    for vertex in bm.verts:
        axis_x = (0.085 if vertex.co.x >= 0.0 else -0.085) if vertex.co.z < 0.90 else 0.0
        outward_hint = Vector((vertex.co.x - axis_x, vertex.co.y + 0.02, 0.0))
        if outward_hint.length_squared > 0.0001:
            vertex.co += outward_hint.normalized() * 0.018
    bm.to_mesh(trousers.data)
    bm.free()
    trousers.data.update()

    for polygon in trousers.data.polygons:
        polygon.use_smooth = True

    material = bpy.data.materials.new("Human_Male_Trousers")
    material.diffuse_color = (0.075, 0.095, 0.09, 1.0)
    material.use_nodes = True
    bsdf = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.075, 0.095, 0.09, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.82
    trousers.data.materials.clear()
    trousers.data.materials.append(material)

    # The copied body object keeps its armature modifier and vertex groups.
    if not any(mod.type == "ARMATURE" and mod.object == trousers.parent for mod in trousers.modifiers):
        raise RuntimeError("Generated trousers must retain the body's armature skinning.")
    required_groups = {"pelvis", "thigh_l", "calf_l", "thigh_r", "calf_r"}
    missing_groups = required_groups.difference(group.name for group in trousers.vertex_groups)
    if missing_groups:
        raise RuntimeError(f"Generated trousers are missing body skin weights: {sorted(missing_groups)}")


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
    make_shirt_buttons()
    make_leather_belt()
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
