using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed record RelationshipEventDefinition(
    string Id,
    RelationshipAdjustment Adjustment);

public sealed class RelationshipEventCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly Dictionary<string, RelationshipEventDefinition> _byId;
    private readonly ReadOnlyCollection<RelationshipEventDefinition> _definitions;

    private RelationshipEventCatalog(IEnumerable<RelationshipEventDefinition> definitions)
    {
        var orderedDefinitions = definitions.ToArray();
        _byId = orderedDefinitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        _definitions = Array.AsReadOnly(orderedDefinitions);
    }

    public IReadOnlyCollection<RelationshipEventDefinition> Definitions => _definitions;

    public static RelationshipEventCatalog LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A relationship event data file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static RelationshipEventCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        RelationshipEventData?[]? entries;
        try
        {
            entries = JsonSerializer.Deserialize<RelationshipEventData?[]>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Relationship event data must be a JSON array of valid event records.",
                exception);
        }

        if (entries is null || entries.Length == 0)
        {
            throw new InvalidDataException("Relationship event data must contain at least one event.");
        }

        var definitions = new List<RelationshipEventDefinition>(entries.Length);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Relationship event data cannot contain a null event.");
            }

            var id = ValidateId(entry.Id);
            if (!ids.Add(id))
            {
                throw new InvalidDataException($"Relationship event ID '{id}' is defined more than once.");
            }

            if (entry.FamiliarityDelta is not { } familiarity
                || entry.TrustDelta is not { } trust
                || entry.FearDelta is not { } fear
                || entry.LoyaltyDelta is not { } loyalty)
            {
                throw new InvalidDataException(
                    $"Relationship event '{id}' must define all four relationship deltas.");
            }

            var adjustment = new RelationshipAdjustment(familiarity, trust, fear, loyalty);
            if (!double.IsFinite(familiarity)
                || !double.IsFinite(trust)
                || !double.IsFinite(fear)
                || !double.IsFinite(loyalty))
            {
                throw new InvalidDataException(
                    $"Relationship event '{id}' deltas must be finite.");
            }

            definitions.Add(new RelationshipEventDefinition(id, adjustment));
        }

        return new RelationshipEventCatalog(definitions);
    }

    public bool TryGet(string? id, out RelationshipEventDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        return _byId.TryGetValue(id, out definition);
    }

    private static string ValidateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id[0] == '-'
            || id[^1] == '-')
        {
            throw new InvalidDataException("Each relationship event must have a valid lowercase event ID.");
        }

        var previousWasSeparator = false;
        foreach (var character in id)
        {
            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    throw new InvalidDataException($"Relationship event ID '{id}' is invalid.");
                }

                previousWasSeparator = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
            {
                throw new InvalidDataException(
                    $"Relationship event ID '{id}' must use lowercase letters, digits, and single hyphens.");
            }

            previousWasSeparator = false;
        }

        return id;
    }

    private sealed class RelationshipEventData
    {
        public string? Id { get; init; }
        public double? FamiliarityDelta { get; init; }
        public double? TrustDelta { get; init; }
        public double? FearDelta { get; init; }
        public double? LoyaltyDelta { get; init; }
    }
}

public enum RelationshipEventApplyResult
{
    Applied,
    UnknownEvent,
    InvalidParticipant,
    InvalidDelta
}

public sealed class RelationshipEventService
{
    private readonly World _world;
    private readonly RelationshipEventCatalog _catalog;

    internal bool HasEvent(string eventId) => _catalog.TryGet(eventId, out _);
    internal bool BelongsTo(World world) => ReferenceEquals(_world, world);

    public RelationshipEventService(World world, RelationshipEventCatalog catalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public RelationshipEventApplyResult Apply(
        string? observerEntityId,
        string? targetEntityId,
        string? eventId)
    {
        if (!_catalog.TryGet(eventId, out var definition) || definition is null)
        {
            return RelationshipEventApplyResult.UnknownEvent;
        }

        return _world.ApplyRelationshipAdjustment(
            observerEntityId,
            targetEntityId,
            definition.Adjustment) switch
        {
            RelationshipAdjustmentResult.Applied => RelationshipEventApplyResult.Applied,
            RelationshipAdjustmentResult.InvalidParticipant => RelationshipEventApplyResult.InvalidParticipant,
            RelationshipAdjustmentResult.InvalidDelta => RelationshipEventApplyResult.InvalidDelta,
            _ => throw new ArgumentOutOfRangeException(nameof(eventId))
        };
    }
}
