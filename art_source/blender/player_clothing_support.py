"""Reversible body coverage and shared secondary garment deformation."""
import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT=Path(__file__).resolve().parents[2]


def add_cloth_bones(rig):
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode="EDIT")
    for side,x in (("l",.12),("r",-.12)):
        for panel,y in (("front",-.1),("back",.1)):
            parent=rig.data.edit_bones["pelvis"]
            points=[Vector((x,y,.98)),Vector((x*1.2,y*1.3,.6)),Vector((x*1.3,y*1.5,.18))]
            for i in range(2):
                bone=rig.data.edit_bones.new(f"cloth_skirt_{panel}_{i+1:02}_{side}")
                bone.head,bone.tail=points[i:i+2];bone.parent=parent
                bone.align_roll(Vector((1,0,0)));parent=bone
    parent=rig.data.edit_bones["spine_03"]
    points=[Vector((0,.09,1.4)),Vector((0,.14,1.05)),Vector((0,.22,.7)),Vector((0,.22,.3))]
    for i in range(3):
        bone=rig.data.edit_bones.new(f"cloth_cape_{i+1:02}")
        bone.head,bone.tail=points[i:i+2];bone.parent=parent
        bone.align_roll(Vector((1,0,0)));parent=bone
    bpy.ops.object.mode_set(mode="OBJECT")


def drape_kind(name):
    name=name.lower()
    if "cape" in name: return "cape"
    if "cloth_front" in name or "cloth_back" in name: return "skirt"
    if any(word in name for word in ("bracelet","necklace","jacket","boot","shoe","glove",
        "handband","arm","pauldron","earring","heel","hair","belt")): return None
    if any(word in name for word in ("dress","skirt","coat","cloth_front","cloth_back")): return "skirt"
    return None


def drape_weights(point,kind,original):
    if kind=="cape" and point.z<1.35:
        blend=max(0,min(1,(point.z-1.1)/.25))
        position=max(0,min(1.999,(1.35-point.z)/.35))
        first=int(position)+1;amount=position-int(position)
        return {"spine_03":blend,f"cloth_cape_{first:02}":(1-blend)*(1-amount),
                f"cloth_cape_{min(3,first+1):02}":(1-blend)*amount}
    if kind!="skirt" or point.z>=.99: return original
    waist=max(0,min(1,(point.z-.74)/.25))
    left=max(0,min(1,(point.x+.16)/.32))
    front=max(0,min(1,(-point.y+.1)/.2))
    panels=sorted([("front","l",front*left),("front","r",front*(1-left)),
                   ("back","l",(1-front)*left),("back","r",(1-front)*(1-left))],
                  key=lambda p:p[2],reverse=True)[:2]
    total=sum(p[2] for p in panels)
    lower=max(0,min(1,(.8-point.z)/.5))
    result={"pelvis":waist}
    for panel,side,weight in panels:
        for segment,amount in ((1,1-lower),(2,lower)):
            result[f"cloth_skirt_{panel}_{segment:02}_{side}"]=(1-waist)*weight/total*amount
    return result


def add_cloth_actions(rig):
    for name in ("human_idle","human_walk","human_run"):
        action=bpy.data.actions[name];rig.animation_data.action=action
        start,end=map(int,action.frame_range)
        for frame in range(start,end+1,3):
            phase=(frame-start)/max(1,end-start)*2*math.pi
            for bone in rig.pose.bones:
                if not bone.name.startswith("cloth_"): continue
                sign=-1 if bone.name.endswith("_r") else 1
                angle=(.025 if name=="human_idle" else .1 if name=="human_walk" else .18)*math.sin(phase)*sign
                axis=bone.bone.matrix_local.to_3x3().inverted()@Vector((1,0,0))
                bone.rotation_mode="QUATERNION";bone.rotation_quaternion=Quaternion(axis,angle)
                bone.keyframe_insert("rotation_quaternion",frame=frame)
    for bone in rig.pose.bones:
        if bone.name.startswith("cloth_"): bone.rotation_quaternion=Quaternion()
    rig.animation_data.action=bpy.data.actions["human_idle"]
    bpy.context.scene.frame_set(1)


