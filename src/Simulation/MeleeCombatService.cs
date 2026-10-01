namespace Simulation;

public sealed record MeleeAttackRequest(string AttackerId, string DefenderId, string BodyPart, double Damage);
public enum MeleeAttackResult
{
    Success,
    InvalidRequest,
    MissingEntity,
    UnknownBodyPart,
    OutOfRange,
    AttackerIncapacitated,
    DefenderUnavailable,
    NoDamage
}
public sealed record MeleeAttackResolution(MeleeAttackResult Result, double AppliedDamage = 0, bool DefenderIncapacitated = false, bool DefenderDead = false);

public static class MeleeCombatService
{
    public static MeleeAttackResolution Resolve(World? world, MeleeAttackRequest? request, double maximumReach = double.MaxValue)
    {
        if (world is null
            || request is null
            || string.IsNullOrWhiteSpace(request.AttackerId)
            || string.IsNullOrWhiteSpace(request.DefenderId)
            || string.Equals(request.AttackerId, request.DefenderId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.BodyPart)
            || !double.IsFinite(request.Damage)
            || request.Damage <= 0)
        {
            return new(MeleeAttackResult.InvalidRequest);
        }
        if (!double.IsFinite(maximumReach) || maximumReach < 0) return new(MeleeAttackResult.InvalidRequest);
        if (!world.TryGetEntity(request.AttackerId, out var attacker) || attacker is null || !world.TryGetEntity(request.DefenderId, out var defender) || defender is null)
            return new(MeleeAttackResult.MissingEntity);
        if (MeleeReachValidator.Validate(attacker!.Position, defender.Position, maximumReach) == MeleeReachResult.OutOfRange) return new(MeleeAttackResult.OutOfRange);
        if (attacker.Health.IsIncapacitated) return new(MeleeAttackResult.AttackerIncapacitated);
        if (defender.Health.IsDead) return new(MeleeAttackResult.DefenderUnavailable);
        if (!defender.Health.TryGetPart(request.BodyPart, out var part) || part is null)
            return new(MeleeAttackResult.UnknownBodyPart);
        if (part.IsDestroyed) return new(MeleeAttackResult.DefenderUnavailable);
        if (!defender.Health.TryDamage(request.BodyPart, request.Damage, out var applied))
            return new(MeleeAttackResult.DefenderUnavailable);
        if (applied <= 0) return new(MeleeAttackResult.NoDamage);
        return new(MeleeAttackResult.Success, applied, defender.Health.IsIncapacitated, defender.Health.IsDead);
    }
}
