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
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art_source/reference/generated_humanoids/human_elf_adapted_rig_v1.blend"
MALE_BASE_SOURCE = ROOT / "art_source/reference/generated_humanoids/blender_male_base_source_v1.blend"
OUT_BLEND = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1.blend"
OUT_GLB = ROOT / "game/assets/humanoids/human/human_male_humanoid_rig_v1.glb"
OUT_PREVIEW = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1_preview.png"
OUT_FRONT_PREVIEW = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1_front.png"
OUT_SIDE_PREVIEW = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1_side.png"
OUT_BACK_PREVIEW = ROOT / "art_source/reference/generated_humanoids/human_male_humanoid_rig_v1_back.png"
FIT_PROFILE_PATH = ROOT / "art_source/reference/generated_humanoids/human_humanoid_clothing_fit_v1.json"
FIT_PROFILE = json.loads(FIT_PROFILE_PATH.read_text(encoding="utf-8"))
TARGET_HEIGHT_M = 1.75

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


def bind_body_to_canonical_rig(body, armature):
    """Bind the normalized male base to Humanoid Rig v1 and normalize its skin weights."""
    world_matrix = body.matrix_world.copy()
    body.parent = None
    body.matrix_world = world_matrix
    for group in list(body.vertex_groups):
        body.vertex_groups.remove(group)
    for modifier in list(body.modifiers):
        if modifier.type == "ARMATURE":
            body.modifiers.remove(modifier)

    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    result = bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if "FINISHED" not in result:
        raise RuntimeError("Blender could not bind the male base to Humanoid Rig v1.")

    groups_by_index = {group.index: group for group in body.vertex_groups}
    zero_weight_vertices = 0
    for vertex in body.data.vertices:
        influences = sorted(
            ((assignment.group, assignment.weight) for assignment in vertex.groups),
            key=lambda item: item[1],
            reverse=True,
        )[:4]
        total = sum(weight for _, weight in influences)
        if total <= 1e-8:
            zero_weight_vertices += 1
            continue
        # Keep a predictable four-influence limit and normalized values for
        # runtime importers while preserving Blender's rig-aware heat weights.
        for group_index, weight in influences:
            group = groups_by_index[group_index]
            group.add([vertex.index], weight / total, "REPLACE")
        retained = {group_index for group_index, _ in influences}
        for assignment in list(vertex.groups):
            if assignment.group not in retained:
                groups_by_index[assignment.group].remove([vertex.index])

    # The heat solver can favor terminal finger helpers and leave the palm on
    # the forearm. Give the palm a direct hand-bone influence, limited to that
    # bone segment so finger articulation remains independent.
    for side in ("l", "r"):
        hand_bone = armature.data.bones[f"hand_{side}"]
        start = armature.matrix_world @ hand_bone.head_local
        end = armature.matrix_world @ hand_bone.tail_local
        axis = end - start
        length_squared = axis.length_squared
        hand_group = body.vertex_groups[f"hand_{side}"]
        for vertex in body.data.vertices:
            point = body.matrix_world @ vertex.co
            along = (point - start).dot(axis) / length_squared
            if not 0.0 <= along <= 1.0:
                continue
            closest = start + axis * along
            distance = (point - closest).length
            hand_weight = 0.75 * (1.0 - smoothstep(0.025, 0.060, distance))
            if hand_weight <= 0.05:
                continue
            current = {assignment.group: assignment.weight for assignment in vertex.groups}
            current[hand_group.index] = max(current.get(hand_group.index, 0.0), hand_weight)
            strongest = sorted(current.items(), key=lambda item: item[1], reverse=True)[:4]
            total = sum(weight for _, weight in strongest)
            for group_index, weight in strongest:
                groups_by_index[group_index].add([vertex.index], weight / total, "REPLACE")
            retained = {group_index for group_index, _ in strongest}
            for assignment in list(vertex.groups):
                if assignment.group not in retained:
                    groups_by_index[assignment.group].remove([vertex.index])

    if zero_weight_vertices:
        raise RuntimeError(f"Automatic rig binding left {zero_weight_vertices} male body vertices unweighted.")
    if len(body.vertex_groups) != len(armature.data.bones):
        raise RuntimeError(
            f"Automatic rig binding created {len(body.vertex_groups)} groups for "
            f"{len(armature.data.bones)} canonical bones."
        )
    print(
        f"Bound male base with Blender automatic weights: {len(body.data.vertices)} vertices, "
        f"maximum 4 normalized influences per vertex, {len(body.vertex_groups)} Humanoid Rig v1 groups."
    )


