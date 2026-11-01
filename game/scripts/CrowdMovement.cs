using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    // A clear plaza allows direct paths through the initial NPC line. Slide around
    // a stationary neighbour instead of rejecting the same forward step forever.
    private NumericsVector3 SlidePastActor(string entityId, NumericsVector3 current, NumericsVector3 proposed,
        NumericsVector3 obstacle, float separation)
    {
        var away = current - obstacle;
        away.Y = 0;
        var tangent = NumericsVector3.Normalize(new NumericsVector3(-away.Z, 0, away.X));
        var movement = proposed - current;
        if (NumericsVector3.Dot(tangent, movement) < 0) tangent = -tangent;
        var candidate = current + tangent * new NumericsVector3(movement.X, 0, movement.Z).Length();
        candidate.Y = proposed.Y;
        // The tangent replaces the earlier follow-clipped step. Reapply both
        // holding boundaries before validating this new endpoint against obstacles.
        candidate = _squad?.ConstrainFollowStep(entityId, current, candidate) ?? candidate;
        candidate = _orders?.ConstrainFollowStep(entityId, current, candidate) ?? candidate;
        if (Math.Abs(candidate.X) > WorldHalfExtent || Math.Abs(candidate.Z) > WorldHalfExtent
            || IsBlockedCell(candidate.X, candidate.X, candidate.Z, candidate.Z, CurrentWorkbenchPositions(), _world.Structures))
            return current;
        foreach (var other in _world.Entities.Where(other => other.Id != entityId && !other.Health.IsDead))
        {
            var distance = NumericsVector3.DistanceSquared(candidate, other.Position);
            if (distance < separation * separation && distance <= NumericsVector3.DistanceSquared(current, other.Position)) return current;
        }
        return candidate;
    }
}
