"""Convert separate acquired wearables to each clean player's canonical rig.

Retarget source rest-bone directions, preserve UV/materials, enforce body
clearance, and transfer normalized weights from the target body surface.
The output is separate equipment GLBs, never replacement NPC body meshes.
"""
from __future__ import annotations
import argparse
import importlib.util
import json
import math
from pathlib import Path
import re
import struct
import sys
import tempfile

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'game/assets/appearance'
MANIFEST = ROOT/'game/data/player_appearance.json'
REPORT = ROOT/'art_source/characters/wardrobe_report.json'
sys.path.insert(0,str(Path(__file__).parent))
import player_clothing_support as clothing
spec=importlib.util.spec_from_file_location('npc_material_helpers',ROOT/'art_source/blender/export_starter_npc_models_v1.py')
materials=importlib.util.module_from_spec(spec)
sys.modules[spec.name]=materials
spec.loader.exec_module(materials)


def category(name):
    name=name.lower()
    if any(word in name for word in ('eyelash','eyebrow','teeth','tongue','eyeball','nude','genital','penis','vagina')):
        return None
    if any(word in name for word in ('hair','mustache','beard')): return 'hair'
    if any(word in name for word in ('jacket','coat')): return 'outerwear'
    if any(word in name for word in ('bracelet','watch','necklace','belt','choker')): return 'accessory'
    if any(word in name for word in ('earring','pauldron','hips','cloth_front','cloth_back')): return 'accessory'
    if any(word in name for word in ('helmet','head wrap','head gear','headgear','glasses','ears','hat','veil')): return 'head'
    if any(word in name for word in ('boot','shoe','sandal','heel','socks')): return 'feet'
    if any(word in name for word in ('glove','sleeve','arm guard','arms','elbow')): return 'hands'
    if any(word in name for word in ('pants','panties','trouser','stocking','bottum','bottom','legging','shorts','thighstrap','skirt','armor_leg','armor_knee')): return 'legs'
    if any(word in name for word in ('jacket','shirt','dress','bodysuit','body suit','bunnysuit','vest','chest','top','under amour','leotard','swimsuit','armor','armour','coat')) or re.search(r'(?:^|[_ .-])bra(?:$|[_ .-])',name): return 'torso'
    if any(word in name for word in ('bracelet','watch','necklace','belt','choker','chain','strap','cape','bandage','accessor','headband','handband','kunai','sickle','weapon','sword')): return 'accessory'
    return None


def slug(name):
    return re.sub('[^a-z0-9]+','_',name.lower()).strip('_')


def write_catalog(entries):
    MANIFEST.parent.mkdir(parents=True,exist_ok=True)
    MANIFEST.write_text(json.dumps({'schemaVersion':1,'items':list(entries.values())},indent=2)+'\n',encoding='utf8',newline='\n')
    domain=[{k:v for k,v in entry.items() if k not in {'assets','source','sourceMesh','bodyMasks','drape'}} for entry in entries.values()]
    domain_path=ROOT/'data/player_appearance.json'
    if domain_path.exists():
        domain += [entry for entry in json.loads(domain_path.read_text())
                   if entry['id'].startswith('custom_') and entry['id'] not in entries
                   and (ROOT/'game/data/outfits'/(entry['id']+'.tres')).is_file()]
    domain_path.write_text(json.dumps(domain,indent=2)+'\n',encoding='utf8',newline='\n')
    write_resources(entries)


def write_resources(entries):
    folder=ROOT/'game/data/outfits'
    folder.mkdir(parents=True,exist_ok=True)
    slots={'head':0,'torso':1,'legs':2,'feet':3,'hands':4,'accessory':5,'hair':6,'outerwear':7}
    for entry in entries.values():
        destination=folder/(entry['id']+'.tres')
        if destination.exists(): continue
        masks=entry.get('bodyMasks',{})
        lines=[f'[gd_resource type="Resource" script_class="EquipableItem" load_steps={2+len(masks)} format=3]','',
               '[ext_resource type="Script" path="res://scripts/EquipableItem.cs" id="1"]']
        for sex,path in masks.items():
            lines.append(f'[ext_resource type="Texture2D" path={json.dumps(path)} id="mask_{sex.lower()}"]')
        lines+=['','[resource]','script = ExtResource("1")',
            'ItemId = '+json.dumps(entry['id']),'ItemName = '+json.dumps(entry['name']),
            'Slot = '+str(slots[entry['slot']]),
            'MeshNodeNames = Array[String](['+json.dumps('Appearance_'+entry['id'])+'])']
        for sex,path in entry['assets'].items(): lines.append(sex+'ModelPath = '+json.dumps(path))
        for sex in masks: lines.append(sex+'SkinMask = ExtResource("mask_'+sex.lower()+'")')
        if entry.get('drape'): lines.append('Drape = '+str({'skirt':1,'cape':2}[entry['drape']]))
        destination.write_text('\n'.join(lines)+'\n',encoding='utf8',newline='\n')


