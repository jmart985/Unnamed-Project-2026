namespace Simulation;

public sealed record TemperamentProfile
{
    public TemperamentProfile(double boldness, double curiosity, double sociability)
    {
        Boldness = ValidateValue(boldness, nameof(boldness));
        Curiosity = ValidateValue(curiosity, nameof(curiosity));
        Sociability = ValidateValue(sociability, nameof(sociability));
    }

    public double Boldness { get; }
    public double Curiosity { get; }
    public double Sociability { get; }

    private static double ValidateValue(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Temperament values must be finite and between 0 and 1.");
        }

        return value;
    }
}
