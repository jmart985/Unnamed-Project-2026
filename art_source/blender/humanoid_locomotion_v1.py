"""Author in-place NPC humanoid locomotion actions for Humanoid Rig v1."""
from __future__ import annotations

import math

import bpy
from mathutils import Quaternion, Vector


REQUIRED_LOCOMOTION_BONES = {
    "pelvis", "spine_02", "spine_03", "head", "upperarm_l", "lowerarm_l", "upperarm_r",
    "lowerarm_r", "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r",
}
ACTION_NAMES = ("human_idle", "human_walk", "human_run")
AxisRotation = tuple[tuple[float, float, float], float]
BonePose = tuple[AxisRotation, ...]
Pose = dict[str, BonePose]


def _local_axis_for_world(bone: bpy.types.Bone, axis: tuple[float, float, float]) -> Vector:
    """Convert an armature-space axis into a bone's local rotation coordinates."""
    return (bone.matrix_local.to_3x3().inverted() @ Vector(axis)).normalized()


def _additional_arm_drop(armature: bpy.types.Object, side: str) -> float:
    """Relax an A-pose down, while respecting rigs whose arms already hang."""
    bone = armature.data.bones[f"upperarm_{side}"]
    direction = (bone.tail_local - bone.head_local).normalized()
    horizontal = math.hypot(direction.x, direction.y)
    source_drop = math.atan2(abs(direction.z), horizontal)
    return max(0.0, 1.08 - source_drop)


def _pose_rotations(armature: bpy.types.Object, values: Pose) -> None:
    for name in REQUIRED_LOCOMOTION_BONES:
        if name not in armature.pose.bones:
            continue
        pose_bone = armature.pose.bones[name]
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        for world_axis, angle in values.get(name, ()):
            local_axis = _local_axis_for_world(armature.data.bones[name], world_axis)
            pose_bone.rotation_quaternion @= Quaternion(local_axis, angle)


def _key_pose(armature: bpy.types.Object, action: bpy.types.Action,
              frames: list[tuple[int, Pose]]) -> None:
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, values in frames:
        bpy.context.scene.frame_set(frame)
        _pose_rotations(armature, values)
        for name in REQUIRED_LOCOMOTION_BONES:
            if name in armature.pose.bones:
                armature.pose.bones[name].keyframe_insert(
                    data_path="rotation_quaternion", frame=frame, group=name)
    action.use_fake_user = True
    action.use_cyclic = True


def _phase_pose(phase: float, stride: float, knee_bend: float, arm_swing: float,
                breath: float = 0.0, arm_hang: float = 1.05,
                elbow_flex: float = 0.12) -> Pose:
    left_leg = stride * math.sin(phase)
    right_leg = -left_leg
    left_knee = knee_bend * max(0.0, -math.sin(phase))
    right_knee = knee_bend * max(0.0, math.sin(phase))
    left_arm = arm_swing * math.sin(phase)
    right_arm = -left_arm
    sagittal = (1.0, 0.0, 0.0)
    vertical = (0.0, 0.0, 1.0)
    forward = (0.0, 1.0, 0.0)
    return {
        "pelvis": ((vertical, 0.018 * math.sin(phase)),),
        "spine_02": ((sagittal, breath + 0.012 * math.sin(phase)),),
        "spine_03": ((sagittal, breath * 0.65),),
        "thigh_l": ((sagittal, left_leg),),
        "thigh_r": ((sagittal, right_leg),),
        "calf_l": ((sagittal, left_knee),),
        "calf_r": ((sagittal, right_knee),),
        "foot_l": ((sagittal, -left_leg * 0.42 - left_knee * 0.35),),
        "foot_r": ((sagittal, -right_leg * 0.42 - right_knee * 0.35),),
        "upperarm_l": ((forward, arm_hang), (sagittal, left_arm)),
        "upperarm_r": ((forward, -arm_hang), (sagittal, right_arm)),
        "lowerarm_l": ((sagittal, elbow_flex + max(0.0, left_arm) * 0.2),),
        "lowerarm_r": ((sagittal, -elbow_flex + max(0.0, right_arm) * 0.2),),
    }


def add_humanoid_locomotion_actions(armature: bpy.types.Object) -> list[str]:
    """Create looping, root-motion-free human idle, walk, and run actions."""
    if armature.type != "ARMATURE":
        raise TypeError("Humanoid locomotion actions require an armature object.")
    missing = REQUIRED_LOCOMOTION_BONES - set(armature.data.bones.keys())
    if missing:
        raise RuntimeError(f"Humanoid locomotion rig is missing bones: {sorted(missing)}")

    for name in ACTION_NAMES:
        previous = bpy.data.actions.get(name)
        if previous is not None:
            bpy.data.actions.remove(previous)

    # A two-second breathing loop keeps the torso alive while the feet remain planted.
    idle = bpy.data.actions.new("human_idle")
    left_arm_drop = _additional_arm_drop(armature, "l")
    right_arm_drop = _additional_arm_drop(armature, "r")
    arm_swing_factor = 0.55 + 0.45 * min(1.0, left_arm_drop / 1.08)
    idle_frames = []
    for frame in (1, 13, 25, 37, 49):
        phase = 2.0 * math.pi * (frame - 1) / 48.0
        idle_frames.append((frame, {
            "spine_02": (((1.0, 0.0, 0.0), 0.012 * math.sin(phase)),),
            "spine_03": (((1.0, 0.0, 0.0), 0.02 * math.sin(phase)),),
            "head": (((1.0, 0.0, 0.0), -0.006 * math.sin(phase)),),
            "upperarm_l": (((0.0, 1.0, 0.0), left_arm_drop),),
            "upperarm_r": (((0.0, 1.0, 0.0), -right_arm_drop),),
            "lowerarm_l": (((1.0, 0.0, 0.0), 0.12),),
            "lowerarm_r": (((1.0, 0.0, 0.0), -0.12),),
        }))
    _key_pose(armature, idle, idle_frames)

    walk = bpy.data.actions.new("human_walk")
    walk_frames = []
    for frame in (1, 4, 7, 10, 13, 16, 19, 22, 25):
        phase = 2.0 * math.pi * (frame - 1) / 24.0
        walk_frames.append((frame, _phase_pose(phase, stride=0.34, knee_bend=0.38,
                                               arm_swing=0.30 * arm_swing_factor,
                                               arm_hang=left_arm_drop)))
    _key_pose(armature, walk, walk_frames)

    run = bpy.data.actions.new("human_run")
    run_frames = []
    for frame in (1, 3, 5, 7, 9, 11, 13, 15, 17):
        phase = 2.0 * math.pi * (frame - 1) / 16.0
        pose = _phase_pose(phase, stride=0.62, knee_bend=0.72,
                           arm_swing=0.50 * arm_swing_factor, breath=0.018,
                           arm_hang=left_arm_drop * 0.85, elbow_flex=0.30)
        pose["spine_02"] = (((1.0, 0.0, 0.0), -0.045 + pose["spine_02"][0][1]),)
        run_frames.append((frame, pose))
    _key_pose(armature, run, run_frames)

    # Preserve the first-pose rest transform in the scene and make both loops seamless.
    bpy.context.scene.frame_set(1)
    armature.animation_data.action = idle
    return list(ACTION_NAMES)
