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
    PlayerSex Sex = PlayerSex.Female)
{
    public static PlayerCharacterProfile Create(
        string? name,
        PlayerHeight height = PlayerHeight.Medium,
        PlayerBodyType bodyType = PlayerBodyType.Average,
        PlayerSex sex = PlayerSex.Female)
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

        return new PlayerCharacterProfile(name.Trim(), height, bodyType, sex);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && string.Equals(Name, Name.Trim(), StringComparison.Ordinal)
        && Enum.IsDefined(Height)
        && Enum.IsDefined(BodyType)
        && Enum.IsDefined(Sex);
}