def make_project_material(name, color, roughness=0.82, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return material


def create_head_weighted_object(name, mesh, armature, body, material):
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = armature
    obj.matrix_world = body.matrix_world.copy()
    group = obj.vertex_groups.new(name="head")
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new(f"{name}Armature", "ARMATURE")
    modifier.object = armature
    mesh.materials.clear()
    mesh.materials.append(material)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def make_uv_sphere_mesh(name, center, radii, segments=16, rings=8):
    vertices = []
    faces = []
    for ring in range(rings + 1):
        latitude = -math.pi / 2.0 + math.pi * ring / rings
        ring_radius = math.cos(latitude)
        z = math.sin(latitude)
        for step in range(segments):
            longitude = 2.0 * math.pi * step / segments
            vertices.append((
                center.x + radii[0] * ring_radius * math.cos(longitude),
                center.y + radii[1] * ring_radius * math.sin(longitude),
                center.z + radii[2] * z,
            ))
    for ring in range(rings):
        for step in range(segments):
            next_step = (step + 1) % segments
            low = ring * segments
            high = (ring + 1) * segments
            faces.append((low + step, low + next_step, high + next_step, high + step))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def deform_upper_body(fit_rig_to_body=None):
    """Replace the elf body with Blender's CC0 male base and retain Rig v1."""
    body = bpy.data.objects["HumanBody"]
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    with bpy.data.libraries.load(str(MALE_BASE_SOURCE), link=False) as (source_data, loaded_data):
        requested = ("BlenderMaleBodyBase", "BlenderMaleEye.L", "BlenderMaleEye.R")
        missing = set(requested) - set(source_data.objects)
        if missing:
            raise RuntimeError(f"Male base source is missing objects: {sorted(missing)}")
        loaded_data.objects = list(requested)
    source_body, source_eye_l, source_eye_r = loaded_data.objects
    for source_obj in loaded_data.objects:
        bpy.context.collection.objects.link(source_obj)
    bpy.context.view_layer.update()

    original_mesh = source_body.data
    source_world = [source_body.matrix_world @ vertex.co for vertex in original_mesh.vertices]
    minimum = Vector(tuple(min(point[axis] for point in source_world) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in source_world) for axis in range(3)))
    source_height = maximum.z - minimum.z
    if source_height <= 0.1:
        raise RuntimeError(f"Blender male source has invalid height {source_height:.3f} m.")
    scale = TARGET_HEIGHT_M / source_height
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    target_body_y = -0.0076
    target_foot_z = 0.01
    reference_inverse = body.matrix_world.inverted()
    def normalize_source_point(point, lift_a_pose_arm=False):
        normalized = Vector((
            (point.x - center_x) * scale,
            (point.y - center_y) * scale + target_body_y,
            (point.z - minimum.z) * scale + target_foot_z,
        ))
        arm_region = (
            (normalized.z > 1.10 and abs(normalized.x) > 0.12)
            or (normalized.z > 0.70 and abs(normalized.x) > 0.24)
        )
        if lift_a_pose_arm and arm_region:
            side = 1.0 if normalized.x > 0.0 else -1.0
            # The Blender base male is posed with relaxed, lowered arms while
            # Humanoid Rig v1 rests in an A/T-pose. Complete the pose correction
            # across the upper arm instead of tapering it to zero at the elbow;
            # the wider ramp left the upper arm down while lifting only the
            # forearm, visibly kinking the limb after skinning.
            influence = smoothstep(0.12, 0.18, abs(normalized.x))
            angle = -side * math.radians(58.0) * influence
            pivot = Vector((side * 0.16, target_body_y, 1.38))
            normalized = pivot + Matrix.Rotation(angle, 4, "Y") @ (normalized - pivot)
        return reference_inverse @ normalized

    target_positions = [normalize_source_point(point, lift_a_pose_arm=fit_rig_to_body is None)
                        for point in source_world]
    body_mesh = original_mesh.copy()
    for vertex, position in zip(body_mesh.vertices, target_positions):
        vertex.co = position
    body_mesh.name = "BlenderMaleBodyBaseMesh"
    body.data = body_mesh
    if fit_rig_to_body is not None:
        fit_rig_to_body(body, armature)
    bind_body_to_canonical_rig(body, armature)

    skin = make_project_material("Human_Male_Skin", (0.47, 0.27, 0.18), roughness=0.87)
    body.data.materials.clear()
    body.data.materials.append(skin)
    for polygon in body.data.polygons:
        polygon.use_smooth = True

    eye_white = make_project_material("Human_Male_Eye_White", (0.80, 0.77, 0.68), roughness=0.38)
    iris = make_project_material("Human_Male_Iris", (0.13, 0.065, 0.028), roughness=0.35)
    pupil = make_project_material("Human_Male_Pupil", (0.012, 0.009, 0.007), roughness=0.24)
    eye_centers = []
    for side, source_eye in (("l", source_eye_l), ("r", source_eye_r)):
        eye_positions = [normalize_source_point(source_eye.matrix_world @ vertex.co)
                         for vertex in source_eye.data.vertices]
        eye_mesh = source_eye.data.copy()
        for vertex, position in zip(eye_mesh.vertices, eye_positions):
            vertex.co = position
        eye = create_head_weighted_object(f"HumanMaleEyeBall_{side}", eye_mesh, armature, body, eye_white)
        eye_centers.append(sum(eye_positions, Vector()) / len(eye_positions))
        bpy.data.objects.remove(source_eye, do_unlink=True)
        center = eye_centers[-1]
        iris_center = Vector((center.x, center.y - 0.0098, center.z))
        pupil_center = Vector((center.x, center.y - 0.0115, center.z))
        iris_obj = create_head_weighted_object(
            f"HumanMaleIris_{side}", make_uv_sphere_mesh(f"HumanMaleIrisMesh_{side}", iris_center, (0.0048, 0.0022, 0.0052)), armature, body, iris
        )
        pupil_obj = create_head_weighted_object(
            f"HumanMalePupil_{side}", make_uv_sphere_mesh(f"HumanMalePupilMesh_{side}", pupil_center, (0.0020, 0.0012, 0.0025)), armature, body, pupil
        )
        del eye, iris_obj, pupil_obj

    # The imported body and eyes now live in project-owned mesh data; clean out
    # every unused object from the temporary source library.
    bpy.data.objects.remove(source_body, do_unlink=True)
    for old_name in ("HumanHair", "HumanBrows", "HumanEyes", "HumanLashes", "HumanShirt", "HumanBoots"):
        old = bpy.data.objects.get(old_name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    scene = bpy.context.scene
    scene["male_base_source"] = "Blender Human Base Meshes v1.4.1 / GEO-body_male_realistic"
    scene["male_base_license"] = "CC0 1.0 Universal"
    scene["male_base_source_sha256"] = "811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515"

def mesh_group_weight(vertex, group_names, wanted_groups):
    """Return a vertex's total influence from a small semantic group set."""
    return sum(
        assignment.weight
        for assignment in vertex.groups
        if group_names[assignment.group] in wanted_groups
    )


def interpolate_profile(points, value):
    """Linearly interpolate measured apparel clearance at a body-height ratio."""
    if value <= points[0][0]:
        return points[0][1]
    for (low_x, low_y), (high_x, high_y) in zip(points, points[1:]):
        if value <= high_x:
            t = (value - low_x) / (high_x - low_x)
            return low_y + (high_y - low_y) * t
    return points[-1][1]


def shirt_clearance_for_weights(position, normal, body_min_z, body_height, weights_by_name):
    """Resolve shell clearance from measured fit samples and interpolated weights."""
    profile = FIT_PROFILE["applied_profile"]["shirt_clearance_m"]
    ratios = FIT_PROFILE["applied_profile"]["body_height_ratios"]
    height_ratio = (position.z - body_min_z) / body_height
    lower_center = (ratios["shirt_lower_chest_start"] + ratios["shirt_upper_chest_start"]) / 2.0
    upper_center = (ratios["shirt_upper_chest_start"] + ratios["shirt_collar"]) / 2.0
    if normal.x * normal.x > normal.y * normal.y:
        direction = "side"
    else:
        direction = "front" if normal.y < 0.0 else "back"

    if direction == "front":
        lower = profile["torso_front_lower_chest"]
        upper = profile["torso_front_upper_chest"]
    elif direction == "back":
        lower = profile["torso_back_lower_chest"]
        upper = profile["torso_back_upper_chest"]
    else:
        lower = profile["torso_side_lower_chest"]
        upper = profile["torso_side_upper_chest"]
    torso_clearance = interpolate_profile(
        [
            (ratios["shirt_hem"], profile["waist_and_hem_overlap"]),
            (lower_center, lower),
            (upper_center, upper),
            (ratios["shirt_collar"], profile["clavicle"]),
        ],
        height_ratio,
    )

    clavicle_weight = sum(weights_by_name.get(name, 0.0) for name in {"clavicle_l", "clavicle_r"})
    arm_weight = sum(
        weights_by_name.get(name, 0.0)
        for name in {"upperarm_l", "lowerarm_l", "upperarm_r", "lowerarm_r"}
    )
    if arm_weight >= 0.4:
        return profile["upper_arm"] if arm_weight >= 0.75 else (
            torso_clearance + (profile["upper_arm"] - torso_clearance) * ((arm_weight - 0.4) / 0.35)
        )
    if clavicle_weight > 0.2:
        blend = min(1.0, clavicle_weight / 0.55)
        return torso_clearance + (profile["clavicle"] - torso_clearance) * blend
    return torso_clearance


def make_male_shirt_torso(include_sleeves=True, source_face_sets=None, hem_height=None):
    body = bpy.data.objects["HumanBody"]
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    body_height = body.dimensions.z
    body_min_z = min(vertex.co.z for vertex in body.data.vertices)
    body_world = body.matrix_world.copy()
    ratios = FIT_PROFILE["applied_profile"]["body_height_ratios"]
    hem_z = body_min_z + body_height * ratios["shirt_hem"]
    if hem_height is not None:
        hem_z = hem_height
    neckline_profile = FIT_PROFILE["applied_profile"]["shirt_clearance_m"]
    thickness_profile = FIT_PROFILE["applied_profile"]["solidify"]
    neck_base_world = armature.matrix_world @ armature.data.bones["neck_01"].head_local
    neck_base_local = body_world.inverted() @ neck_base_world
    collar_z = neck_base_local.z + neckline_profile["neckline_side_offset_from_neck_base"]

    # Cut the hem and neckline's maximum height into the source topology. The
    # measured curved neckline below selects around these boundary rings.
    cut_mesh = bmesh.new()
    cut_mesh.from_mesh(body.data)
    geometry = list(cut_mesh.verts) + list(cut_mesh.edges) + list(cut_mesh.faces)
    for height in (hem_z, collar_z):
        bmesh.ops.bisect_plane(
            cut_mesh,
            geom=geometry,
            dist=0.00001,
            plane_co=(0.0, 0.0, height),
            plane_no=(0.0, 0.0, 1.0),
        )
        geometry = list(cut_mesh.verts) + list(cut_mesh.edges) + list(cut_mesh.faces)
    cut_mesh.to_mesh(body.data)
    cut_mesh.free()
    body.data.update()

    arm_groups = {
        "clavicle_l", "upperarm_l", "lowerarm_l",
        "clavicle_r", "upperarm_r", "lowerarm_r",
    }
    side_arm_groups = {
        side: {f"clavicle_{side}", f"upperarm_{side}", f"lowerarm_{side}"}
        for side in ("l", "r")
    }
    hand_groups = {
        group.name for group in body.vertex_groups
        if group.name in {"hand_l", "hand_r"}
        or any(name in group.name for name in ("thumb_", "index_", "middle_", "ring_", "pinky_"))
    }
    group_names = {group.index: group.name for group in body.vertex_groups}
    arm_weights = [mesh_group_weight(vertex, group_names, arm_groups) for vertex in body.data.vertices]
    side_arm_weights = {
        side: [
            mesh_group_weight(vertex, group_names, groups)
            for vertex in body.data.vertices
        ]
        for side, groups in side_arm_groups.items()
    }
    hand_weights = [mesh_group_weight(vertex, group_names, hand_groups) for vertex in body.data.vertices]
    selected_faces = []
    for polygon in body.data.polygons:
        if source_face_sets is not None:
            source_set = body.data.attributes[".sculpt_face_set"].data[polygon.index].value
            if source_set not in source_face_sets:
                continue
        center = polygon.center
        arm_weight = sum(arm_weights[index] for index in polygon.vertices) / len(polygon.vertices)
        side_weights = {
            side: sum(side_arm_weights[side][index] for index in polygon.vertices) / len(polygon.vertices)
            for side in ("l", "r")
        }
        hand_weight = max(hand_weights[index] for index in polygon.vertices)
        in_vertical_range = hem_z <= center.z <= collar_z
        width_blend = smoothstep(
            neckline_profile["neckline_source_half_width"],
            neckline_profile["neckline_target_half_width"],
            abs(center.x),
        )
        depth_blend = smoothstep(-0.023, 0.077, center.y)
        front_cut_z = neck_base_local.z + neckline_profile["neckline_front_center_offset_from_neck_base"]
        back_cut_z = neck_base_local.z + neckline_profile["neckline_back_center_offset_from_neck_base"]
        side_cut_z = neck_base_local.z + neckline_profile["neckline_side_offset_from_neck_base"]
        front_cut_z += (side_cut_z - front_cut_z) * width_blend
        back_cut_z += (side_cut_z - back_cut_z) * width_blend
        neckline_z = front_cut_z + (back_cut_z - front_cut_z) * depth_blend
        outside_neckline = (
            abs(center.x) > neckline_profile["neckline_target_half_width"]
            or center.z <= neckline_z
        )
        arm_surface_crossing = any(
            arm_weights[index] >= 0.20
            and abs(body.data.vertices[index].co.x) > 0.22
            for index in polygon.vertices
        )
        is_torso = (
            in_vertical_range and abs(center.x) <= 0.38
            and outside_neckline
            and (include_sleeves or not arm_surface_crossing)
        )
        is_sleeve = include_sleeves and in_vertical_range and arm_weight >= 0.12 and hand_weight < 0.12 and outside_neckline
        if source_face_sets is not None:
            # Source-region boundaries define the male player's armholes;
            # weight thresholds can otherwise leave detached scraps on wrists.
            is_torso = in_vertical_range and outside_neckline
        if is_torso or is_sleeve:
            selected_faces.append(polygon)
    if len(selected_faces) < 300:
        raise RuntimeError(f"Measured-fit male shirt selection is unexpectedly small: {len(selected_faces)} faces.")

    normal_mesh = bmesh.new()
    normal_mesh.from_mesh(body.data)
    normal_mesh.verts.ensure_lookup_table()
    normal_mesh.normal_update()
    vertex_normals = [vertex.normal.normalized() for vertex in normal_mesh.verts]
    normal_mesh.free()

    source_to_shirt = {}
    source_indices = []
    shirt_vertices = []
    shirt_faces = []
    measured_offsets = []
    for polygon in selected_faces:
        face = []
        for source_index in polygon.vertices:
            shirt_index = source_to_shirt.get(source_index)
            if shirt_index is None:
                source_vertex = body.data.vertices[source_index]
                normal = vertex_normals[source_index]
                weights_by_name = {
                    body.vertex_groups[assignment.group].name: assignment.weight
                    for assignment in source_vertex.groups
                    if assignment.weight > 1e-8
                }
                clearance = shirt_clearance_for_weights(
                    source_vertex.co, normal, body_min_z, body_height, weights_by_name
                )
                shirt_index = len(shirt_vertices)
                source_to_shirt[source_index] = shirt_index
                source_indices.append(source_index)
                shirt_vertices.append(tuple(source_vertex.co + normal * clearance))
                measured_offsets.append(clearance)
            face.append(shirt_index)
        shirt_faces.append(tuple(face))

    if len(shirt_faces) < 300:
        raise RuntimeError(f"Measured-fit male shirt selection is unexpectedly small: {len(shirt_faces)} faces.")

    shirt_mesh = bpy.data.meshes.new("HumanMaleShirtMesh")
    shirt_mesh.from_pydata(shirt_vertices, [], shirt_faces)
    shirt_mesh.update()
    shirt_bmesh = bmesh.new()
    shirt_bmesh.from_mesh(shirt_mesh)
    bmesh.ops.recalc_face_normals(shirt_bmesh, faces=list(shirt_bmesh.faces))
    shirt_bmesh.to_mesh(shirt_mesh)
    shirt_bmesh.free()

    shirt = bpy.data.objects.new("HumanMaleShirt", shirt_mesh)
    bpy.context.collection.objects.link(shirt)
    shirt.parent = body.parent
    shirt.matrix_world = body_world
    groups_by_name = {
        group.name: shirt.vertex_groups.new(name=group.name)
        for group in body.vertex_groups
    }
    for shirt_index, source_index in enumerate(source_indices):
        for assignment in body.data.vertices[source_index].groups:
            if assignment.weight > 1e-6:
                source_group = body.vertex_groups[assignment.group]
                groups_by_name[source_group.name].add([shirt_index], assignment.weight, "REPLACE")

    # Keep the measured shell surface at the measured garment clearance. Use
    # up to 10 mm of fabric inward, but thin the tightest areas enough to keep
    # at least the profile's explicit gap between cloth and skin.
    thickness_group = shirt.vertex_groups.new(name="HumanMaleShirtThickness")
    target_thickness = thickness_profile["shirt_thickness_m"]
    minimum_thickness = thickness_profile["shirt_min_thickness_m"]
    minimum_skin_gap = thickness_profile["shirt_min_skin_gap_m"]
    applied_thicknesses = []
    for shirt_index, clearance in enumerate(measured_offsets):
        applied_thickness = min(target_thickness, max(minimum_thickness, clearance - minimum_skin_gap))
        thickness_group.add([shirt_index], applied_thickness / target_thickness, "REPLACE")
        applied_thicknesses.append(applied_thickness)

    shell = shirt.modifiers.new("HumanMaleShirtFabricThickness", "SOLIDIFY")
    shell.thickness = target_thickness
    shell.offset = thickness_profile["shirt_offset"]
    shell.vertex_group = thickness_group.name
    shell.thickness_vertex_group = 0.0
    shell.use_even_offset = False
    shell.use_rim = True
    if abs(shell.offset + 1.0) > 1e-6 or not shell.vertex_group:
        raise RuntimeError("Male shirt thickness must preserve its measured outer fit and adapt to local clearance.")
    skin = shirt.modifiers.new("HumanMaleShirtArmature", "ARMATURE")
    skin.object = armature
    skin.use_vertex_groups = True
    shirt.data.materials.append(
        make_project_material("Human_Male_Workwear", (0.24, 0.29, 0.16), roughness=0.9)
    )
    for polygon in shirt.data.polygons:
        polygon.use_smooth = True
    shirt.data.update()

    shirt["fit_profile_id"] = FIT_PROFILE["profile_id"]
    shirt["clearance_min_m"] = min(measured_offsets)
    shirt["clearance_median_m"] = sorted(measured_offsets)[len(measured_offsets) // 2]
    shirt["clearance_max_m"] = max(measured_offsets)
    shirt["source_face_count"] = len(shirt_faces)
    shirt["fabric_thickness_m"] = shell.thickness
    shirt["fabric_minimum_thickness_m"] = min(applied_thicknesses)
    shirt["fabric_inward_depth_range_m"] = [min(applied_thicknesses), max(applied_thicknesses)]
    shirt["fabric_outward_depth_m"] = thickness_profile["shirt_outward_depth_m"]
    print(
        f"Created independently skinned male shirt from the measured body fit "
        f"({'sleeved' if include_sleeves else 'sleeveless'}): "
        f"{len(shirt_faces)} faces, "
        f"measured clearances {min(measured_offsets):.4f}–{max(measured_offsets):.4f} m "
        f"(median {sorted(measured_offsets)[len(measured_offsets) // 2]:.4f} m), "
        f"with {shell.thickness:.3f} m fabric thickness."
    )


def make_shirt_buttons():
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    torso = bpy.data.objects["HumanMaleShirt"]
    front = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in torso.data.vertices],
        [tuple(polygon.vertices) for polygon in torso.data.polygons],
        all_triangles=False,
        epsilon=0.0,
    )

    def front_y(z):
        hit, _, _, _ = front.ray_cast(Vector((0.0, -0.5, z)), Vector((0.0, 1.0, 0.0)), 1.0)
        if hit is None:
            raise RuntimeError(f"Could not place a shirt button at torso height {z:.3f} m.")
        return hit.y

    vertices = []
    faces = []
    button_heights = (1.18, 1.225, 1.27, 1.315, 1.36)
    sides = 12
    for z in button_heights:
        thickness_profile = FIT_PROFILE["applied_profile"]["solidify"]
        outer_front_y = front_y(z) - thickness_profile["shirt_outward_depth_m"] - 0.006
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


