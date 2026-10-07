using Godot;

namespace UnnamedProject2026;

/// <summary>
/// Presentation-only procedural locomotion for jointed primitive rigs. It reads movement speed and rotates
/// limb pivots (walk cycle, knee/elbow bend, tail and head sway) plus idle breathing; it owns no simulation state.
/// </summary>
public sealed class ProceduralGait
{
    public sealed class Limb
    {
        public required Node3D Pivot { get; init; }
        public Vector3 Axis { get; init; } = Vector3.Right;
        public float Phase { get; init; }
        public float Amplitude { get; init; }
        public float Bias { get; init; }
        public bool Rectify { get; init; }
        public float IdleAmplitude { get; init; }
        public float IdleFrequency { get; init; } = 1.5f;
        public int FastGroup { get; init; }
    }

    private float _phase;
    private float _time;
    private float _intensity;

    public List<Limb> Limbs { get; } = [];
    public Node3D? Body { get; set; }
    public float ReferenceSpeed { get; set; } = 3.0f;
    public float RadiansPerMeter { get; set; } = 4.2f;
    public float BobHeight { get; set; } = 0.04f;
    public float BreathScale { get; set; } = 0.012f;

    public float Intensity => _intensity;

    public void Advance(float delta, float speed)
    {
        if (delta <= 0 || !float.IsFinite(speed)) return;
        speed = Mathf.Max(0, speed);
        var target = Mathf.Clamp(speed / ReferenceSpeed, 0, 1.5f);
        _intensity = Mathf.Lerp(_intensity, target, 1 - Mathf.Exp(-10 * delta));
        _phase = Mathf.Wrap(_phase + delta * Mathf.Min(speed, ReferenceSpeed * 1.5f) * RadiansPerMeter, 0, Mathf.Tau);
        _time += delta;

        var moving = Mathf.Min(_intensity, 1);
        var idle = 1 - moving;
        foreach (var limb in Limbs)
        {
            var fastGait = target > 0.78f;
            var phase = fastGait && limb.FastGroup > 0
                ? (limb.FastGroup == 1 ? 0f : Mathf.Pi)
                : limb.Phase;
            var wave = Mathf.Sin(_phase + phase);
            if (limb.Rectify) wave = Mathf.Max(0, wave);
            var angle = limb.Bias * moving + wave * limb.Amplitude * _intensity;
            angle += Mathf.Sin(_time * limb.IdleFrequency + limb.Phase) * limb.IdleAmplitude * idle;
            limb.Pivot.Rotation = limb.Axis * angle;
        }

        if (Body is not null)
        {
            var bob = Mathf.Abs(Mathf.Sin(_phase)) * BobHeight * _intensity;
            Body.Position = new Vector3(0, bob, 0);
            Body.Scale = new Vector3(1, 1 + Mathf.Sin(_time * 1.8f) * BreathScale * idle, 1);
        }
    }
}
