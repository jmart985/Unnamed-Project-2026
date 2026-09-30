namespace Simulation;

public enum CombatTargetResult { Selected, InvalidTarget, NoTarget }
public sealed record CombatTargetSelection(CombatTargetResult Result, string? BodyPart = null);

public static class CombatTargetSelector
{
    public static CombatTargetSelection Select(CharacterHealth? health, string? requestedPart)
    {
        if (health is null) return new(CombatTargetResult.NoTarget);
        if (!string.IsNullOrWhiteSpace(requestedPart)) return health.TryGetPart(requestedPart, out var explicitPart) && explicitPart is not null ? new(CombatTargetResult.Selected, explicitPart.Name) : new(CombatTargetResult.InvalidTarget);
        if (health.TryGetPart("Torso", out var torso) && torso is not null && !torso.IsDestroyed) return new(CombatTargetResult.Selected, torso.Name);
        var fallback = health.Parts.FirstOrDefault(part => part.RequiredForConsciousness && !part.IsDestroyed);
        return fallback is null ? new(CombatTargetResult.NoTarget) : new(CombatTargetResult.Selected, fallback.Name);
    }
}
