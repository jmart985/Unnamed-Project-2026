namespace Simulation;

public sealed record MeleeAttackRequest(string AttackerId, string DefenderId, string BodyPart, double Damage);
public enum MeleeAttackResult { Success, InvalidRequest, MissingEntity, UnknownBodyPart }
public sealed record MeleeAttackResolution(MeleeAttackResult Result, double AppliedDamage = 0, bool DefenderIncapacitated = false);

public static class MeleeCombatService
{
    public static MeleeAttackResolution Resolve(World? world, MeleeAttackRequest? request)
    {
        if (world is null || request is null || string.IsNullOrWhiteSpace(request.AttackerId) || string.IsNullOrWhiteSpace(request.DefenderId) || string.IsNullOrWhiteSpace(request.BodyPart) || !double.IsFinite(request.Damage) || request.Damage <= 0)
            return new(MeleeAttackResult.InvalidRequest);
        if (!world.TryGetEntity(request.AttackerId, out _) || !world.TryGetEntity(request.DefenderId, out var defender) || defender is null)
            return new(MeleeAttackResult.MissingEntity);
        if (!defender.Health.TryGetPart(request.BodyPart, out var part) || part is null)
            return new(MeleeAttackResult.UnknownBodyPart);
        var applied = part.ApplyDamage(request.Damage);
        return new(MeleeAttackResult.Success, applied, defender.Health.IsIncapacitated);
    }
}
