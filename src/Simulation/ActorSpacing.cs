using System.Numerics;

namespace Simulation;

/// <summary>Authoritative world-space separation checks for live actors.</summary>
public static class ActorSpacing
{
    public static double RadiusFor(SpeciesDefinition species)
    {
        ArgumentNullException.ThrowIfNull(species);
        return species.BodyCategory.ToLowerInvariant() switch
        {
            "quadruped" => 0.75,
            "large" => 1.0,
            "small" => 0.35,
            _ => 0.5
        };
    }

    public static bool IsPositionAvailable(World world, EntityRecord moving, Vector3 position, double clearance = 0.05)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(moving);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) return false;
        if (!double.IsFinite(clearance) || clearance < 0) throw new ArgumentOutOfRangeException(nameof(clearance));

        var movingRadius = RadiusFor(world.GetSpecies(moving.SpeciesId));
        return world.Entities
            .Where(entity => !ReferenceEquals(entity, moving) && !entity.Health.IsDead)
            .All(entity => Vector3.Distance(position, entity.Position)
                >= movingRadius + RadiusFor(world.GetSpecies(entity.SpeciesId)) + clearance);
    }
}
