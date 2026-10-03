namespace Simulation;

public sealed record TemperamentProfile
{
    public TemperamentProfile(
        double boldness,
        double aggression,
        double fear,
        double curiosity,
        double sociability,
        double territoriality)
    {
        Boldness = ValidateValue(boldness, nameof(boldness));
        Aggression = ValidateValue(aggression, nameof(aggression));
        Fear = ValidateValue(fear, nameof(fear));
        Curiosity = ValidateValue(curiosity, nameof(curiosity));
        Sociability = ValidateValue(sociability, nameof(sociability));
        Territoriality = ValidateValue(territoriality, nameof(territoriality));
    }

    public double Boldness { get; }
    public double Aggression { get; }
    public double Fear { get; }
    public double Curiosity { get; }
    public double Sociability { get; }
    public double Territoriality { get; }

    private static double ValidateValue(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Temperament values must be finite and between 0 and 1.");
        }

        return value;
    }
}
