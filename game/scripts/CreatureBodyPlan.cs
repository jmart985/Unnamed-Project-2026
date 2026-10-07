namespace UnnamedProject2026;

/// Presentation classification shared by creature rigs. Species content selects a
/// plan through BodyCategory; individual species can still tune colors and scale.
public enum CreatureBodyPlan
{
    Quadruped,
    Primate,
    Ungulate,
    Canid,
    Humanoid
}

public static class CreatureBodyPlanResolver
{
    public static CreatureBodyPlan Resolve(string? category, bool isCreature)
    {
        if (!isCreature || string.Equals(category, "humanoid", StringComparison.OrdinalIgnoreCase))
            return CreatureBodyPlan.Humanoid;

        return category?.Trim().ToLowerInvariant() switch
        {
            "primate" or "upright_creature" => CreatureBodyPlan.Primate,
            "ungulate" => CreatureBodyPlan.Ungulate,
            "canid" => CreatureBodyPlan.Canid,
            _ => CreatureBodyPlan.Quadruped
        };
    }
}
