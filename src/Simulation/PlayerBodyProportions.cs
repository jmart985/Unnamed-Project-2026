namespace Simulation;

/// <summary>
/// Rig-independent body proportions derived from character-creation choices. Lengths are metres for a
/// standing human; widths are multipliers applied to torso, limb thickness and shoulder/hip spacing.
/// The presentation layer builds a jointed rig from these values so the collision capsule is never scaled.
/// </summary>
public sealed record PlayerBodyProportions(
    float Height,
    float TorsoWidth,
    float TorsoDepth,
    float LimbThickness,
    float ShoulderSpan,
    float HipSpan,
    float ChestSize)
{
    public float LegLength => Height * 0.47f;
    public float TorsoLength => Height * 0.32f;
    public float NeckLength => Height * 0.04f;
    public float HeadRadius => Height * 0.075f;
    public float ArmLength => Height * 0.40f;

    public static PlayerBodyProportions From(PlayerCharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsValid)
        {
            throw new ArgumentException("The player profile is invalid.", nameof(profile));
        }

        var height = profile.Height switch
        {
            PlayerHeight.Small => 1.58f,
            PlayerHeight.Medium => 1.8f,
            PlayerHeight.Tall => 2.0f,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var (width, depth, limb) = profile.BodyType switch
        {
            PlayerBodyType.Lean => (0.84f, 0.86f, 0.82f),
            PlayerBodyType.Athletic => (1.0f, 0.94f, 1.0f),
            PlayerBodyType.Average => (1.0f, 1.0f, 1.0f),
            PlayerBodyType.Stocky => (1.12f, 1.08f, 1.2f),
            PlayerBodyType.Thick => (1.24f, 1.18f, 1.38f),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var female = profile.Sex == PlayerSex.Female;
        var weightScale = 0.86f + (float)profile.Weight * 0.28f;
        var muscleScale = 0.88f + (float)profile.Muscle * 0.24f;
        width *= weightScale;
        depth *= weightScale;
        limb *= muscleScale;
        // Athletic builds trade torso bulk for broader shoulders; sex shifts the shoulder/hip balance.
        var shoulder = (female ? 0.92f : 1.06f) * (profile.BodyType == PlayerBodyType.Athletic ? 1.1f : 1f) * muscleScale;
        var hip = female ? 1.08f : 0.96f;
        return new PlayerBodyProportions(height, width, depth, limb, shoulder * width, hip * width, female ? 1f : 0f);
    }
}
