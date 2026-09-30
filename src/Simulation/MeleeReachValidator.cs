using System.Numerics;

namespace Simulation;

public enum MeleeReachResult { InRange, OutOfRange, InvalidReach }

public static class MeleeReachValidator
{
    public static MeleeReachResult Validate(Vector3 attackerPosition, Vector3 defenderPosition, double maximumReach)
    {
        if (!double.IsFinite(maximumReach) || maximumReach < 0 || !float.IsFinite(attackerPosition.X) || !float.IsFinite(attackerPosition.Y) || !float.IsFinite(attackerPosition.Z) || !float.IsFinite(defenderPosition.X) || !float.IsFinite(defenderPosition.Y) || !float.IsFinite(defenderPosition.Z)) return MeleeReachResult.InvalidReach;
        return Vector3.Distance(attackerPosition, defenderPosition) <= maximumReach ? MeleeReachResult.InRange : MeleeReachResult.OutOfRange;
    }
}