def fitted_starter_garments(entries,report):
    """Duplicate/offset body surfaces; never warp a cylindrical legacy shell."""
    for sex in ('male','female'):
        bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'art_source/characters/player_{sex}.blend'))
        body=bpy.data.objects['HumanBody']
        rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
        rig.data.pose_position='REST'
        for modifier in body.modifiers:
            if modifier.type=='MULTIRES': modifier.levels=1
        bpy.context.view_layer.update()
        graph=bpy.context.evaluated_depsgraph_get()
        for item_id,slot,color in (
            ('work_shirt','torso',(.23,.30,.15,1)),('work_trousers','legs',(.09,.13,.12,1)),
            ('deluxe_bunny_suit_top','torso',(.38,.035,.09,1)),
            ('deluxe_bunny_suit_legs','legs',(.065,.045,.055,1))):
            mesh=bpy.data.meshes.new_from_object(body.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
            regions=mesh.attributes.get('.sculpt_face_set')
            if regions is None: raise RuntimeError('Starter garment extraction needs source face sets.')
            keep=set()
            for face in mesh.polygons:
                region=regions.data[face.index].value
                point=sum((body.matrix_world@mesh.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
                if slot=='torso':
                    selected=(region in {1,19} and .99<point.z<1.45) or (region in {20,21} and abs(point.x)<.25)
                else:
                    selected=(region in {18,23,24,15,16} and point.z>.10) or (region==19 and point.z<1.03)
                if selected: keep.add(face.index)
            obj=bpy.data.objects.new('Appearance_'+item_id,mesh)
            bpy.context.collection.objects.link(obj)
            obj.matrix_world=body.matrix_world.copy()
            bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm,geom=[face for face in bm.faces if face.index not in keep],context='FACES')
            bm.to_mesh(mesh);bm.free()
            bpy.context.view_layer.update()
            for vertex in mesh.vertices:
                vertex.co += vertex.normal*(.012 if slot=='torso' else .006)
            material=bpy.data.materials.new(item_id+' fabric')
            material.use_nodes=True
            shader=material.node_tree.nodes['Principled BSDF']
            shader.inputs['Base Color'].default_value=color
            shader.inputs['Roughness'].default_value=.85
            mesh.materials.clear();mesh.materials.append(material)
            for face in mesh.polygons: face.material_index=0;face.use_smooth=True
            obj.parent=rig
            skin=obj.modifiers.new('Canonical player skin','ARMATURE');skin.object=rig
            shell=obj.modifiers.new('Garment thickness 3mm','SOLIDIFY');shell.thickness=.003;shell.offset=1
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
            dest=OUT/sex/(item_id+'.glb')
            bpy.ops.export_scene.gltf(filepath=str(dest),export_format='GLB',use_selection=True,
                export_animations=False,export_skins=True,export_apply=True,export_yup=True)
            entries[item_id]['name']=item_id.replace('_',' ').title()
            entries[item_id]['slot']=slot
            entries[item_id]['coversGenitalia']=slot=='legs'
            entries[item_id]['drape']=None
            entries[item_id]['weightKg']={'work_shirt':.7,'work_trousers':.9,
                'deluxe_bunny_suit_top':.8,'deluxe_bunny_suit_legs':.65}[item_id]
            entries[item_id]['source']='Blender Human Base Meshes v1.4.1'
            entries[item_id]['sourceMesh']='HumanBody / duplicated fitted '+slot+' surface'
            mask,faces=clothing.write_body_mask(body,obj,item_id,sex)
            entries[item_id].setdefault('bodyMasks',{})[sex.title()]=mask
            report[:]=[row for row in report if not (row['id']==item_id and row['sex']==sex)]
            report.append({'id':item_id,'sex':sex,'source':'Blender Human Base Meshes v1.4.1',
                'mesh':'HumanBody surface','method':f'duplicate, offset {12 if slot=="torso" else 6}mm, solidify 3mm, inherited skin weights',
                'vertices':len(mesh.vertices),'status':'converted'})
            bpy.data.objects.remove(obj,do_unlink=True)


def target_surface(body,rig):
    rig.data.pose_position='REST'
    for modifier in body.modifiers:
        if modifier.type=='MULTIRES': modifier.levels=1
    bpy.context.view_layer.update()
    graph=bpy.context.evaluated_depsgraph_get()
    mesh=bpy.data.meshes.new_from_object(body.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
    mesh.calc_loop_triangles()
    points=[body.matrix_world@v.co for v in mesh.vertices]
    triangles=[tuple(t.vertices) for t in mesh.loop_triangles]
    tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
    names={g.index:g.name for g in body.vertex_groups}
    weights=[{names[g.group]:g.weight for g in v.groups} for v in mesh.vertices]
    return tree,points,triangles,weights


def source_mapper(source_rig,target_rig,source_body,target_body,bone_map,flip,normalized=False,reference_meshes=()):
    raw=[obj.matrix_world@v.co for obj in ([source_body] if source_body else reference_meshes) for v in obj.data.vertices]
    if not raw and not normalized: raise RuntimeError('A source needs a full body or measured scene bounds for scale.')
    if normalized:
        scale=1;center=Vector()
    else:
        lo=Vector(tuple(min(p[i] for p in raw) for i in range(3)))
        hi=Vector(tuple(max(p[i] for p in raw) for i in range(3)))
        scale=1.75/max(.1,hi.z-lo.z)
        center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    orientation=Matrix.Rotation(math.pi if flip else 0,4,'Z')
    normalize=orientation @ Matrix.Scale(scale,4) @ Matrix.Translation(-center)
    source_tree=BVHTree.FromPolygons([normalize @ p for p in raw],
        [tuple(p.vertices) for p in source_body.data.polygons]) if source_body and source_body.data.polygons else None
    inverse={source:semantic for semantic,source in bone_map.items()}
    if source_rig:
        for bone in source_rig.data.bones:
            clean=bone.name.replace('DEF-','').replace('ORG-','')
            plain=re.sub(r'\.(?:bend|twist|twk)(?=\.|$)','',clean)
            if plain in inverse:inverse[bone.name]=inverse[plain]
            if plain in target_rig.data.bones:inverse[bone.name]=plain
            if clean in inverse: inverse[bone.name]=inverse[clean]
            for side,aliases in (('l',('.L','.l','_l',' Left')),('r',('.R','.r','_r',' Right'))):
                if any(clean.endswith(suffix) for suffix in aliases):
                    lower=clean.lower()
                    suffix=next(suffix for suffix in aliases if clean.endswith(suffix))
                    stem=re.sub(r'\.(?:bend|twist|twk)(?=\.|$)','',clean[:-len(suffix)].lower().removeprefix('c_'))
                    arp={'arm_stretch':'upperarm','arm_twist':'upperarm',
                         'forearm_stretch':'lowerarm','forearm_twist':'lowerarm',
                         'thigh_stretch':'thigh','thigh_twist':'thigh',
                         'leg_stretch':'calf','leg_twist':'calf'}
                    if stem in arp: inverse[bone.name]=f'{arp[stem]}_{side}'
                    conventional={'upper_arm':'upperarm','upperarm':'upperarm','arm':'upperarm',
                                  'forearm':'lowerarm','lowerarm':'lowerarm','lower_arm':'lowerarm',
                                  'thigh':'thigh','upperleg':'thigh','shin':'calf','calf':'calf',
                                  'leg':'calf','foot':'foot','hand':'hand'}
                    if stem in conventional: inverse[bone.name]=f'{conventional[stem]}_{side}'
                    finger=re.fullmatch(r'(?:f_)?(thumb|index|middle|ring|pinky)[._](0?[123])',stem)
                    if finger:inverse[bone.name]=f'{finger[1]}_{int(finger[2]):02}_{side}'
                    if 'hand' in lower and 'finger' not in lower: inverse[bone.name]=f'hand_{side}'
                    if any(word in lower for word in ('clavicle','shoulder')): inverse[bone.name]=f'clavicle_{side}'
            if clean in target_rig.data.bones: inverse[bone.name]=clean
    mappings={}
    if source_rig:
        joints={semantic:source_rig.data.bones[source] for semantic,source in bone_map.items()
                if source in source_rig.data.bones}
        for name,semantic in sorted(inverse.items(),key=lambda pair:('twist' in pair[0], 'stretch' not in pair[0])):
            if name in source_rig.data.bones: joints.setdefault(semantic,source_rig.data.bones[name])
        if 'upperarm_l' in joints and 'upperarm_r' in joints:
            left=normalize@source_rig.matrix_world@joints['upperarm_l'].head_local
            right=normalize@source_rig.matrix_world@joints['upperarm_r'].head_local
            if left.x<=right.x:
                raise RuntimeError('Source orientation reverses anatomical left/right; correct the intake forward direction.')
        next_joint={f'{first}_{side}':f'{second}_{side}' for side in ('l','r')
                    for first,second in (('upperarm','lowerarm'),('lowerarm','hand'),
                                         ('thigh','calf'),('calf','foot'))}
        next_joint.update({f'{finger}_{number:02}_{side}':f'{finger}_{number+1:02}_{side}'
            for side in ('l','r') for finger in ('thumb','index','middle','ring','pinky') for number in (1,2)})
        for bone in source_rig.data.bones:
            ancestor=bone
            while ancestor and ancestor.name not in inverse:
                ancestor=ancestor.parent
            if not ancestor or inverse[ancestor.name] not in target_rig.data.bones: continue
            semantic=inverse[ancestor.name]
            target=target_rig.data.bones[semantic]
            start=normalize @ source_rig.matrix_world @ ancestor.head_local
            end=normalize @ source_rig.matrix_world @ ancestor.tail_local
            target_start=target_rig.matrix_world @ target.head_local
            target_end=target_rig.matrix_world @ target.tail_local
            # Imported joint tails can be arbitrary display lengths (ARP stretch
            # controls here are over a metre long). Use anatomical joint heads
            # for segment alignment and scale, including twist/helper aliases.
            anatomical_segment=semantic in next_joint and next_joint[semantic] in joints
            if anatomical_segment:
                start=normalize@source_rig.matrix_world@joints[semantic].head_local
                end=normalize@source_rig.matrix_world@joints[next_joint[semantic]].head_local
                target_end=target_rig.matrix_world@target_rig.data.bones[next_joint[semantic]].head_local
            source_direction=end-start
            target_direction=target_end-target_start
            if source_direction.length<1e-5 or target_direction.length<1e-5: continue
            rotation=source_direction.rotation_difference(target_direction)
            ratio=target_direction.length/source_direction.length
            ratio=max(.2,min(5,ratio)) if anatomical_segment else max(.7,min(1.4,ratio))
            if semantic in {'root','pelvis','spine_01','spine_02','spine_03','neck_01','head'}:
                # Torso/head display-bone tails are not a body-scale contract.
                # Sources may point their pelvis bone down rather than up.
                # Preserve the normalized anatomical frame and move its joint;
                # surface fitting handles the remaining proportion difference.
                rotation=Matrix.Identity(3).to_quaternion();ratio=1
            mappings[bone.name]=(start,target_start,rotation,ratio)

    # Breast helper bones are often detached controls, not descendants of a
    # named spine bone. Fit these deformation regions to measured body peaks;
    # a skeletal chest joint is not the surface landmark of a garment cup.
    source_points=[normalize@p for p in raw] if source_body else []
    target_points=[target_body.matrix_world@v.co for v in target_body.data.vertices]
    for sign in (-1,1):
        def peak(points):
            candidates=[p for p in points if .03<sign*p.x<.14 and 1.16<p.z<1.42]
            return min(candidates,key=lambda p:p.y) if candidates else None
        source_peak,target_peak=peak(source_points),peak(target_points)
        if source_peak is None or target_peak is None or not source_rig: continue
        for bone in source_rig.data.bones:
            position=normalize@source_rig.matrix_world@bone.head_local
            if any(word in bone.name.lower() for word in ('breast','pectoral')) and sign*position.x>0:
                mappings[bone.name]=(source_peak,target_peak,Matrix.Identity(3).to_quaternion(),1)

    def point(obj,vertex):
        common=normalize @ obj.matrix_world @ vertex.co
        values=[]
        for group in vertex.groups:
            name=obj.vertex_groups[group.group].name
            if name in mappings and group.weight>.001:
                start,target,rotation,ratio=mappings[name]
                values.append((target+rotation@(common-start)*ratio,group.weight))
        total=sum(weight for _,weight in values)
        return sum((p*w for p,w in values),Vector())/total if total>.01 else common
    def clearance(obj,vertex):
        if source_tree is None: return 0
        common=normalize @ obj.matrix_world @ vertex.co
        hit=source_tree.find_nearest(common)
        # Retain the signed inner/outer shell offsets. Clamping both to zero
        # collapses a source garment's thickness when its inner shell is skin-side.
        limit=.12 if any(word in obj.name.lower() for word in ('armor','armour','amour')) else .03
        return max(-.004,min(limit,(common-hit[0]).dot(hit[1]))) if hit[0] is not None else 0
    clearance.has_body=source_tree is not None
    return point,clearance


def original_body_reference(specification,npc_sources,source_rig):
    """Recover masked NPC skin as a measurement surface, never as wearable art."""
    if not specification.get('original'):return None
    if npc_sources is None or source_rig is None:
        raise RuntimeError('NPC fitting requires --npc-sources and its exported deform skeleton.')
    path=npc_sources/specification['original']
    with bpy.data.libraries.load(str(path),link=False) as (_,loaded):
        loaded.objects=[specification['body'],specification['armature']]
    original_body,original_rig=loaded.objects
    if original_body is None or original_rig is None or original_body.type!='MESH':
        raise RuntimeError(f"Missing original skin/rig reference in {path.name}.")
    linked=[]
    def link_chain(obj):
        if obj.parent:link_chain(obj.parent)
        if obj.name not in bpy.context.scene.objects:
            bpy.context.collection.objects.link(obj);linked.append(obj)
    link_chain(original_body);link_chain(original_rig)
    original_rig.data.pose_position='REST'
    bpy.context.view_layer.update()
    pairs=[(original_rig.data.bones[bone.name],bone) for bone in source_rig.data.bones if bone.name in original_rig.data.bones]
    if len(pairs)<3:raise RuntimeError(f"{specification['slug']} needs three matching deform joints for reference alignment.")
    a=np.array([original_rig.matrix_world@first.head_local for first,second in pairs])
    b=np.array([source_rig.matrix_world@second.head_local for first,second in pairs])
    ac,bc=a.mean(axis=0),b.mean(axis=0)
    u,s,vt=np.linalg.svd((a-ac).T@(b-bc))
    correction=np.ones(3);correction[-1]=np.linalg.det(vt.T@u.T)
    rotation=vt.T@np.diag(correction)@u.T
    scale=float((s*correction).sum()/np.square(a-ac).sum())
    translation=bc-scale*rotation@ac
    error=float(np.linalg.norm((scale*(a@rotation.T)+translation)-b,axis=1).max())
    if error>.01:raise RuntimeError(f"{specification['slug']} original body frame alignment differs by {error:.4f}m.")
    transform=Matrix.Identity(4)
    for row in range(3):
        for col in range(3):transform[row][col]=float(scale*rotation[row,col])
        transform[row][3]=float(translation[row])
    world=transform@original_body.matrix_world
    points=[world@vertex.co for vertex in original_body.data.vertices]
    faces=[tuple(face.vertices) for face in original_body.data.polygons]
    if world.determinant()<0:faces=[tuple(reversed(face)) for face in faces]
    mesh=bpy.data.meshes.new('Original skin reference');mesh.from_pydata(points,[],faces);mesh.update()
    reference=bpy.data.objects.new('Reference_'+specification['body'],mesh)
    reference['alignment_error_m']=error
    for obj in reversed(linked):
        bpy.data.objects.remove(obj,do_unlink=True)
    return reference


def portable_materials(obj,images,already_portable=False,npc_sources=None):
    notes=[]
    for index,material in enumerate(list(obj.data.materials)):
        if material is None: continue
        copied=material.copy()
        obj.data.materials[index]=copied
        recovery=None
        # The earlier Aerith NPC export baked its custom hair graph to an
        # entirely transparent image. Recover the exact diffuse/opacity pair
        # from that source material, never a scene-wide filename guess.
        if material.name.startswith('aerith_Aerith Hair Default_0_Aerith Hair'):
            texture=next((node.image for node in copied.node_tree.nodes if node.type=='TEX_IMAGE'),None)
            pixels=np.array(texture.pixels[:]) if texture else np.array([])
            if not pixels.size or not pixels.any():
                if npc_sources is None: raise RuntimeError('Aerith hair recovery needs --npc-sources pointing to original NPC sources.')
                folder=npc_sources/'aerith/textures'
                diffuse=bpy.data.images.load(str(folder/'StandardPC0003_00_Hair_C.jpg'),check_existing=False)
                opacity=bpy.data.images.load(str(folder/'StandardAlphaPC0003_00_Hair_A.jpg'),check_existing=False)
                materials._make_principled_material(copied,diffuse,[diffuse],True)
                alpha=copied.node_tree.nodes.new('ShaderNodeTexImage');alpha.image=opacity
                opacity.colorspace_settings.name='Non-Color'
                copied.node_tree.links.new(alpha.outputs['Color'],copied.node_tree.nodes['Principled BSDF'].inputs['Alpha'])
                copied.use_backface_culling=False
                recovery={'diffuse':Path(diffuse.filepath).name,'opacity':Path(opacity.filepath).name,
                          'reason':'source NPC GLB contains an entirely transparent hair bake'}
        local_images=[node.image for node in materials._material_nodes(copied)
                      if node.type=='TEX_IMAGE' and node.image is not None]
        if not already_portable and not materials._gltf_native_base_color(copied):
            image=materials._best_material_image(copied,'base',local_images)
            if image is not None:
                materials._make_principled_material(copied,image,local_images,
                    materials._material_is_transparent(copied) or 'hair' in obj.name.lower())
            else:
                raise RuntimeError(f"Material {material.name} requires an explicit shader bake; no local albedo map.")
        if copied.use_nodes:
            for node in copied.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image and max(node.image.size)>2048:
                    resized=node.image.copy()
                    width,height=resized.size
                    ratio=2048/max(width,height)
                    resized.scale(max(1,int(width*ratio)),max(1,int(height*ratio)))
                    # Reload resampled bytes rather than retaining the source
                    # image's stale packed payload at its original resolution.
                    path=Path(tempfile.gettempdir())/'player_wardrobe_textures'/(slug(obj.name+'_'+str(index)+'_'+node.name)+'.png')
                    path.parent.mkdir(parents=True,exist_ok=True)
                    resized.filepath_raw=str(path);resized.file_format='PNG';resized.save()
                    portable=bpy.data.images.load(str(path),check_existing=False)
                    portable.colorspace_settings.name=node.image.colorspace_settings.name
                    portable.name=node.image.name+'_2048'
                    portable.pack()
                    node.image=portable
        notes.append({'material':material.name,'images':[image.name for image in local_images],
                      'method':'source diffuse/opacity recovery' if recovery else 'preserved native PBR' if already_portable or materials._gltf_native_base_color(material) else 'material-local PBR conversion',
                      **({'recovery':recovery} if recovery else {})})
    return notes


def fit_piece(source,rig,surface,mapping,item_id):
    tree,points,triangles,body_weights=surface
    mapper,source_clearance=mapping
    graph=bpy.context.evaluated_depsgraph_get()
    mesh=bpy.data.meshes.new_from_object(source.evaluated_get(graph),
        preserve_all_data_layers=True,depsgraph=graph)
    # More fitting samples prevent long source triangles from cutting through
    # convex target anatomy even when their corner vertices clear the skin.
    if category(source.name) in {'torso','outerwear'} and len(mesh.polygons)<30000:
        subdivided=bmesh.new();subdivided.from_mesh(mesh)
        bmesh.ops.subdivide_edges(subdivided,edges=list(subdivided.edges),cuts=1,use_grid_fill=True)
        subdivided.to_mesh(mesh);subdivided.free()
    obj=bpy.data.objects.new('Appearance_'+item_id,mesh)
    bpy.context.collection.objects.link(obj)
    if obj.data.shape_keys:
        obj.shape_key_clear()
    for attribute in list(mesh.attributes):
        if attribute.name in {'custom_normal','sharp_face','sharp_edge'}:
            mesh.attributes.remove(attribute)
    weights=[]
    projected=0
    kind=clothing.drape_kind(source.name)
    gap=.022 if category(source.name)=='outerwear' else .006
    fit_outside=source_clearance.has_body and category(source.name) in {'torso','outerwear','legs','hands'} and 'fan' not in source.name.lower()
    for vertex in mesh.vertices:
        point=mapper(source,vertex)
        hit=tree.find_nearest(point)
        if hit[0] is None: raise RuntimeError('The target body has no fitting surface.')
        closest,normal,face_index,distance=hit
        allowed=gap+source_clearance(source,vertex)
        free_drape=kind=='cape' or (kind=='skirt' and point.z<.99)
        if (point-closest).dot(normal)<gap or (fit_outside and not free_drape and distance>allowed+.002):
            point=closest+normal*allowed
            projected+=1
        vertex.co=point
        a,b,c=triangles[face_index]
        bary=barycentric_transform(closest,points[a],points[b],points[c],Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
        combined={}
        for vi,amount in zip((a,b,c),bary):
            for name,weight in body_weights[vi].items():
                combined[name]=combined.get(name,0)+max(0,amount)*weight
        combined=clothing.drape_weights(point,kind,combined)
        top=sorted(combined.items(),key=lambda pair:pair[1],reverse=True)[:4]
        total=sum(weight for _,weight in top)
        if total<1e-8: raise RuntimeError('A garment vertex has no skin weight.')
        weights.append([(name,weight/total) for name,weight in top])
    # A copied/evaluated Mesh carries its source deform-layer indices even
    # though the new Object has no matching vertex-group table.
    obj.vertex_groups.clear()
    clean=bmesh.new()
    clean.from_mesh(mesh)
    deform=clean.verts.layers.deform.active
    if deform is not None:
        for vertex in clean.verts: vertex[deform].clear()
    clean.to_mesh(mesh)
    clean.free()
    groups={name:obj.vertex_groups.new(name=name) for name in {name for weights_at_vertex in weights for name,_ in weights_at_vertex}}
    for vi,values in enumerate(weights):
        for name,weight in values: groups[name].add([vi],weight,'REPLACE')
    obj.parent=rig
    modifier=obj.modifiers.new('Canonical player skin','ARMATURE')
    modifier.object=rig
    if category(source.name) in {'torso','outerwear','legs','hands','feet'}:
        topology=bmesh.new();topology.from_mesh(mesh)
        open_surface=any(edge.is_boundary for edge in topology.edges)
        topology.free()
        if open_surface:
            # Closed source shells keep their authored depth. Open fabric gets
            # physical depth after fitting, so projection cannot flatten it.
            shell=obj.modifiers.new('Open garment thickness 3mm','SOLIDIFY')
            shell.thickness=.003;shell.offset=1
    mesh.update()
    for vertex in mesh.vertices:
        if abs(sum(g.weight for g in vertex.groups)-1)>.001 or not vertex.groups:
            raise RuntimeError(f"Invalid converted weights on {item_id} vertex {vertex.index}.")
    if any(group.name not in rig.data.bones for group in obj.vertex_groups):
        raise RuntimeError(f"Noncanonical skin group on {item_id}.")
    return obj,projected


def sources(packs):
    roster=json.loads((ROOT/'art_source/blender/starter_npc_model_roster_v1.json').read_text())['models']
    result=[]
    for model in roster:
        result.append({'slug':model['slug'],'sex':model['sex'],'path':ROOT/f"game/assets/humanoids/human/npc/{model['slug']}.glb",
                       'body':'Genesis 8.1 Female Mesh' if model['slug']=='yuffie' else model['meshes'][0],
                       'armature':model['armature'],'original':model['source_file'],'normalized':True,'bone_map':model['bone_map'],
                       'flip':True})
    for slug_name,relative in [('bladedancer','RyanReos_BladedancerMercenary/Blender/BladedancerMercenary.blend'),
                               ('bunny_akali','RyanReos_BunnyAkali/Blender/BunnyAkali_ARP_Walk_Animation.blend'),
                               ('gris','RyanReos_GrisSwimsuit/Blender/GrisSwimsuit.blend')]:
        result.append({'slug':slug_name,'sex':'female','path':packs/relative,'body':None,'bone_map':{},'flip':False,
                       'normalized':slug_name!='bunny_akali'})
    result.append({'slug':'deluxe_bunny','sex':'female','path':ROOT/'art_source/reference/clothing/deluxe_bunny_suit/deluxe_bunny_suit_source_v1.blend','body':None,'bone_map':{},'flip':False,'normalized':True})
    for name,sex in [('radagon_male','male'),('elf_swordmaster','female')]:
        result.append({'slug':name,'sex':sex,'path':ROOT/f'art_source/reference/generated_humanoids/{name}_humanoid_rig_v1.blend',
                       'body':'HumanBody','bone_map':{},'flip':False,'normalized':True})
    for sex in ('male','female'):
        result.append({'slug':'legacy_'+sex,'sex':sex,'path':ROOT/f'game/assets/humanoids/human/player_{sex}_humanoid_rig_v1.glb',
                       'body':'HumanBody','bone_map':{name:name for name in ('pelvis','spine_02','spine_03','head','upperarm_l','upperarm_r','lowerarm_l','lowerarm_r','thigh_l','thigh_r','calf_l','calf_r','foot_l','foot_r')},'flip':False})
    return result


def build(packs,only,npc_sources):
    entries={item['id']:item for item in json.loads(MANIFEST.read_text())['items']} if only else {}
    report=json.loads(REPORT.read_text()) if only else []
    legacy={'HumanWorkShirt':'work_shirt','HumanWorkTrousers':'work_trousers',
            'HumanDeluxeBunnySuitJacket':'deluxe_bunny_suit_top','HumanDeluxeBunnySuitStockings':'deluxe_bunny_suit_legs'}
    for specification in sources(packs):
        if only and specification['slug'] not in only: continue
        sex=specification['sex']
        bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'art_source/characters/player_{sex}.blend'))
        target_body=bpy.data.objects['HumanBody']
        target_rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
        surface=target_surface(target_body,target_rig)
        before=set(bpy.context.scene.objects)
        path=specification['path']
        if path.suffix=='.glb':
            bpy.ops.import_scene.gltf(filepath=str(path))
            imported=[obj for obj in bpy.context.scene.objects if obj not in before]
        else:
            with bpy.data.libraries.load(str(path),link=False) as (_,loaded):
                loaded.objects=list(_.objects)
            imported=list(loaded.objects)
            for obj in imported:
                bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.update()
        for armature in (obj for obj in imported if obj.type=='ARMATURE'):
            armature.data.pose_position='REST'
        helpers={bone.custom_shape for obj in imported if obj.type=='ARMATURE'
                 for bone in obj.pose.bones if bone.custom_shape is not None}
        source_meshes=[obj for obj in imported if obj.type=='MESH' and len(obj.data.vertices)>10
                       and len(obj.data.polygons)>0 and obj not in helpers
                       and not obj.name.lower().startswith(('wgt-','wgt_'))
                       and 'proxymesh' not in obj.name.lower() and '|' not in obj.name]
        body=next((obj for obj in source_meshes if obj.name==specification['body'] or obj.name.startswith(str(specification['body'])+'.')),None)
        if body is None and specification['body'] is None:
            candidates=[obj for obj in source_meshes if any(word in obj.name.lower() for word in ('body','nude'))
                        and 'suit' not in obj.name.lower() and any(m.type=='ARMATURE' and m.object for m in obj.modifiers)]
            if candidates:
                body=min(candidates,key=lambda obj:len(obj.name))
        source_rig=next((m.object for m in body.modifiers if m.type=='ARMATURE'),None) if body else None
        if source_rig is None:
            source_rig=next((obj for obj in imported if obj.type=='ARMATURE' and obj.name==specification.get('armature')),
                            max((obj for obj in imported if obj.type=='ARMATURE'),key=lambda obj:len(obj.data.bones),default=None))
        if source_rig: source_rig.data.pose_position='REST'
        if specification.get('original'):
            body=original_body_reference(specification,npc_sources,source_rig)
        if path.suffix=='.blend' and body:
            # Packs contain backup meshes in centimetres on separate source
            # rigs. They are not variants of the active metre-scale body.
            source_meshes=[obj for obj in source_meshes if not any(m.type=='ARMATURE' and m.object
                and m.object!=source_rig for m in obj.modifiers)]
        unresolved=[]
        if path.suffix=='.blend':
            texture_root=path.parent.parent if path.is_relative_to(packs) else path.parent
            unresolved=materials._resolve_and_pack_textures(source_meshes,path.parent,
                materials._texture_index(texture_root),Path(tempfile.gettempdir())/'player_wardrobe_textures'/specification['slug'])
        mapper=source_mapper(source_rig,target_rig,body,target_body,specification['bone_map'],specification['flip'],
            normalized=specification.get('normalized',False),reference_meshes=source_meshes)
        images=list(bpy.data.images)
        for source in source_meshes:
            if source==body: continue
            slot=category(source.name)
            if not slot: continue
            legacy_source=specification['slug'].startswith('legacy_')
            if legacy_source and source.name not in legacy and source.name not in {'HumanHair'}: continue
            item_id=legacy.get(source.name) if legacy_source else None
            item_id=item_id or slug(specification['slug']+'_'+source.name)
            entry=entries.setdefault(item_id,{'id':item_id,'name':specification['slug'].replace('_',' ').title()+' / '+source.name,
                                             'slot':slot,'sexes':[],'coversGenitalia':slot=='legs' or any(word in source.name.lower() for word in ('dress','bodysuit','leotard','swimsuit')),
                                             'weightKg':0 if slot=='hair' else .5,'assets':{},'source':path.name,'sourceMesh':source.name})
            if sex.title() in entry['sexes'] and not only: continue
            entry['slot']=slot
            converted,projected=fit_piece(source,target_rig,surface,mapper,item_id)
            material_notes=portable_materials(converted,images,already_portable=path.suffix=='.glb',npc_sources=npc_sources)
            mask,mask_faces=clothing.write_body_mask(target_body,converted,item_id,sex,
                allow_cover=slot in {'torso','outerwear','legs','hands','feet'})
            entry.setdefault('bodyMasks',{})[sex.title()]=mask
            entry['drape']=clothing.drape_kind(source.name)
            dest=OUT/sex/(item_id+'.glb')
            dest.parent.mkdir(parents=True,exist_ok=True)
            bpy.ops.object.select_all(action='DESELECT')
            converted.select_set(True)
            target_rig.select_set(True)
            bpy.context.view_layer.objects.active=target_rig
            bpy.ops.export_scene.gltf(filepath=str(dest),export_format='GLB',use_selection=True,
                                     export_animations=False,export_skins=True,export_apply=True,export_yup=True)
            if sex.title() not in entry['sexes']: entry['sexes'].append(sex.title())
            entry['assets'][sex.title()]='res://'+str(dest.relative_to(ROOT/'game')).replace('\\','/')
            report[:]=[row for row in report if not (row['id']==item_id and row['sex']==sex)]
            report.append({'id':item_id,'sex':sex,'source':path.name,'mesh':source.name,
                           'normalizationReference':body.name if body else 'declared metre-scale source' if specification.get('normalized') else 'combined source garment bounds',
                           **({'sourceFrameErrorM':body['alignment_error_m']} if body and 'alignment_error_m' in body else {}),
                           'unresolvedSourceImages':unresolved,
                           'vertices':len(converted.data.vertices),'clearanceAdjustedVertices':projected,
                           'maskedBodyFaces':mask_faces,'drape':entry['drape'],
                           'materials':material_notes,'status':'converted'})
            bpy.data.objects.remove(converted,do_unlink=True)
            print('WARDROBE CONVERTED',sex,item_id)
    fitted_starter_garments(entries,report)
    write_catalog(entries)
    REPORT.write_text(json.dumps(report,indent=2)+'\n',encoding='utf8',newline='\n')
    print('PLAYER WARDROBE PASSED',len(entries),'items;',len(report),'body-specific assets')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--packs',type=Path,required=True)
    parser.add_argument('--npc-sources',type=Path)
    parser.add_argument('--only',nargs='*',default=[])
    parser.add_argument('--catalog-only',action='store_true')
    parser.add_argument('--starter-only',action='store_true')
    parser.add_argument('--masks-only',action='store_true')
    parser.add_argument('--resources-only',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.resources_only:
        write_resources({item['id']:item for item in json.loads(MANIFEST.read_text())['items']})
    elif args.masks_only:
        entries={item['id']:item for item in json.loads(MANIFEST.read_text())['items']}
        for item in entries.values():
            for sex_name,path in item['assets'].items():
                sex=sex_name.lower()
                bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'art_source/characters/player_{sex}.blend'))
                body=bpy.data.objects['HumanBody']
                rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
                target_surface(body,rig)
                before=set(bpy.context.scene.objects)
                bpy.ops.import_scene.gltf(filepath=str(ROOT/'game'/path.removeprefix('res://')))
                meshes=[obj for obj in bpy.context.scene.objects if obj not in before and obj.type=='MESH'
                        and obj.name=='Appearance_'+item['id']]
                if len(meshes)!=1: raise RuntimeError(f"Coverage rebuild expects one wearable mesh: {[(o.name,o.type) for o in bpy.context.scene.objects if o not in before]}.")
                mask,count=clothing.write_body_mask(body,meshes[0],item['id'],sex,
                    allow_cover=item['slot'] in {'torso','outerwear','legs','hands','feet'})
                item.setdefault('bodyMasks',{})[sex_name]=mask
                print('BODY COVERAGE PASSED',item['id'],sex,count)
        write_catalog(entries)
    elif args.starter_only:
        entries={item['id']:item for item in json.loads(MANIFEST.read_text())['items']}
        report=json.loads(REPORT.read_text())
        fitted_starter_garments(entries,report)
        write_catalog(entries)
        REPORT.write_text(json.dumps(report,indent=2)+'\n',encoding='utf8',newline='\n')
    elif args.catalog_only:
        entries={item['id']:item for item in json.loads(MANIFEST.read_text())['items']}
        for item in list(entries.values()):
            valid=True
            for path in item['assets'].values():
                binary=(ROOT/'game'/path.removeprefix('res://')).read_bytes()
                size=struct.unpack_from('<I',binary,12)[0]
                valid=valid and bool(json.loads(binary[20:20+size]).get('skins'))
            if not valid:
                del entries[item['id']]
                print('EXCLUDED RIG HELPER',item['id'])
                continue
            if item['id'] not in {'work_shirt','work_trousers','deluxe_bunny_suit_top','deluxe_bunny_suit_legs'}:
                item['slot']=category(item['sourceMesh'])
            item['coversGenitalia']=item['slot']=='legs' or any(word in item['sourceMesh'].lower() for word in ('dress','bodysuit','leotard','swimsuit'))
        write_catalog(entries)
        report=[row for row in json.loads(REPORT.read_text()) if row['id'] in entries]
        REPORT.write_text(json.dumps(report,indent=2)+'\n',encoding='utf8',newline='\n')
    else:
        build(args.packs,args.only,args.npc_sources)
