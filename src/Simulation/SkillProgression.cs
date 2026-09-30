namespace Simulation;

public sealed record SkillDefinition
{
    public SkillDefinition(string id, int maximumLevel, double experiencePerLevel = 100)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A skill ID is required.", nameof(id));
        if (maximumLevel < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        if (!double.IsFinite(experiencePerLevel) || experiencePerLevel <= 0) throw new ArgumentOutOfRangeException(nameof(experiencePerLevel));
        Id = id; MaximumLevel = maximumLevel; ExperiencePerLevel = experiencePerLevel;
    }
    public string Id { get; }
    public int MaximumLevel { get; }
    public double ExperiencePerLevel { get; }
}

public enum SkillProgressionResult { Applied, UnknownSkill, InvalidExperience }

public sealed class SkillState
{
    internal SkillState(SkillDefinition definition) { Definition = definition; }
    public SkillDefinition Definition { get; }
    public int Level { get; private set; }
    public double Experience { get; private set; }
    public SkillProgressionResult AddExperience(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0) return SkillProgressionResult.InvalidExperience;
        if (Level >= Definition.MaximumLevel) return SkillProgressionResult.Applied;
        Experience += amount;
        while (Experience >= Definition.ExperiencePerLevel && Level < Definition.MaximumLevel) { Experience -= Definition.ExperiencePerLevel; Level++; }
        if (Level >= Definition.MaximumLevel) Experience = 0;
        return SkillProgressionResult.Applied;
    }
}

public sealed class SkillSet
{
    private readonly Dictionary<string, SkillState> _skills;
    public SkillSet(IEnumerable<SkillDefinition>? definitions = null)
    {
        var source = definitions ?? DefaultDefinitions;
        _skills = source.ToDictionary(definition => definition.Id, definition => new SkillState(definition), StringComparer.Ordinal);
    }
    public IReadOnlyCollection<SkillState> Skills => _skills.Values;
    public bool TryGet(string? id, out SkillState? skill) => _skills.TryGetValue(id ?? string.Empty, out skill);
    public SkillProgressionResult AddExperience(string? id, double amount) => TryGet(id, out var skill) && skill is not null ? skill.AddExperience(amount) : SkillProgressionResult.UnknownSkill;
    public static IReadOnlyList<SkillDefinition> DefaultDefinitions { get; } = [new("Athletics", 20), new("Combat", 20), new("Crafting", 20), new("Medicine", 20), new("Hauling", 20)];
}
