"""Create editable Rigify IK/FK workspaces around canonical player deform rigs.

Rigify is an authoring dependency. A rest-offset bridge drives the unchanged
Humanoid Rig v1. Bake visual transforms on that rig before runtime GLB export;
never export metarigs, widgets, control bones or constraints to Godot.
"""
import json
from pathlib import Path
import sys

import addon_utils
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import player_clothing_support as clothing


def build(sex):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/f"art_source/characters/player_{sex}.blend"))
    if "rigify" not in bpy.context.preferences.addons:
        bpy.ops.preferences.addon_enable(module="rigify")
    from rigify.metarigs.Basic import basic_human
    canonical = bpy.data.objects["HumanoidRigV1"]
    canonical.animation_data_clear()
    canonical.data.pose_position = "POSE"
    for bone in canonical.pose.bones: bone.matrix_basis = Matrix.Identity(4)
    bpy.data.objects["HumanBody"].modifiers.new("Full body cloth collision","COLLISION")
    bpy.data.objects["HumanBody"].collision.thickness_outer=.005
    mask_checks=[]
    loose="aerith_aerith_pink_dress_dress" if sex=="female" else "radagon_male_radagon_default_dress"
    for item in ("work_shirt","work_trousers",loose):
        before=set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=str(ROOT/f"game/assets/appearance/{sex}/{item}.glb"))
        imported=[obj for obj in bpy.context.scene.objects if obj not in before]
        wearables=[obj for obj in imported if obj.type=="MESH" and obj.name=="Appearance_"+item]
        for obj in wearables:
            transform=obj.matrix_world.copy()
            obj.parent=canonical;obj.matrix_world=transform
            for modifier in obj.modifiers:
                if modifier.type=="ARMATURE": modifier.object=canonical
            obj["weight_source"]="Canonical body: nearest-face interpolated / inherited surface weights"
            body=bpy.data.objects["HumanBody"]
            count=clothing.native_mask(body,obj)
            modifier=body.modifiers[-1]
            if count==0 and item!=loose: raise RuntimeError("The starter garment did not cover any body vertices.")
            obj.hide_viewport=True;obj.update_tag();bpy.context.view_layer.update()
            if modifier.show_viewport: raise RuntimeError("Hiding clothing did not disable its body mask.")
            obj.hide_viewport=False;obj.update_tag();bpy.context.view_layer.update()
            obj.hide_render=True;obj.update_tag();bpy.context.view_layer.update()
            if modifier.show_render: raise RuntimeError("Render visibility did not disable its body mask.")
            obj.hide_render=False;obj.update_tag();bpy.context.view_layer.update()
            mask_checks.append({"item":item,"coveredCageVertices":count,"visibilityDriven":True})
            if item==loose:
                pin=obj.vertex_groups.new(name="Pinned")
                vertices=[v.index for v in obj.data.vertices if (obj.matrix_world@v.co).z>.99]
                pin.add(vertices,1,"REPLACE")
                simulation=obj.modifiers.new("Optional cloth bake (disabled)","CLOTH")
                simulation.settings.vertex_group_mass=pin.name
                simulation.collision_settings.distance_min=.005
                simulation.show_viewport=simulation.show_render=False
                obj.hide_viewport=obj.hide_render=True
        for obj in imported:
            if obj not in wearables: bpy.data.objects.remove(obj,do_unlink=True)
    meta_data = bpy.data.armatures.new("PlayerMetarig")
    meta = bpy.data.objects.new("PlayerMetarig", meta_data)
    bpy.context.collection.objects.link(meta)
    bpy.ops.object.select_all(action="DESELECT")
    meta.select_set(True)
    bpy.context.view_layer.objects.active = meta
    basic_human.create(meta)
    bpy.ops.object.mode_set(mode="OBJECT")
    mapping = {"spine": "pelvis", "spine.001": "spine_01", "spine.002": "spine_02",
               "spine.003": "spine_03", "spine.006": "head"}
    for side in ("l","r"):
        upper=side.upper()
        for source,target in (("shoulder","clavicle"),("upper_arm","upperarm"),
            ("forearm","lowerarm"),("hand","hand"),("thigh","thigh"),("shin","calf"),
            ("foot","foot"),("toe","toe")):
            mapping[f"{source}.{upper}"]=f"{target}_{side}"
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in meta.data.edit_bones: bone.use_connect=False
    for name,target in mapping.items():
        bone=meta.data.edit_bones[name];source=canonical.data.bones[target]
        bone.matrix=source.matrix_local.copy();bone.length=source.length
    neck=canonical.data.bones["neck_01"]
    middle=neck.head_local.lerp(neck.tail_local,.5)
    for name,start,end in (("spine.004",neck.head_local,middle),("spine.005",middle,neck.tail_local)):
        bone=meta.data.edit_bones[name];bone.head=start;bone.tail=end;bone.align_roll(Vector((0,-1,0)))
    for side in ("l","r"):
        upper=side.upper()
        hip=canonical.data.bones[f"thigh_{side}"].head_local
        pelvis=meta.data.edit_bones[f"pelvis.{upper}"]
        pelvis.head=canonical.data.bones["pelvis"].head_local
        pelvis.tail=hip+Vector((0,0,.03))
        heel=meta.data.edit_bones[f"heel.02.{upper}"]
        foot=canonical.data.bones[f"foot_{side}"]
        heel.head=foot.head_local+Vector((-.025,.03,-.025))
        heel.tail=heel.head+Vector((.05,0,0))
        for finger in ("thumb","index","middle","ring","pinky"):
            parent=meta.data.edit_bones[f"hand.{upper}"]
            for number in range(1,4):
                name=f"{finger}_{number:02}_{side}"
                source=canonical.data.bones[name]
                bone=meta.data.edit_bones.new(name)
                bone.matrix=source.matrix_local.copy();bone.length=source.length
                bone.parent=parent;bone.use_connect=number>1
                parent=bone;mapping[name]=name
    extras=[bone.name for bone in canonical.data.bones if bone.name.startswith(
        ("genital_","eye_","eyelid_","brow_","mouth_corner_","cloth_")) or bone.name=="jaw"]
    for name in extras:
        source=canonical.data.bones[name];bone=meta.data.edit_bones.new(name)
        bone.matrix=source.matrix_local.copy();bone.length=source.length
        parent=source.parent.name
        bone.parent=meta.data.edit_bones.get(parent) or meta.data.edit_bones[
            "spine" if parent=="pelvis" else "spine.003" if parent=="spine_03" else "spine.006"]
        mapping[name]=name
    for name in ("spine.001","spine.002","spine.003","spine.005","spine.006"):
        meta.data.edit_bones[name].use_connect=True
    for side in ("L","R"):
        for name in ("shin","foot","toe","forearm","hand"):
            meta.data.edit_bones[f"{name}.{side}"].use_connect=True
    bpy.ops.object.mode_set(mode="OBJECT")
    meta.data.use_mirror_x=True
    for side in ("l","r"):
        for finger in ("thumb","index","middle","ring","pinky"):
            meta.pose.bones[f"{finger}_01_{side}"].rigify_type="limbs.super_finger"
            params=meta.pose.bones[f"{finger}_01_{side}"].rigify_parameters
            for kind,collection_name in (("primary",f"Arm.{side.upper()} (FK)"),
                                         ("tweak",f"Arm.{side.upper()} (Tweak)")):
                refs=getattr(params,kind+"_coll_refs",None)
                if refs is not None:
                    refs.add().set_collection(meta.data.collections[collection_name])
            for number in range(1,4):
                meta.data.collections[f"Arm.{side.upper()} (FK)"].assign(
                    meta.data.bones[f"{finger}_{number:02}_{side}"])
    for label,row in (("Face",11),("Anatomy",12),("Cloth",14)):
        collection=meta.data.collections.new(label)
        collection.rigify_ui_row=row
    for name in extras:
        bone=meta.pose.bones[name];bone.rigify_type="basic.super_copy"
        bone.rigify_parameters.make_control=True
        bone.rigify_parameters.make_deform=True
        bone.rigify_parameters.make_widget=True
        meta.data.collections["Cloth" if name.startswith("cloth_") else
                             "Anatomy" if name.startswith("genital_") else "Face"].assign(bone.bone)
    bpy.ops.pose.rigify_generate()
    controls=bpy.context.object
    controls.name="PlayerControls"
    controls.show_in_front=True
    for bone in controls.pose.bones:
        if "IK_FK" in bone: bone["IK_FK"]=1.0
    meta.hide_set(True);meta.hide_render=True
    targets={target:"DEF-"+source for source,target in mapping.items()}
    targets["root"]="root"
    # Independent bridge bones apply only each control's delta from its rest
    # transform. Canonical parents cannot multiply a copied parent delta twice.
    bpy.context.view_layer.objects.active=controls
    bpy.ops.object.mode_set(mode="EDIT")
    for name,target in targets.items():
        if target not in controls.data.edit_bones:
            raise RuntimeError(f"Rigify did not generate {target}.")
        bone=controls.data.edit_bones.new("BRIDGE_"+name)
        source=canonical.data.bones[name]
        bone.matrix=source.matrix_local.copy();bone.length=source.length
        bone.use_deform=False
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    for name,target in targets.items():
        bridge=controls.pose.bones["BRIDGE_"+name]
        constraint=bridge.constraints.new("CHILD_OF")
        constraint.target=controls;constraint.subtarget=target
        constraint.use_scale_x=constraint.use_scale_y=constraint.use_scale_z=False
        constraint.inverse_matrix=(controls.matrix_world@controls.pose.bones[target].matrix).inverted()
        follow=canonical.pose.bones[name].constraints.new("COPY_TRANSFORMS")
        follow.name="Rigify authoring bridge"
        follow.target=controls;follow.subtarget=bridge.name
        follow.target_space=follow.owner_space="WORLD"
        follow.remove_target_shear=True
        controls.data.bones[bridge.name].hide=True
    canonical.hide_set(True)
    for obj in bpy.context.scene.objects:
        if obj.name.startswith("Anatomy_"): obj.hide_render=True;obj.hide_set(True)
    bpy.context.view_layer.update()
    rest_error=max((canonical.pose.bones[name].matrix.translation-
                    canonical.data.bones[name].matrix_local.translation).length for name in targets)
    if rest_error>.001: raise RuntimeError(f"Rigify changed the neutral pose by {rest_error}m.")
    bpy.context.scene["authoring_notes"]="Use PlayerControls IK/FK widgets. Body and clothing retain canonical weights. Bake visual keys on HumanoidRigV1 before GLB export."
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/f"art_source/characters/player_{sex}_controls.blend"),compress=True)
    before=canonical.pose.bones["hand_l"].matrix.translation.copy()
    control=controls.pose.bones["upper_arm_fk.L"]
    control.rotation_mode="XYZ";control.rotation_euler.x=.3
    bpy.context.view_layer.update()
    distance=(canonical.pose.bones["hand_l"].matrix.translation-before).length
    if distance<.02: raise RuntimeError("Rigify arm control did not drive the canonical hand.")
    before_foot=canonical.pose.bones["foot_l"].matrix.translation.copy()
    controls.pose.bones["thigh_parent.L"]["IK_FK"]=0.0
    controls.pose.bones["foot_ik.L"].location.x+=.05
    bpy.context.view_layer.update()
    foot_distance=(canonical.pose.bones["foot_l"].matrix.translation-before_foot).length
    if foot_distance<.02: raise RuntimeError("Rigify foot IK did not drive the canonical foot.")
    before_hem=canonical.pose.bones["cloth_skirt_front_02_l"].matrix.translation.copy()
    hem=controls.pose.bones["cloth_skirt_front_01_l"]
    hem.rotation_mode="XYZ";hem.rotation_euler.x=.2
    bpy.context.view_layer.update()
    hem_distance=(canonical.pose.bones["cloth_skirt_front_02_l"].matrix.translation-before_hem).length
    if hem_distance<.01: raise RuntimeError("Rigify cloth tweak did not drive the canonical hem.")
    bpy.ops.object.select_all(action="DESELECT")
    canonical.hide_set(False);canonical.select_set(True)
    bpy.context.view_layer.objects.active=canonical
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    expected={name:canonical.pose.bones[name].matrix.copy() for name in targets}
    bpy.ops.nla.bake(frame_start=1,frame_end=1,only_selected=True,visual_keying=True,
        clear_constraints=True,clear_parents=False,use_current_action=False,bake_types={"POSE"})
    bpy.context.view_layer.update()
    bake_error=max((canonical.pose.bones[name].matrix.translation-expected[name].translation).length for name in targets)
    if bake_error>.001: raise RuntimeError(f"Visual bake changed the driven pose by {bake_error}m.")
    bpy.ops.object.mode_set(mode="OBJECT")
    report={"sex":sex,"rigifyControls":len(controls.data.bones),"canonicalBones":len(canonical.data.bones),
        "restPositionErrorM":rest_error,"armControlHandDisplacementM":distance,
        "footIkDisplacementM":foot_distance,
        "bodyMasks":mask_checks,
        "clothTweakDisplacementM":hem_distance,
        "visualBakePositionErrorM":bake_error,"status":"PASS"}
    (ROOT/f"art_source/characters/player_{sex}_controls.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf8",newline="\n")
    print("PLAYER RIGIFY AUTHORING PASSED",sex,report)


for sex in ("male","female"):
    build(sex)
