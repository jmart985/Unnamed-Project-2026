namespace Simulation;

/// <summary>
/// Defines the type of interaction being attempted.
/// </summary>
public enum InteractionType
{
    /// <summary>Direct physical state change (e.g., Capture, Feed, Harvest).</summary>
    Physical,
    /// <summary>Social bond mutation (e.g., Greet, Bribe, Threaten).</summary>
    Social,
    /// <summary>AI behavioral override (e.g., Follow, Stay, Attack Target).</summary>
    Command
}

/// <summary>
/// Encapsulates the intent of an interaction between two entities or an entity and the world.
/// </summary>
public sealed record InteractionRequest(
    string ActorId,
    string? TargetId,
    InteractionType Type,
    string ActionId,
    // A single-item quantity descriptor; ownership stays in the actor's World inventory.
    ResourceContainer? ProvidedItem = null,
    object? Context = null);

/// <summary>
/// The outcome of an interaction resolution.
/// </summary>
public sealed record InteractionResult(
    bool Succeeded,
    string Reason = "",
    object? Data = null);
