"""Audit exported skins and verify loose hems avoid locomotion leg weights."""
import json
import sys
from pathlib import Path
import numpy as np

sys.path.insert(0,str(Path(__file__).parent))
from retarget_player_expressions import load_glb
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
manifest=json.loads((ROOT/"game/data/player_appearance.json").read_text())
domain=json.loads((ROOT/'data/player_appearance.json').read_text())
if {entry['id'] for entry in domain if not entry['id'].startswith('custom_')}!={entry['id'] for entry in manifest['items']}:
    raise RuntimeError('The runtime catalog retains stale generated entries outside the conversion manifest.')
rows=[]
for item in manifest["items"]:
    for sex,path in item["assets"].items():
        doc,read,world=load_glb(ROOT/"game"/path.removeprefix("res://"))
        if len(doc.get("skins",[]))!=1: raise RuntimeError(f"{item['id']} lacks one valid skin.")
        skin=doc["skins"][0]
        meshes=[node for node in doc['nodes'] if 'mesh' in node]
        if len(meshes)!=1 or meshes[0]['name']!='Appearance_'+item['id']:
            raise RuntimeError(f"{item['id']} includes unintended source meshes or rig widgets.")
        names=[doc["nodes"][index]["name"] for index in skin["joints"]]
        if "neutral_bone" in names: raise RuntimeError(f"{item['id']} has stale/unbound skin weights.")
        vertices=hem_vertices=0;worst=0.;thickness=[]
        for i,node in enumerate(doc["nodes"]):
            if "mesh" not in node: continue
            for primitive in doc["meshes"][node["mesh"]]["primitives"]:
                for attribute,accessor in primitive['attributes'].items():
                    if not np.isfinite(read(accessor)).all():
                        raise RuntimeError(f"{item['id']} has nonfinite {attribute} data.")
                positions=read(primitive["attributes"]["POSITION"])
                placed=np.array([world(i)@Vector(position) for position in positions])
                if (np.abs(placed[:,0]).max()>1.1 or np.abs(placed[:,2]).max()>1.1
                    or placed[:,1].min()<-.2 or placed[:,1].max()>2.3):
                    raise RuntimeError(f"{item['id']} is outside the normalized player wardrobe envelope: {placed.min(axis=0)} .. {placed.max(axis=0)}.")
                joints=read(primitive["attributes"]["JOINTS_0"]).astype(int)
                weights=read(primitive["attributes"]["WEIGHTS_0"])
                if item['id'] in {'work_shirt','work_trousers','deluxe_bunny_suit_top','deluxe_bunny_suit_legs'}:
                    points=[world(i)@Vector(position) for position in positions]
                    faces=read(primitive['indices']).reshape(-1,3).astype(int).tolist()
                    tree=BVHTree.FromPolygons(points,faces,all_triangles=True)
                    for a,b,c in faces[::max(1,len(faces)//100)]:
                        normal=(points[b]-points[a]).cross(points[c]-points[a]).normalized()
                        center=(points[a]+points[b]+points[c])/3
                        hit=tree.ray_cast(center-normal*.00001,-normal,.01)
                        if hit[0] is not None and hit[1].dot(normal)<-.5: thickness.append(hit[3])
                worst=max(worst,float(np.abs(weights.sum(axis=1)-1).max()))
                if "JOINTS_1" in primitive["attributes"]: raise RuntimeError("More than four exported weights.")
                if item.get("drape")=="skirt":
                    for position,indices,values in zip(positions,joints,weights):
                        if (world(i)@Vector(position)).y>=.74: continue
                        hem_vertices+=1
                        for index,weight in zip(indices,values):
                            if weight>.001 and names[index].startswith(("thigh_","calf_")):
                                raise RuntimeError(f"{item['id']} binds its free hem to a leg.")
                vertices+=len(positions)
        if worst>.001: raise RuntimeError(f"{item['id']} has unnormalized skinning.")
        if thickness and not .001<float(np.median(thickness))<.006:
            raise RuntimeError(f"{item['id']} lost its exported physical fabric thickness.")
        if item['id'] in {'work_shirt','work_trousers','deluxe_bunny_suit_top','deluxe_bunny_suit_legs'} and len(thickness)<10:
            raise RuntimeError(f"{item['id']} lacks enough paired shell surfaces for a thickness check.")
        if sex not in item.get("bodyMasks",{}): raise RuntimeError(f"{item['id']} lacks coverage.")
        rows.append({"id":item["id"],"sex":sex,"vertices":vertices,"joints":len(names),
                     "maximumWeightSumError":worst,"freeHemVertices":hem_vertices,
                     **({'medianFabricThicknessM':float(np.median(thickness))} if thickness else {}),"status":"PASS"})
(ROOT/"art_source/characters/wardrobe_validation.json").write_text(
    json.dumps(rows,indent=2)+"\n",encoding="utf8",newline="\n")
print("EXPORTED PLAYER WARDROBE VALIDATION PASSED",len(rows),"assets; normalized skins; cloth hems independent of legs.")
