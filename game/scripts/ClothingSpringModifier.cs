using Godot;

namespace UnnamedProject2026;

/// <summary>Bounded local-space cloth springs after animation; never owns actor physics.</summary>
public partial class ClothingSpringModifier : SkeletonModifier3D
{
    private sealed class Joint(int bone, string name)
    {
        public int Bone { get; } = bone;
        public string Name { get; } = name;
        public Vector2 Angle;
        public Vector2 Velocity;
    }
    private readonly List<Joint> _joints = [];
    private HashSet<string> _kinds = new(StringComparer.Ordinal);
    private bool _initialized;
    private Vector3 _lastOrigin;
    private Vector3 _lastVelocity;
    private float _lastYaw;
    internal float MaximumOffsetRadians { get; private set; }
    internal int ModifiedBoneCount { get; private set; }
    internal const float AngleLimit = .5f;

    internal void SetKinds(IEnumerable<string> kinds)
    {
        var requested = kinds.ToHashSet(StringComparer.Ordinal);
        if (_kinds.SetEquals(requested)) return;
        _kinds = requested;
        _joints.Clear();
        var skeleton = GetSkeleton();
        if (skeleton is not null)
            for (var bone = 0; bone < skeleton.GetBoneCount(); bone++)
            {
                var name = skeleton.GetBoneName(bone);
                if ((_kinds.Contains("skirt") && name.StartsWith("cloth_skirt_", StringComparison.Ordinal))
                    || (_kinds.Contains("cape") && name.StartsWith("cloth_cape_", StringComparison.Ordinal)))
                    _joints.Add(new(bone, name));
            }
        _initialized = false;
        MaximumOffsetRadians = 0;
        ModifiedBoneCount = 0;
        Active = _joints.Count > 0;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (skeleton is null || delta <= 0 || _joints.Count == 0) return;
        var dt = (float)Math.Min(delta, 1.0 / 30);
        var origin = skeleton.GlobalPosition;
        var basis = skeleton.GlobalBasis.Orthonormalized();
        var yaw = basis.GetEuler().Y;
        if (!_initialized || origin.DistanceTo(_lastOrigin) > 3 || delta > .25)
        {
            _initialized = true;
            _lastOrigin = origin; _lastVelocity = Vector3.Zero; _lastYaw = yaw;
            foreach (var joint in _joints) { joint.Angle = Vector2.Zero; joint.Velocity = Vector2.Zero; }
            MaximumOffsetRadians = 0;
            return;
        }
        var velocity = (origin - _lastOrigin) / dt;
        var acceleration = basis.Inverse() * ((velocity - _lastVelocity) / dt).LimitLength(25);
        var turn = Mathf.Clamp(Mathf.AngleDifference(_lastYaw, yaw) / dt, -5, 5);
        _lastOrigin = origin; _lastVelocity = velocity; _lastYaw = yaw;
        var target = new Vector2(acceleration.Z * .012f, acceleration.X * .012f + turn * .025f).LimitLength(.3f);
        MaximumOffsetRadians = 0;
        ModifiedBoneCount = 0;
        foreach (var joint in _joints)
        {
            // Substeps keep the damped angular spring stable across frame rates.
            var steps = Math.Max(1, (int)Math.Ceiling(dt * 120));
            var step = dt / steps;
            for (var i = 0; i < steps; i++)
            {
                joint.Velocity += ((target - joint.Angle) * 55 - joint.Velocity * 12) * step;
                joint.Angle = (joint.Angle + joint.Velocity * step).LimitLength(AngleLimit);
            }
            var pose = skeleton.GetBoneGlobalPose(joint.Bone);
            var rotation = new Quaternion(Vector3.Right, joint.Angle.X)
                * new Quaternion(Vector3.Forward, joint.Angle.Y);
            pose.Basis = new Basis(rotation) * pose.Basis;
            // These capsules are evaluated in the same unscaled skeleton space
            // as the spring, so height/build presentation scale remains safe.
            var direction = pose.Basis.Y.Normalized();
            var children = skeleton.GetBoneChildren(joint.Bone);
            var length = children.Length > 0 ? skeleton.GetBoneRest(children[0]).Origin.Length() : .4f;
            var tip = pose.Origin + direction * length;
            foreach (var side in new[] { "l", "r" })
            {
                var hip = skeleton.FindBone("thigh_" + side);
                var knee = skeleton.FindBone("calf_" + side);
                var ankle = skeleton.FindBone("foot_" + side);
                if (hip < 0 || knee < 0 || ankle < 0) continue;
                tip = ClearSegment(tip, skeleton.GetBoneGlobalPose(hip).Origin,
                    skeleton.GetBoneGlobalPose(knee).Origin, .085f, joint.Name);
                tip = ClearSegment(tip, skeleton.GetBoneGlobalPose(knee).Origin,
                    skeleton.GetBoneGlobalPose(ankle).Origin, .065f, joint.Name);
            }
            if (length > .001f && tip.DistanceSquaredTo(pose.Origin) > .000001f)
            {
                var desired = (tip - pose.Origin).Normalized();
                var clearance = new Quaternion(direction, desired);
                if (clearance.GetAngle() > .3f)
                    clearance = Quaternion.Identity.Slerp(clearance, .3f / clearance.GetAngle());
                pose.Basis = new Basis(clearance) * pose.Basis;
            }
            skeleton.SetBoneGlobalPose(joint.Bone, pose);
            MaximumOffsetRadians = Math.Max(MaximumOffsetRadians, joint.Angle.Length());
            ModifiedBoneCount++;
        }
    }

    private static Vector3 ClearSegment(Vector3 point, Vector3 start, Vector3 end, float radius, string name)
    {
        var segment = end - start;
        var fraction = segment.LengthSquared() > .000001f
            ? Mathf.Clamp((point - start).Dot(segment) / segment.LengthSquared(), 0, 1) : 0;
        var closest = start + segment * fraction;
        var offset = point - closest;
        if (offset.LengthSquared() >= radius * radius) return point;
        var fallback = name.Contains("front", StringComparison.Ordinal) ? Vector3.Back : Vector3.Forward;
        return closest + (offset.LengthSquared() > .000001f ? offset.Normalized() : fallback) * radius;
    }
}
