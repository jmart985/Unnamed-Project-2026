namespace Simulation;

/// <summary>
/// Defines the level of authority one entity has over another.
/// </summary>
public enum AuthorityLevel
{
    /// <summary>No authority; commands are ignored.</summary>
    None,
    /// <summary>Mutual trust/loyalty; commands are likely to be obeyed.</summary>
    Bonded,
    /// <summary>Formal ownership; commands are generally obeyed.</summary>
    Owned,
    /// <summary>Complete control; commands are strictly obeyed.</summary>
    Enslaved
}

/// <summary>
/// Determines if an actor has sufficient authority to issue a command to a target.
/// </summary>
public static class AuthorityMatrix
{
    public static AuthorityLevel GetAuthority(EntityRecord actor, EntityRecord target, World world)
    {
        // 1. Check for Enslavement/Ownership (Highest priority)
        if (target.CaptivityState == CreatureCaptivityState.Domesticated && target.AllegianceFactionId == actor.AllegianceFactionId)
        {
            return AuthorityLevel.Owned;
        }

        if (target.CaptivityState == CreatureCaptivityState.Captive)
        {
            // In a full system, we would check if the actor is the one who captured the target.
            return AuthorityLevel.Owned;
        }

        // 2. Check for Bond (Derived from Relationship state)
        if (world.TryGetRelationship(target.Id, actor.Id, out var rel) && rel != null)
        {
            if (rel.Loyalty > 70) return AuthorityLevel.Bonded;
        }

        return AuthorityLevel.None;
    }

    public static bool CanObey(AuthorityLevel level, string commandType)
    {
        return commandType switch
        {
            "follow" or "stay" => level >= AuthorityLevel.Bonded,
            "attack" => level >= AuthorityLevel.Owned,
            _ => level >= AuthorityLevel.Enslaved
        };
    }
}
