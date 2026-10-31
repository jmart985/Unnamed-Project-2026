"""Author clean, skinned Blender player bases without baked-in wearables.

The native source retains Multires. The runtime copy evaluates that authored
surface, retaining interpolated skin weights. Optional anatomy is a separate
set of skinned meshes, not part of the base body or simulation anatomy.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
SOURCE_OUT = ROOT / "art_source/characters"
RUNTIME = ROOT / "game/assets/humanoids/player"
sys.path.insert(0, str(Path(__file__).parent))
import canonical_player_rig as canonical
import humanoid_locomotion_v1 as motion
import retarget_player_expressions as expressions
import player_clothing_support as clothing


def create_rig(body):
    data = bpy.data.armatures.new("HumanoidRigV1")
    rig = bpy.data.objects.new("HumanoidRigV1", data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    parents = {"root": None, "pelvis": "root", "spine_01": "pelvis",
               "spine_02": "spine_01", "spine_03": "spine_02",
               "neck_01": "spine_03", "head": "neck_01", "head_end": "head"}
    for side in ("l", "r"):
        for chain, parent in ((["clavicle", "upperarm", "lowerarm", "hand"], "spine_03"),
                              (["thigh", "calf", "foot", "toe"], "pelvis")):
            for name in chain:
                key = f"{name}_{side}"
                parents[key], parent = parent, key
        parents[f"ball_{side}_end"] = f"toe_{side}"
        for finger in ("thumb", "index", "middle", "ring", "pinky"):
            parent = f"hand_{side}"
            for number in range(1, 4):
                key = f"{finger}_{number:02}_{side}"
                parents[key], parent = parent, key
            parents[f"{finger}_03_{side}_end"] = parent
    for name in parents:
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = (0, 0, 0), (0, 0, .1)
    for name, parent in parents.items():
        bone = data.edit_bones[name]
        if parent:
            bone.parent = data.edit_bones[parent]
        bone.use_deform = name != "root" and not name.endswith("_end")
    bpy.ops.object.mode_set(mode="OBJECT")
    canonical.fit_rig_to_base(body, rig)
    return rig


def normalize_weights(obj):
    for vertex in obj.data.vertices:
        top = sorted([(g.group, g.weight) for g in vertex.groups if g.weight > 1e-7],
                     key=lambda pair: pair[1], reverse=True)[:4]
        total = sum(w for _, w in top)
        if total <= 1e-7:
            raise RuntimeError(f"Unweighted vertex {vertex.index} on {obj.name}.")
        for group in list(vertex.groups):
            obj.vertex_groups[group.group].remove([vertex.index])
        for group, weight in top:
            obj.vertex_groups[group].add([vertex.index], weight / total, "REPLACE")


def bind(obj, rig, bone=None):
    transform = obj.matrix_world.copy()
    obj.parent = rig
    obj.matrix_world = transform
    if bone:
        group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
        modifier = obj.modifiers.new("Player skin", "ARMATURE")
        modifier.object = rig
    else:
        levels = [(m, m.levels) for m in obj.modifiers if m.type == "MULTIRES"]
        for modifier, _ in levels:
            modifier.levels = 0
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        if "FINISHED" not in bpy.ops.object.parent_set(type="ARMATURE_AUTO"):
            raise RuntimeError("Blender could not skin the source body.")
        for modifier, level in levels:
            modifier.levels = level
    normalize_weights(obj)


def additional_bones(body, eyes, rig, sex):
    points = [body.matrix_world @ v.co for v in body.data.vertices]
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    eye_centers = [sum((eye.matrix_world @ v.co for v in eye.data.vertices), Vector())
                   / len(eye.data.vertices) for eye in eyes]
    eye_centers.sort(key=lambda p: p.x, reverse=True)
    mouth = Vector((0, min(p.y for p in points if head.z < p.z < head.z + .08), head.z + .03))
    positions = {"jaw": (head + Vector((0, .015, -.005)), mouth + Vector((0, .025, -.025))),
                 "genital_root": (Vector((0, -.075, .855)), Vector((0, -.08, .825))),
                 "genital_01": (Vector((0, -.08, .825)), Vector((0, -.085, .79))),
                 "genital_02": (Vector((0, -.085, .79)), Vector((0, -.085, .765))),
                 "genital_l": (Vector((.014, -.075, .83)), Vector((.014, -.08, .8))),
                 "genital_r": (Vector((-.014, -.075, .83)), Vector((-.014, -.08, .8)))}
    for side, eye in zip(("l", "r"), eye_centers):
        positions[f"eye_{side}"] = (eye, eye + Vector((0, -.02, 0)))
        positions[f"eyelid_{side}"] = (eye + Vector((0, -.007, .006)), eye + Vector((0, -.007, .016)))
        positions[f"brow_{side}"] = (eye + Vector((0, -.005, .032)), eye + Vector((0, -.005, .045)))
        x = .027 if side == "l" else -.027
        corner = mouth + Vector((x, .008, -.005))
        positions[f"mouth_corner_{side}"] = (corner, corner + Vector((0, 0, .014)))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    inverse = rig.matrix_world.inverted()
    for name, (start, end) in positions.items():
        bone = rig.data.edit_bones.new(name)
        bone.head, bone.tail = inverse @ start, inverse @ end
        parent = "pelvis" if name == "genital_root" else (
            "genital_01" if name == "genital_02" else "genital_root" if name.startswith("genital_") else "head")
        bone.parent = rig.data.edit_bones[parent]
        bone.align_roll(Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    for name, (start, _) in positions.items():
        if name == "jaw" or name.startswith(("brow_", "eyelid_", "mouth_corner_")):
            start = mouth + Vector((0, .014, -.025)) if name == "jaw" else start
            radius = .05 if name == "jaw" else .03 if name.startswith("mouth") else .024
            group = body.vertex_groups.new(name=name)
            for vertex in body.data.vertices:
                distance = (body.matrix_world @ vertex.co - start).length
                amount = .65 * max(0, 1 - distance / radius)
                if amount < .001:
                    continue
                for assignment in list(vertex.groups):
                    body.vertex_groups[assignment.group].add([vertex.index], assignment.weight * (1 - amount), "REPLACE")
                group.add([vertex.index], amount, "REPLACE")
    normalize_weights(body)
    return positions


def optional_anatomy(rig, sex, body):
    parts = ([('shaft_base', (0, -.091, .815), (.013, .014, .029), 'genital_01'),
              ('shaft_tip', (0, -.092, .779), (.014, .014, .018), 'genital_02'),
              ('scrotal_l', (.014, -.077, .821), (.016, .014, .021), 'genital_l'),
              ('scrotal_r', (-.014, -.077, .821), (.016, .014, .021), 'genital_r')]
             if sex == 'male' else
             [('labial_l', (.008, -.084, .847), (.007, .01, .024), 'genital_l'),
              ('labial_r', (-.008, -.084, .847), (.007, .01, .024), 'genital_r'),
              ('clitoral_hood', (0, -.089, .867), (.005, .006, .008), 'genital_01')])
    material = body.data.materials[0] if body.data.materials else bpy.data.materials.new('Optional anatomical surface')
    for label, location, scale, bone in parts:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=location, scale=scale)
        obj = bpy.context.object
        obj.name = 'Anatomy_' + label
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(material)
        for face in obj.data.polygons:
            face.use_smooth = True
        bind(obj, rig, bone)
        obj['default_visible'] = False
        obj['anatomical_attachment'] = True


def add_anchors(rig, sex, body):
    surface=KDTree(len(body.data.vertices))
    for vertex in body.data.vertices: surface.insert(body.matrix_world@vertex.co,vertex.index)
    surface.balance()
    eye_z=rig.data.bones['eye_l'].head_local.z
    locations={'anchor_mouth':Vector((0,-.16,eye_z-.055)),
               'anchor_navel':Vector((0,-.16,1.04)), 'anchor_anus':Vector((0,.12,.84)),
               'anchor_breast_l':Vector((.09,-.2,1.3)),
               'anchor_breast_r':Vector((-.09,-.2,1.3))}
    anchors = {'anchor_root':'root', 'anchor_hand_l':'hand_l', 'anchor_hand_r':'hand_r',
               'anchor_foot_l':'foot_l', 'anchor_foot_r':'foot_r', 'anchor_carry':'pelvis',
               'anchor_mouth':'jaw', 'anchor_genital':'genital_root', 'anchor_navel':'spine_01',
               'anchor_anus':'pelvis', 'anchor_urethral':'genital_01',
               'anchor_eye_l':'eye_l', 'anchor_eye_r':'eye_r',
               'anchor_breast_l':'spine_03', 'anchor_breast_r':'spine_03'}
    anchors.update({'anchor_penile':'genital_02','anchor_testicular':'genital_l'} if sex == 'male'
                   else {'anchor_vaginal':'genital_root'})
    for name, bone_name in anchors.items():
        bone = rig.data.bones[bone_name]
        obj = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(obj)
        obj.parent, obj.parent_type, obj.parent_bone = rig, 'BONE', bone_name
        obj.matrix_parent_inverse = Matrix.Identity(4)
        point=surface.find(locations[name])[0] if name in locations else bone.head_local
        local=bone.matrix_local.inverted()@point
        obj.matrix_basis = Matrix.Translation(local-Vector((0,bone.length,0)))


def add_actions(rig):
    motion.add_humanoid_locomotion_actions(rig,maximum_arm_drop=1.38)
    bone_map = {name:name for name in motion.REQUIRED_LOCOMOTION_BONES}
    for side in ('l', 'r'):
        action = bpy.data.actions.new('human_attack_' + side)
        frames = []
        for frame, angle, elbow in ((1,0,0),(3,.35,.7),(6,-.85,-.35),(12,0,0)):
            frames.append((frame, {f'upperarm_{side}': (((1,0,0),angle),),
                                   f'lowerarm_{side}': (((1,0,0),elbow),)}))
        motion._key_pose(rig, action, frames, bone_map, {}, {})
        action.use_cyclic = False
    for name, pose in {
        'human_wave': {'upperarm_r': (((0,1,0),-.95),((1,0,0),-.9)),
                       'lowerarm_r': (((1,0,0),-1.1),)},
        'expression_smile': {'mouth_corner_l': .008, 'mouth_corner_r': .008},
        'expression_sad': {'mouth_corner_l': -.004, 'mouth_corner_r': -.004, 'brow_l': .004, 'brow_r': .004},
        'expression_angry': {'brow_l': -.006, 'brow_r': -.006},
        'expression_surprised': {'brow_l': .012, 'brow_r': .012},
        'expression_blink': {'eyelid_l': -.01, 'eyelid_r': -.01},
    }.items():
        action = bpy.data.actions.new(name)
        rig.animation_data.action = action
        for frame, blend in ((1,0), (9,1), (28,1), (36,0)):
            bpy.context.scene.frame_set(frame)
            for bone_name, value in pose.items():
                bone = rig.pose.bones[bone_name]
                if isinstance(value, float):
                    axis = rig.data.bones[bone_name].matrix_local.to_3x3().inverted() @ Vector((0,0,1))
                    bone.location = axis * value * blend
                    bone.keyframe_insert('location', frame=frame)
                else:
                    bone.rotation_mode = 'QUATERNION'
                    rotation = Quaternion((1,0,0,0))
                    for world_axis, angle in value:
                        rotation @= Quaternion(motion._local_axis_for_world(rig.data.bones[bone_name], world_axis), angle*blend)
                    bone.rotation_quaternion = rotation
                    bone.keyframe_insert('rotation_quaternion', frame=frame)
        action.use_fake_user = True
        action.use_cyclic = False
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    rig.animation_data.action = bpy.data.actions['human_idle']
    bpy.context.scene.frame_set(1)


def relax_partial_actions(rig):
    """Facial previews retain a relaxed body rather than reverting to bind pose."""
    semantic={name:name for name in motion.REQUIRED_LOCOMOTION_BONES}
    for action in bpy.data.actions:
        if not action.name.startswith(('source_','expression_')) and action.name!='human_wave': continue
        rig.animation_data.action=action
        start,end=(int(value) for value in action.frame_range)
        for side in ('l','r'):
            if action.name=='human_wave' and side=='r': continue
            drop=motion._additional_arm_drop(rig,side,semantic,1.38)*(1 if side=='l' else -1)
            for name,axis,angle in ((f'upperarm_{side}',(0,1,0),drop),
                                    (f'lowerarm_{side}',(1,0,0),.12 if side=='l' else -.12)):
                bone=rig.pose.bones[name];bone.rotation_mode='QUATERNION'
                value=Quaternion(motion._local_axis_for_world(bone.bone,axis),angle)
                for frame in (start,end):
                    bpy.context.scene.frame_set(frame)
                    bone.rotation_quaternion=value
                    bone.keyframe_insert('rotation_quaternion',frame=frame)
    rig.animation_data.action=bpy.data.actions['human_idle']
    bpy.context.scene.frame_set(1)


def build(source, sex):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    names = tuple(f'GEO-body_{sex}_realistic'+suffix for suffix in ('','.eye.L','.eye.R'))
    with bpy.data.libraries.load(str(source), link=False) as (_, loaded):
        loaded.objects = list(names)
    body, *eyes = loaded.objects
    for obj in loaded.objects:
        bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()
    points = [body.matrix_world@v.co for v in body.data.vertices]
    lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    uniform = 1.75/(hi.z-lo.z)
    center = Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for obj in [body,*eyes]:
        obj.matrix_world = Matrix.Translation(-center*uniform) @ Matrix.Scale(uniform,4) @ obj.matrix_world
    body.name = 'HumanBody'
    rig = create_rig(body)
    bind(body,rig)
    additional_bones(body,eyes,rig,sex)
    clothing.add_cloth_bones(rig)
    for side,eye in zip(('l','r'),sorted(eyes,key=lambda obj:obj.matrix_world.translation.x,reverse=True)):
        eye.name = f'HumanEye_{side}'
        bind(eye,rig,f'eye_{side}')
    optional_anatomy(rig,sex,body)
    add_anchors(rig,sex,body)
    add_actions(rig)
    clothing.add_cloth_actions(rig)
    expression_report = expressions.add_source_expressions(body,rig)
    relax_partial_actions(rig)
    SOURCE_OUT.mkdir(parents=True,exist_ok=True)
    RUNTIME.mkdir(parents=True,exist_ok=True)
    (SOURCE_OUT/f'player_{sex}_expressions.json').write_text(
        json.dumps(expression_report,indent=2)+'\n',encoding='utf8',newline='\n')
    bpy.context.scene['source_asset'] = 'Blender Human Base Meshes v1.4.1 / '+names[0]
    bpy.context.scene['humanoid_rig_version'] = 1
    bpy.context.scene['genitalia_default_visible'] = False
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_OUT/f'player_{sex}.blend'),compress=True)
    # Export a runtime copy with the authored surface evaluated once. Keeping
    # Multires active while sampling every bone animation is both expensive
    # and can produce more than four interpolated influences per vertex.
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = bpy.data.meshes.new_from_object(body.evaluated_get(graph),
                                               preserve_all_data_layers=True, depsgraph=graph)
    body.data = evaluated
    for modifier in list(body.modifiers):
        body.modifiers.remove(modifier)
    for attribute in list(body.data.attributes):
        if attribute.name in {'custom_normal', 'sharp_face', 'sharp_edge'}:
            body.data.attributes.remove(attribute)
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    modifier = body.modifiers.new('Player skin', 'ARMATURE')
    modifier.object = rig
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    rig.data.pose_position = 'POSE'
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.gltf(filepath=str(RUNTIME/f'player_{sex}_base_rig_v1.glb'),
                             export_format='GLB',use_selection=True,export_apply=True,
                             export_animations=True,export_skins=True,export_yup=True,
                             export_frame_step=3)
    print(f'PLAYER BASE RIG PASSED: {sex}, {len(rig.data.bones)} bones, separate optional anatomy, no wearables.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--sex',choices=('male','female','both'),default='both')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    for sex in (('male','female') if args.sex=='both' else (args.sex,)):
        build(args.source,sex)
