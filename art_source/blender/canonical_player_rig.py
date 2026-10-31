"""Fit Humanoid Rig v1 to Blender's untouched realistic base meshes.

The sculpt face sets belong to the preserved Blender source, not gameplay
anatomy IDs. Their shared boundary loops locate the actual source joints.
Only the armature changes here; the normalized body vertices never move.
"""
from __future__ import annotations

import bpy
from mathutils import Vector


SOURCE_BODY_SETS = {
    "l": {"upperarm": 21, "lowerarm": 12, "hand": 9,
          "thigh": 24, "calf": 15, "foot": 14,
          "fingers": {"thumb": 80, "index": 76, "middle": 72, "ring": 68, "pinky": 64}},
    "r": {"upperarm": 20, "lowerarm": 11, "hand": 10,
          "thigh": 23, "calf": 16, "foot": 13,
          "fingers": {"thumb": 84, "index": 88, "middle": 92, "ring": 96, "pinky": 100}},
}


def fit_rig_to_base(body, armature):
    points = [body.matrix_world @ vertex.co for vertex in body.data.vertices]
    face_sets = body.data.attributes.get(".sculpt_face_set")
    if face_sets is None or face_sets.domain != "FACE":
        raise RuntimeError("Blender body source is missing its anatomical sculpt face sets.")
    regions = {}
    for face, value in zip(body.data.polygons, face_sets.data):
        regions.setdefault(value.value, set()).update(face.vertices)

    def center(indices):
        if not indices:
            raise RuntimeError("The Blender body source has an empty joint boundary.")
        return sum((points[i] for i in indices), Vector()) / len(indices)

    def joint(first, second):
        return center(regions[first] & regions[second])

    def tip(region, start, direction):
        direction = direction.normalized()
        furthest = max((points[i] - start).dot(direction) for i in regions[region])
        return center({i for i in regions[region]
                       if (points[i] - start).dot(direction) >= furthest - 0.003})

    # Reset all template animation before editing the rest skeleton. Retain its
    # semantic names and parent hierarchy so gameplay and sockets stay stable.
    armature.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for bone in armature.pose.bones:
        bone.matrix_basis.identity()
        for constraint in list(bone.constraints):
            bone.constraints.remove(constraint)

    waist = joint(18, 19)
    abdomen = joint(19, 1)
    neck = joint(1, 17)
    chest = abdomen.lerp(neck, 0.5)
    hip_center = (joint(18, 24) + joint(18, 23)) / 2
    shoulder_center = (joint(1, 21) + joint(1, 20)) / 2
    head_base = neck + Vector((0, -0.025, 0.065))
    head_top = Vector((head_base.x, head_base.y, max(p.z for p in points) - 0.015))
    positions = {
        "root": (Vector(), Vector((0, 0, 0.12))),
        "pelvis": (hip_center, waist),
        "spine_01": (waist, abdomen),
        "spine_02": (abdomen, chest),
        "spine_03": (chest, neck),
        "neck_01": (neck, head_base),
        "head": (head_base, head_top),
        "head_end": (head_top, head_top + Vector((0, 0, 0.02))),
    }
    for side, source in SOURCE_BODY_SETS.items():
        sign = 1 if side == "l" else -1
        shoulder = joint(1, source["upperarm"])
        elbow = joint(source["upperarm"], source["lowerarm"])
        wrist = joint(source["lowerarm"], source["hand"])
        palm = joint(source["hand"], source["fingers"]["middle"])
        hip = joint(18, source["thigh"])
        knee = joint(source["thigh"], source["calf"])
        ankle = joint(source["calf"], source["foot"])
        toe_base = Vector((ankle.x + sign * 0.045, -0.035, 0.035))
        toe_tip = Vector((ankle.x + sign * 0.055, -0.098, 0.025))
        positions.update({
            f"clavicle_{side}": (shoulder_center + Vector((sign * 0.035, 0, 0)), shoulder),
            f"upperarm_{side}": (shoulder, elbow),
            f"lowerarm_{side}": (elbow, wrist),
            f"hand_{side}": (wrist, palm),
            f"thigh_{side}": (hip, knee),
            f"calf_{side}": (knee, ankle),
            f"foot_{side}": (ankle, toe_base),
            f"toe_{side}": (toe_base, toe_tip),
            f"ball_{side}_end": (toe_tip, toe_tip + Vector((0, -0.01, 0))),
        })
        for finger, first in source["fingers"].items():
            base = joint(source["hand"], first)
            middle = joint(first, first + 1)
            distal = joint(first + 1, first + 2)
            end = tip(first + 2, distal, distal - middle)
            for number, start, finish in ((1, base, middle), (2, middle, distal), (3, distal, end)):
                positions[f"{finger}_{number:02}_{side}"] = (start, finish)
            positions[f"{finger}_03_{side}_end"] = (end, end + (end - distal).normalized() * 0.005)

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    inverse = armature.matrix_world.inverted()
    missing = set(armature.data.edit_bones.keys()) - set(positions)
    if missing:
        raise RuntimeError(f"The base fitter does not cover canonical bones: {sorted(missing)}")
    for bone in armature.data.edit_bones:
        bone.use_connect = False
    for name, (start, end) in positions.items():
        bone = armature.data.edit_bones[name]
        bone.head = inverse @ start
        bone.tail = inverse @ end
        bone.align_roll(Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    body["source_shape_preserved"] = True
    bpy.context.scene["base_rig_fit"] = "source face-set boundary loops; uniform body normalization only"
    print(f"Fitted {len(positions)} canonical bones to the untouched Blender body.")
