using System.Text.Json.Serialization;

namespace Simulation;

public sealed record ItemDefinition
{
    public ItemDefinition(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An item ID is required.", nameof(id));
        }

        Id = id;
    }

    [JsonPropertyName("id")]
    public string Id { get; }
}
