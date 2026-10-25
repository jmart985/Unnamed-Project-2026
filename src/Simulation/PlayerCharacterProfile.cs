namespace Simulation;

public enum PlayerHeight
{
    Small,
    Medium,
    Tall
}

public enum PlayerBodyType
{
    Lean,
    Athletic,
    Average,
    Stocky,
    Thick
}

public enum PlayerSex
{
    Female,
    Male
}

public sealed record PlayerCharacterProfile(
    string Name,
    PlayerHeight Height = PlayerHeight.Medium,
    PlayerBodyType BodyType = PlayerBodyType.Average,
    PlayerSex Sex = PlayerSex.Female,
    double Weight = 0.5,
    double Muscle = 0.5)
{
    public BiologicalSex EntitySex => Sex == PlayerSex.Male
        ? BiologicalSex.Male
        : BiologicalSex.Female;

    public static PlayerCharacterProfile Create(
        string? name,
        PlayerHeight height = PlayerHeight.Medium,
        PlayerBodyType bodyType = PlayerBodyType.Average,
        PlayerSex sex = PlayerSex.Female,
        double weight = 0.5,
        double muscle = 0.5)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A player name is required.", nameof(name));
        }

        if (!Enum.IsDefined(height))
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (!Enum.IsDefined(bodyType))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyType));
        }

        if (!Enum.IsDefined(sex))
        {
            throw new ArgumentOutOfRangeException(nameof(sex));
        }

        ValidateSlider(weight, nameof(weight));
        ValidateSlider(muscle, nameof(muscle));

        return new PlayerCharacterProfile(name.Trim(), height, bodyType, sex, weight, muscle);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && string.Equals(Name, Name.Trim(), StringComparison.Ordinal)
        && Enum.IsDefined(Height)
        && Enum.IsDefined(BodyType)
        && Enum.IsDefined(Sex)
        && double.IsFinite(Weight) && Weight is >= 0 and <= 1
        && double.IsFinite(Muscle) && Muscle is >= 0 and <= 1;

    private static void ValidateSlider(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, "Body sliders must be finite values between 0 and 1.");
    }
}
