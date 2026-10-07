namespace Simulation;

public sealed record PlayerBodyPartAdjustment(string PartName, double X, double Y, double Z)
{
    public const double Minimum = -0.25;
    public const double Maximum = 0.25;

    public bool IsValid => !string.IsNullOrWhiteSpace(PartName)
        && string.Equals(PartName, PartName.Trim(), StringComparison.Ordinal)
        && IsBounded(X) && IsBounded(Y) && IsBounded(Z);

    public static PlayerBodyPartAdjustment Create(string partName, double x = 0, double y = 0, double z = 0)
    {
        if (string.IsNullOrWhiteSpace(partName)) throw new ArgumentException("A body part name is required.", nameof(partName));
        Validate(x, nameof(x));
        Validate(y, nameof(y));
        Validate(z, nameof(z));
        return new(partName.Trim(), x, y, z);
    }

    private static bool IsBounded(double value) => double.IsFinite(value) && value is >= Minimum and <= Maximum;

    private static void Validate(double value, string parameterName)
    {
        if (!IsBounded(value))
            throw new ArgumentOutOfRangeException(parameterName, $"Body-part offsets must be between {Minimum} and {Maximum}.");
    }
}

public static class PlayerBodyCustomization
{
    public static IReadOnlyList<string> SupportedParts { get; } =
        ["Head", "Torso", "LeftShoulder", "RightShoulder", "LeftElbow", "RightElbow", "LeftHip", "RightHip", "LeftKnee", "RightKnee"];

    public static PlayerBodyPartAdjustment Reset(string partName)
    {
        if (!SupportedParts.Contains(partName, StringComparer.Ordinal))
            throw new ArgumentException($"Unsupported body part '{partName}'.", nameof(partName));
        return PlayerBodyPartAdjustment.Create(partName);
    }

    public static PlayerBodyPartAdjustment Mirror(PlayerBodyPartAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        var mirroredName = adjustment.PartName switch
        {
            "LeftShoulder" => "RightShoulder", "RightShoulder" => "LeftShoulder",
            "LeftElbow" => "RightElbow", "RightElbow" => "LeftElbow",
            "LeftHip" => "RightHip", "RightHip" => "LeftHip",
            "LeftKnee" => "RightKnee", "RightKnee" => "LeftKnee",
            _ => adjustment.PartName
        };
        return PlayerBodyPartAdjustment.Create(mirroredName, -adjustment.X, adjustment.Y, adjustment.Z);
    }
}
