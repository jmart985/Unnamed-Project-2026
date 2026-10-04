namespace Simulation;

/// <summary>
/// Monitors simulation events to derive behavioral shifts based on Temperament and Relationships.
/// </summary>
public sealed class BehavioralTrigger
{
    private readonly World _world;

    public BehavioralTrigger(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <summary>
    /// Responds to a combat event by potentially triggering a behavioral shift in the defender.
    /// </summary>
    public void OnDamageTaken(string attackerId, string defenderId, double damage)
    {
        if (damage <= 0) return;

        if (!_world.TryGetEntity(defenderId, out var defender) || defender is null) return;

        // Derive individual temperament for the defender
        // Note: In a full implementation, this would use the established IndividualTemperament service.
        var species = _world.GetSpecies(defender.SpeciesId);
        var temperament = IndividualTemperament.ForEntity(defender.Id, species.Temperament);

        // Logical Process: Aggression = (Species Aggression * Individual Variation) + (Relationship Fear/Hate)
        // For Phase 2, we simulate this by checking if the combination of temperament and damage 
        // crosses a threshold to trigger a "Hostile Pursuit" behavior.
        
        double hostilityScore = temperament.Aggression + (damage / 100.0);
        
        if (hostilityScore > 0.7)
        {
            // Trigger AI override to attack the attacker
            OverrideBehavior(defender, attackerId, "AggressivePursuit");
        }
    }

    private void OverrideBehavior(EntityRecord entity, string targetId, string behaviorId)
    {
        // This marks the entity's current activity and sets a target.
        // The Roaming/AI system will check these fields on the next tick.
        entity.Activity = behaviorId;
        entity.TargetId = targetId;
    }
}