def make_leather_belt(clearance=0.012):
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    body = bpy.data.objects["HumanBody"]
    mesh = body.data
    belt_heights = (1.055, 1.105)
    torso_groups = {
        group.name: group.index
        for group in body.vertex_groups
        if group.name in {"pelvis", "spine_01", "spine_02"}
    }
    if len(torso_groups) != 3:
        raise RuntimeError("Body rig is missing torso weights needed to fit its belt.")
    waist_samples = [
        vertex.co for vertex in mesh.vertices
        if min(belt_heights) - 0.045 <= vertex.co.z <= max(belt_heights) + 0.045
        and abs(vertex.co.x) <= 0.28
        and sum(
            assignment.weight
            for assignment in vertex.groups
            if assignment.group in torso_groups.values()
        ) >= 0.35
    ]
    if len(waist_samples) < 20:
        raise RuntimeError("Could not measure the body's waist for its fitted belt.")
    center_y = (
        min(point.y for point in waist_samples)
        + max(point.y for point in waist_samples)
    ) * 0.5
    front_depth = max(center_y - point.y for point in waist_samples) + clearance
    back_depth = max(point.y - center_y for point in waist_samples) + clearance
    half_width = max(abs(point.x) for point in waist_samples) + clearance
    segments = 48
    vertices = []
    faces = []
    for z in belt_heights:
        for step in range(segments):
            angle = 2.0 * math.pi * step / segments
            sine = math.sin(angle)
            depth = front_depth if sine < 0.0 else back_depth
            vertices.append((
                half_width * math.cos(angle),
                center_y + sine * depth,
                z,
            ))
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

    buckle_x = 0.0
    buckle_front_y = center_y - front_depth - 0.010
    buckle_back_y = center_y - front_depth + 0.002
    buckle_vertices = [
        (buckle_x - 0.030, buckle_back_y, 1.065), (buckle_x + 0.030, buckle_back_y, 1.065),
        (buckle_x + 0.030, buckle_back_y, 1.095), (buckle_x - 0.030, buckle_back_y, 1.095),
        (buckle_x - 0.030, buckle_front_y, 1.065), (buckle_x + 0.030, buckle_front_y, 1.065),
        (buckle_x + 0.030, buckle_front_y, 1.095), (buckle_x - 0.030, buckle_front_y, 1.095),
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
    body = bpy.data.objects["HumanBody"]
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    mesh = body.data
    mesh.calc_loop_triangles()
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    positions = [vertex.co.copy() for vertex in mesh.vertices]
    scalp = BVHTree.FromPolygons(positions, triangles, all_triangles=True)
    center = Vector((0.0, -0.015, 1.625))
    segments = 48
    rings = 9
    vertices = []

    def cast(direction):
        hit, normal, _, _ = scalp.ray_cast(center, direction, 0.5)
        if hit is None:
            raise RuntimeError("Could not fit the male hair cap to the Blender head surface.")
        if normal.dot(direction) < 0.0:
            normal.negate()
        return hit + normal.normalized() * 0.009

    vertices.append(cast(Vector((0.0, 0.0, 1.0))))
    for ring in range(1, rings + 1):
        fraction = ring / rings
        for step in range(segments):
            azimuth = 2.0 * math.pi * step / segments
            back = max(0.0, math.sin(azimuth))
            front = max(0.0, -math.sin(azimuth))
            side = abs(math.cos(azimuth))
            # The hairline stays above the ears at the temples, while the back
            # drops toward the nape for continuous side/back coverage.
            theta_limit = 1.34 + 0.96 * back - 0.10 * front + 0.06 * side
            theta = theta_limit * fraction
            direction = Vector((
                math.sin(theta) * math.cos(azimuth),
                math.sin(theta) * math.sin(azimuth),
                math.cos(theta),
            )).normalized()
            vertices.append(cast(direction))

    faces = []
    first_ring = 1
    for step in range(segments):
        next_step = (step + 1) % segments
        faces.append((0, first_ring + step, first_ring + next_step))
    for ring in range(rings - 1):
        low = 1 + ring * segments
        high = low + segments
        for step in range(segments):
            next_step = (step + 1) % segments
            faces.append((low + step, low + next_step, high + next_step, high + step))

    hair_mesh = bpy.data.meshes.new("HumanMaleShortHairMesh")
    hair_mesh.from_pydata(vertices, [], faces)
    hair_mesh.update()
    bm = bmesh.new()
    bm.from_mesh(hair_mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(hair_mesh)
    bm.free()
    hair = create_head_weighted_object(
        "HumanMaleShortHair", hair_mesh, armature, body,
        make_project_material("Human_Male_Hair", (0.055, 0.027, 0.016), roughness=0.88),
    )
    side_coverage = sum(1 for x, y, z in vertices if abs(x) > 0.05 and z > 1.48)
    back_coverage = sum(1 for x, y, z in vertices if y > 0.045 and z > 1.48)
    if side_coverage < 40 or back_coverage < 50:
        raise RuntimeError(
            f"Male hair cap lost coverage ({side_coverage} side, {back_coverage} back samples)."
        )


def make_trousers(source_face_sets=None):
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

    group_names = {group.index: group.name for group in trousers.vertex_groups}
    leg_groups = {
        "pelvis", "thigh_l", "calf_l", "foot_l",
        "thigh_r", "calf_r", "foot_r",
    }
    leg_weights = [mesh_group_weight(vertex, group_names, leg_groups)
                   for vertex in trousers.data.vertices]
    bm = bmesh.new()
    bm.from_mesh(trousers.data)
    if source_face_sets is not None:
        for height in (0.30, 1.085):
            bmesh.ops.bisect_plane(
                bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                dist=0.00001, plane_co=(0, 0, height), plane_no=(0, 0, 1),
            )
    bm.verts.index_update()
    source_layer = bm.faces.layers.int.get(".sculpt_face_set")
    garment_faces = [
        face for face in bm.faces
        if 0.30 <= face.calc_center_median().z <= (1.085 if source_face_sets is not None else 1.12)
        and (face[source_layer] in source_face_sets if source_face_sets is not None else
             sum(leg_weights[vertex.index] for vertex in face.verts) / len(face.verts) >= 0.38)
    ]
    if len(garment_faces) < 250:
        bm.free()
        raise RuntimeError(f"Trousers body-surface selection is unexpectedly small: {len(garment_faces)} faces.")
    garment_face_set = set(garment_faces)
    bmesh.ops.delete(bm, geom=[face for face in bm.faces if face not in garment_face_set], context="FACES")
    loose_vertices = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose_vertices:
        bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    clearance = FIT_PROFILE["applied_profile"]["trousers_clearance_m"]["all_fitted_regions"]
    thickness_profile = FIT_PROFILE["applied_profile"]["solidify"]
    # Preserve the user-approved trouser silhouette using the clearance measured
    # from its existing body-to-garment surfaces, not a builder-only constant.
    for vertex in bm.verts:
        if vertex.normal.length_squared > 1e-5:
            vertex.co += vertex.normal.normalized() * clearance
    bm.to_mesh(trousers.data)
    bm.free()
    trousers.data.update()

    shell = trousers.modifiers.new("HumanMaleTrousersFabricThickness", "SOLIDIFY")
    shell.thickness = thickness_profile["trousers_thickness_m"]
    shell.offset = thickness_profile["trousers_offset"]
    shell.use_even_offset = True
    shell.use_rim = True
    if abs(shell.thickness - thickness_profile["trousers_inward_depth_m"]) > 1e-6 or abs(shell.offset + 1.0) > 1e-6:
        raise RuntimeError("Male trousers Solidify depths do not sum to the profile thickness.")
    bpy.context.view_layer.objects.active = trousers
    trousers.select_set(True)
    armature_modifier = next(
        (modifier for modifier in trousers.modifiers if modifier.type == "ARMATURE"), None
    )
    if armature_modifier is None:
        raise RuntimeError("Generated trousers must retain the body's armature skinning.")
    while trousers.modifiers.find(shell.name) > trousers.modifiers.find(armature_modifier.name):
        if "FINISHED" not in bpy.ops.object.modifier_move_up(modifier=shell.name):
            raise RuntimeError("Could not place trouser thickness before the armature modifier.")

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
    trousers["fit_profile_id"] = FIT_PROFILE["profile_id"]
    trousers["clearance_m"] = clearance
    trousers["fabric_thickness_m"] = shell.thickness
    trousers["fabric_inward_depth_m"] = thickness_profile["trousers_inward_depth_m"]
    trousers["fabric_outward_depth_m"] = thickness_profile["trousers_outward_depth_m"]

    # The copied body object keeps its armature modifier and vertex groups.
    if not any(mod.type == "ARMATURE" and mod.object == trousers.parent for mod in trousers.modifiers):
        raise RuntimeError("Generated trousers must retain the body's armature skinning.")
    required_groups = {"pelvis", "thigh_l", "calf_l", "thigh_r", "calf_r"}
    missing_groups = required_groups.difference(group.name for group in trousers.vertex_groups)
    if missing_groups:
        raise RuntimeError(f"Generated trousers are missing body skin weights: {sorted(missing_groups)}")


def make_male_boots():
    """Create fitted low boots from the male body's actual lower-leg surface."""
    body = bpy.data.objects["HumanBody"]
    boots = body.copy()
    boots.data = body.data.copy()
    boots.name = "HumanMaleBoots"
    boots.data.name = "HumanMaleBootsMesh"
    bpy.context.collection.objects.link(boots)

    bm = bmesh.new()
    bm.from_mesh(boots.data)
    bm.verts.index_update()
    boot_faces = [
        face for face in bm.faces
        if 0.0 <= face.calc_center_median().z <= 0.39
        and abs(face.calc_center_median().x) <= 0.42
    ]
    if len(boot_faces) < 100:
        bm.free()
        bpy.data.objects.remove(boots, do_unlink=True)
        raise RuntimeError(f"Fitted male boot selection is unexpectedly small: {len(boot_faces)} faces.")
    boot_face_set = set(boot_faces)
    bmesh.ops.delete(bm, geom=[face for face in bm.faces if face not in boot_face_set], context="FACES")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    for vertex in bm.verts:
        if vertex.normal.length_squared > 1e-5:
            vertex.co += vertex.normal.normalized() * 0.018
    bm.to_mesh(boots.data)
    bm.free()
    boots.data.update()
    for polygon in boots.data.polygons:
        polygon.use_smooth = True
    boot_material = make_project_material(
        "Human_Male_Boot_Leather", (0.11, 0.075, 0.045), roughness=0.9
    )
    boots.data.materials.clear()
    boots.data.materials.append(boot_material)
    # Color the underlying foot surface too. The body source has one foot
    # slightly lower in its source pose, so a fitted shell alone can leave toes
    # exposed after the idle action moves the feet.
    body.data.materials.append(boot_material)
    boot_material_index = len(body.data.materials) - 1
    for polygon in body.data.polygons:
        center = polygon.center
        if center.z <= 0.39 and abs(center.x) <= 0.42:
            polygon.material_index = boot_material_index
    if not any(mod.type == "ARMATURE" and mod.object == boots.parent for mod in boots.modifiers):
        raise RuntimeError("Generated male boots must retain the body's armature skinning.")


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

    # A softer rear fill keeps the turntable views readable from every side.
    fill_data = bpy.data.lights.new("Preview Rear Fill", type="AREA")
    fill_data.energy = 300.0
    fill_data.size = 5.0
    fill = bpy.data.objects.new("Preview Rear Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (2.5, 4.0, 3.3)
    fill.rotation_euler = (Vector((0.0, 0.0, 0.9)) - fill.location).to_track_quat("-Z", "Y").to_euler()


def render_turntable_previews():
    """Save front, side, and back views alongside the established three-quarter preview."""
    scene = bpy.context.scene
    camera = scene.camera
    target = Vector((0.0, 0.0, 0.88))
    for output_path, location in (
        (OUT_FRONT_PREVIEW, (0.0, -5.8, 1.75)),
        (OUT_SIDE_PREVIEW, (5.8, 0.0, 1.75)),
        (OUT_BACK_PREVIEW, (0.0, 5.8, 1.75)),
    ):
        camera.location = location
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)


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

    body = bpy.data.objects["HumanBody"]
    body_height = max(vertex.co.z for vertex in body.data.vertices) - min(vertex.co.z for vertex in body.data.vertices)
    if abs(body_height - TARGET_HEIGHT_M) > 0.02:
        raise RuntimeError(f"Male body height is {body_height:.3f} m; expected {TARGET_HEIGHT_M:.2f} m.")
    for side in ("l", "r"):
        hand_group = body.vertex_groups[f"hand_{side}"]
        visible_hand_vertices = 0
        for vertex in body.data.vertices:
            try:
                if hand_group.weight(vertex.index) > 0.15:
                    visible_hand_vertices += 1
            except RuntimeError:
                continue
        if visible_hand_vertices < 20:
            raise RuntimeError(f"Reference hand geometry/weights are missing for side {side}.")

    shirt = bpy.data.objects.get("HumanMaleShirt")
    if shirt is None or shirt.type != "MESH" or int(shirt.get("source_face_count", 0)) < 300:
        raise RuntimeError("A separate fitted shirt shell must cover the torso and both sleeves.")
    if shirt.get("fit_profile_id") != FIT_PROFILE["profile_id"]:
        raise RuntimeError("The male shirt must reference the measured clothing-fit profile.")
    if not any(mod.type == "ARMATURE" and mod.object == armatures[0] for mod in shirt.modifiers):
        raise RuntimeError("The separate male shirt must use the canonical humanoid armature.")
    shirt_materials = {material.name for material in shirt.data.materials if material is not None}
    if "Human_Male_Workwear" not in shirt_materials:
        raise RuntimeError("The separate fitted shirt shell is missing its workwear material.")
    clearance_values = [
        float(shirt.get("clearance_min_m", 0.0)),
        float(shirt.get("clearance_median_m", 0.0)),
        float(shirt.get("clearance_max_m", 0.0)),
    ]
    if not (0.003 <= clearance_values[0] <= clearance_values[1] <= clearance_values[2] <= 0.02):
        raise RuntimeError(f"Male shirt clearances are outside the measured fitted range: {clearance_values}.")
    expected_thickness = FIT_PROFILE["applied_profile"]["solidify"]["shirt_thickness_m"]
    shirt_shells = [mod for mod in shirt.modifiers if mod.type == "SOLIDIFY"]
    if len(shirt_shells) != 1 or abs(shirt_shells[0].thickness - expected_thickness) > 1e-6:
        raise RuntimeError("Male shirt must have the measured-profile fabric Solidify thickness.")
    if abs(shirt_shells[0].offset + 1.0) > 1e-6 or shirt_shells[0].vertex_group != "HumanMaleShirtThickness":
        raise RuntimeError("Male shirt Solidify must preserve outer fit and use clearance-based thickness weights.")
    thickness_group = shirt.vertex_groups.get("HumanMaleShirtThickness")
    if thickness_group is None:
        raise RuntimeError("Male shirt clearance-based fabric thickness weights are incomplete.")
    thickness_values = []
    for vertex in shirt.data.vertices:
        try:
            thickness_values.append(thickness_group.weight(vertex.index))
        except RuntimeError:
            continue
    if len(thickness_values) != len(shirt.data.vertices) or min(thickness_values) < 0.29 or max(thickness_values) > 1.0:
        raise RuntimeError("Male shirt thickness weights do not cover the measured 3–10 mm range.")
    if [mod.type for mod in shirt.modifiers].index("SOLIDIFY") > [
        mod.type for mod in shirt.modifiers
    ].index("ARMATURE"):
        raise RuntimeError("Male shirt thickness must be generated before it is skinned to the rig.")
    trousers = bpy.data.objects.get("HumanTrousers")
    if trousers is None or trousers.get("fit_profile_id") != FIT_PROFILE["profile_id"]:
        raise RuntimeError("The male trousers must use the measured clothing-fit profile.")
    expected_trouser_clearance = FIT_PROFILE["applied_profile"]["trousers_clearance_m"]["all_fitted_regions"]
    if abs(float(trousers.get("clearance_m", 0.0)) - expected_trouser_clearance) > 1e-6:
        raise RuntimeError("The male trousers no longer match the measured fitted clearance.")
    expected_trouser_thickness = FIT_PROFILE["applied_profile"]["solidify"]["trousers_thickness_m"]
    trouser_shells = [mod for mod in trousers.modifiers if mod.type == "SOLIDIFY"]
    if len(trouser_shells) != 1 or abs(trouser_shells[0].thickness - expected_trouser_thickness) > 1e-6:
        raise RuntimeError("Male trousers must have the measured-profile fabric Solidify thickness.")
    if [mod.type for mod in trousers.modifiers].index("SOLIDIFY") > [
        mod.type for mod in trousers.modifiers
    ].index("ARMATURE"):
        raise RuntimeError("Male trouser thickness must be generated before it is skinned to the rig.")


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


def validate_exported_garment_shells():
    """Require the runtime shirt and trousers to export as closed, thick meshes."""
    blob = OUT_GLB.read_bytes()
    if blob[:4] != b"glTF":
        raise RuntimeError("Exported runtime asset is not a binary glTF file.")
    json_length = struct.unpack_from("<I", blob, 12)[0]
    document = json.loads(blob[20:20 + json_length].decode("utf-8"))
    bin_header = 20 + json_length
    bin_length, bin_kind = struct.unpack_from("<II", blob, bin_header)
    if bin_kind != 0x004E4942:
        raise RuntimeError("Exported GLB is missing its binary geometry chunk.")
    binary = blob[bin_header + 8:bin_header + 8 + bin_length]
    component_formats = {5121: ("B", 1), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
    type_widths = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}

    def accessor_values(accessor_index):
        accessor = document["accessors"][accessor_index]
        view = document["bufferViews"][accessor["bufferView"]]
        component_format, component_size = component_formats[accessor["componentType"]]
        width = type_widths[accessor["type"]]
        item_size = component_size * width
        stride = view.get("byteStride", item_size)
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        values = []
        for item_index in range(accessor["count"]):
            unpacked = struct.unpack_from(
                "<" + component_format * width, binary, start + item_index * stride
            )
            values.append(unpacked[0] if width == 1 else unpacked)
        return values

    nodes_by_name = {}
    for node in document.get("nodes", []):
        if "mesh" in node:
            nodes_by_name.setdefault(node.get("name", ""), []).append(node["mesh"])

    weld_tolerance_m = 1e-5
    for garment_name in ("HumanMaleShirt", "HumanTrousers"):
        mesh_indices = nodes_by_name.get(garment_name, [])
        if len(mesh_indices) != 1:
            raise RuntimeError(
                f"Expected one runtime GLB mesh node named {garment_name}; found {len(mesh_indices)}."
            )
        edge_counts = {}
        triangle_count = 0
        welded_vertices = set()
        for primitive in document["meshes"][mesh_indices[0]]["primitives"]:
            if primitive.get("mode", 4) != 4:
                raise RuntimeError(f"Runtime garment {garment_name} is not exported as triangles.")
            positions = accessor_values(primitive["attributes"]["POSITION"])
            coordinates = [
                tuple(round(component / weld_tolerance_m) for component in position)
                for position in positions
            ]
            welded_vertices.update(coordinates)
            indices = accessor_values(primitive["indices"]) if "indices" in primitive else list(range(len(positions)))
            if len(indices) % 3:
                raise RuntimeError(f"Runtime garment {garment_name} has an incomplete triangle.")
            triangle_count += len(indices) // 3
            for offset in range(0, len(indices), 3):
                triangle = [coordinates[indices[offset + vertex]] for vertex in range(3)]
                for edge_index in range(3):
                    first = triangle[edge_index]
                    second = triangle[(edge_index + 1) % 3]
                    edge = (first, second) if first <= second else (second, first)
                    edge_counts[edge] = edge_counts.get(edge, 0) + 1

        boundary_edges = sum(count == 1 for count in edge_counts.values())
        nonmanifold_edges = sum(count != 2 for count in edge_counts.values())
        if triangle_count == 0 or not welded_vertices:
            raise RuntimeError(f"Runtime garment {garment_name} exported without geometry.")
        if boundary_edges or nonmanifold_edges:
            raise RuntimeError(
                f"Runtime garment {garment_name} is not a closed fabric shell: "
                f"{boundary_edges} boundary edges, {nonmanifold_edges} edges with incidence other than 2."
            )
        print(
            f"Runtime garment shell passed: {garment_name}, {len(welded_vertices)} welded vertices, "
            f"{triangle_count} triangles, closed rims and inner surface present."
        )


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    deform_upper_body()
    make_short_hair()
    make_male_shirt_torso()
    make_shirt_buttons()
    make_leather_belt()
    make_trousers()
    make_male_boots()
    add_semantic_anchors()
    validate_asset()

    # Keep source geometry provenance separate from the inherited reference rig.
    bpy.context.scene["asset_id"] = "human_male_humanoid_rig_v1"
    bpy.context.scene["humanoid_rig_version"] = 1
    bpy.context.scene["source_asset"] = "Blender CC0 male base body + Humanoid Rig v1 reference"
    bpy.context.scene["source_license"] = "CC0 1.0 body; inherited rig, animation, and boots license unresolved"
    bpy.context.scene["variant"] = "male-presenting adult human prototype"

    OUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    OUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    OUT_PREVIEW.parent.mkdir(parents=True, exist_ok=True)

    configure_render()
    bpy.context.scene.render.filepath = str(OUT_PREVIEW)
    bpy.ops.render.render(write_still=True)
    render_turntable_previews()

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
        # The exporter applies non-armature modifiers but preserves skinning.
        # This bakes Solidify into the GLB instead of exporting an open sheet.
        export_apply=True,
    )
    validate_exported_anchors()
    validate_exported_garment_shells()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    print(f"Male variant written: {OUT_BLEND}")
    print(f"Runtime GLB written: {OUT_GLB}")
    print(f"Preview written: {OUT_PREVIEW}")
    print(f"Front preview written: {OUT_FRONT_PREVIEW}")
    print(f"Side preview written: {OUT_SIDE_PREVIEW}")
    print(f"Back preview written: {OUT_BACK_PREVIEW}")


if __name__ == "__main__":
    main()
