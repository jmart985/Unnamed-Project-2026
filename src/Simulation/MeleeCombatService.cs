namespace Simulation;

public sealed record MeleeAttackRequest(string AttackerId, string DefenderId, string BodyPart, double Damage);
public enum MeleeAttackResult { Success, InvalidRequest, MissingEntity, UnknownBodyPart, OutOfRange }
public sealed record MeleeAttackResolution(MeleeAttackResult Result, double AppliedDamage = 0, bool DefenderIncapacitated = false);

public static class MeleeCombatService
{
    public static MeleeAttackResolution Resolve(World? world, MeleeAttackRequest? request, double maximumReach = double.MaxValue)
    {
        if (world is null || request is null || string.IsNullOrWhiteSpace(request.AttackerId) || string.IsNullOrWhiteSpace(request.DefenderId) || string.IsNullOrWhiteSpace(request.BodyPart) || !double.IsFinite(request.Damage) || request.Damage <= 0)
            return new(MeleeAttackResult.InvalidRequest);
        if (!double.IsFinite(maximumReach) || maximumReach < 0) return new(MeleeAttackResult.InvalidRequest);
        if (!world.TryGetEntity(request.AttackerId, out var attacker) || attacker is null || !world.TryGetEntity(request.DefenderId, out var defender) || defender is null)
            return new(MeleeAttackResult.MissingEntity);
        if (MeleeReachValidator.Validate(attacker!.Position, defender.Position, maximumReach) == MeleeReachResult.OutOfRange) return new(MeleeAttackResult.OutOfRange);
        if (!defender.Health.TryGetPart(request.BodyPart, out var part) || part is null)
            return new(MeleeAttackResult.UnknownBodyPart);
        var applied = part.ApplyDamage(request.Damage);
        return new(MeleeAttackResult.Success, applied, defender.Health.IsIncapacitated);
    }
}