def coverage(body,garment,raw_skin=False):
    """Mask opaque garment coverage, including loose fabric above the skin."""
    graph=bpy.context.evaluated_depsgraph_get()
    skin=body.data.copy() if raw_skin else bpy.data.meshes.new_from_object(
        body.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
    cloth=bpy.data.meshes.new_from_object(garment.evaluated_get(graph),depsgraph=graph)
    cloth.calc_loop_triangles()
    points=[garment.matrix_world@v.co for v in cloth.vertices]
    tree=BVHTree.FromPolygons(points,[tuple(t.vertices) for t in cloth.loop_triangles],all_triangles=True)
    alpha={}
    for material in cloth.materials:
        if material is None or not material.use_nodes: continue
        shader=next((n for n in material.node_tree.nodes if n.type=="BSDF_PRINCIPLED"),None)
        if shader is None: continue
        opacity=shader.inputs["Alpha"]
        image=next((link.from_node.image for link in opacity.links
                    if link.from_node.type=="TEX_IMAGE"),None)
        if image is not None and image.channels==4:
            values=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(values)
            alpha[material.name]=(values.reshape(image.size[1],image.size[0],4)[:,:,3],image.size[:])
        elif not opacity.is_linked and opacity.default_value<.99:
            alpha[material.name]=(None,None)
    covered=[]
    for vertex in skin.vertices:
        point=body.matrix_world@vertex.co
        normal=(body.matrix_world.to_3x3().inverted().transposed()@vertex.normal).normalized()
        hit=tree.ray_cast(point-normal*.004,normal,.15)
        inside=hit[0] is not None
        if not inside:
            hit=tree.find_nearest(point)
            # Retain a small overlap allowance for animated/skinned fabric and
            # face chords crossing a convex breast, shoulder or knee.
            inside=hit[0] is not None and hit[3]<.04 and abs(normal.dot(hit[1]))>.3
        if inside:
            triangle=cloth.loop_triangles[hit[2]]
            material=cloth.materials[cloth.polygons[triangle.polygon_index].material_index] if cloth.materials else None
            entry=alpha.get(material.name) if material else None
            if entry is not None:
                pixels,dimensions=entry
                if pixels is None: inside=False
                elif cloth.uv_layers.active is not None:
                    a,b,c=[points[i] for i in triangle.vertices]
                    bary=barycentric_transform(hit[0],a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
                    uv=sum((cloth.uv_layers.active.data[i].uv*w for i,w in zip(triangle.loops,bary)),Vector((0,0)))
                    if not all(math.isfinite(value) for value in uv):
                        inside=False
                    else:
                        x=min(dimensions[0]-1,int((uv.x%1)*dimensions[0]))
                        y=min(dimensions[1]-1,int((uv.y%1)*dimensions[1]))
                        inside=pixels[y,x]>.98
        covered.append(inside)
    return skin,cloth,tree,covered


def save_mask(pixels,item_id,sex):
    size=pixels.shape[0]
    rgba=np.ones((size,size,4),dtype=np.float32);rgba[:,:,:3]=pixels[:,:,None]
    image=bpy.data.images.new("Coverage "+item_id,width=size,height=size,alpha=False)
    image.pixels.foreach_set(rgba.ravel())
    destination=ROOT/f"game/assets/appearance/{sex}/masks/{item_id}.png"
    destination.parent.mkdir(parents=True,exist_ok=True)
    image.filepath_raw=str(destination);image.file_format="PNG";image.save()
    bpy.data.images.remove(image)
    return "res://"+destination.relative_to(ROOT/"game").as_posix()


def write_body_mask(body,garment,item_id,sex,allow_cover=True):
    if not allow_cover: return save_mask(np.zeros((512,512),dtype=np.float32),item_id,sex),0
    skin,cloth,_,covered=coverage(body,garment)
    skin.calc_loop_triangles()
    uv=skin.uv_layers.active
    if uv is None: raise RuntimeError("Body masking requires the canonical body's UV map.")
    size=512;pixels=np.zeros((size,size),dtype=np.float32)
    faces=0
    for triangle in skin.loop_triangles:
        if not allow_cover or not all(covered[i] for i in triangle.vertices): continue
        faces+=1
        coords=np.array([uv.data[i].uv[:] for i in triangle.loops])*size
        lo=np.maximum(0,np.floor(coords.min(axis=0)).astype(int))
        hi=np.minimum(size-1,np.ceil(coords.max(axis=0)).astype(int))
        if np.any(hi<lo): continue
        x,y=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        a,b,c=coords
        det=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(det)<1e-8: continue
        u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/det
        v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/det
        inside=(u>=0)&(v>=0)&(u+v<=1)
        pixels[lo[1]:hi[1]+1,lo[0]:hi[0]+1][inside]=1
    # Keep a one-texel safety border under garment openings.
    eroded=pixels.copy()
    eroded[1:-1,1:-1]=np.minimum.reduce([pixels[1:-1,1:-1],pixels[:-2,1:-1],
        pixels[2:,1:-1],pixels[1:-1,:-2],pixels[1:-1,2:]])
    path=save_mask(eroded,item_id,sex)
    bpy.data.meshes.remove(skin);bpy.data.meshes.remove(cloth)
    return path,faces


def native_mask(body,garment):
    skin,cloth,_,covered=coverage(body,garment,raw_skin=True)
    group=body.vertex_groups.new(name="Under_"+garment.name)
    vertices=[index for index,hidden in enumerate(covered) if hidden]
    group.add(vertices,1,"REPLACE")
    modifier=body.modifiers.new("Under "+garment.name,"MASK")
    modifier.vertex_group=group.name;modifier.invert_vertex_group=True
    for property_name,visibility in (("show_viewport","hide_viewport"),("show_render","hide_render")):
        driver=modifier.driver_add(property_name).driver
        driver.expression="not hidden"
        variable=driver.variables.new();variable.name="hidden";variable.type="SINGLE_PROP"
        variable.targets[0].id=garment;variable.targets[0].data_path=visibility
    bpy.data.meshes.remove(skin);bpy.data.meshes.remove(cloth)
    return len(vertices)
