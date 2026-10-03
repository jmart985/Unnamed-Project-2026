using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed record TradeGoodDefinition(string Id, int CoinValue);

public sealed class TradeGoodCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly Dictionary<string, TradeGoodDefinition> _byId;
    private readonly ReadOnlyCollection<TradeGoodDefinition> _tradeGoods;

    private TradeGoodCatalog(IEnumerable<TradeGoodDefinition> tradeGoods)
    {
        var orderedTradeGoods = tradeGoods.OrderBy(good => good.Id, StringComparer.Ordinal).ToArray();
        _byId = orderedTradeGoods.ToDictionary(good => good.Id, StringComparer.Ordinal);
        _tradeGoods = Array.AsReadOnly(orderedTradeGoods);
    }

    public IReadOnlyCollection<TradeGoodDefinition> TradeGoods => _tradeGoods;

    public static TradeGoodCatalog LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A trade-good data file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static TradeGoodCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        TradeGoodData?[]? entries;
        try
        {
            entries = JsonSerializer.Deserialize<TradeGoodData?[]>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Trade-good data must be a JSON array of valid trade-good records.",
                exception);
        }

        if (entries is null || entries.Length == 0)
        {
            throw new InvalidDataException("Trade-good data must contain at least one trade good.");
        }

        var definitions = new List<TradeGoodDefinition>(entries.Length);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Trade-good data cannot contain a null trade-good record.");
            }

            var id = entry.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidDataException("Each trade good must define a non-blank item ID.");
            }

            if (id == ItemIds.Coin)
            {
                throw new InvalidDataException($"Currency item ID '{ItemIds.Coin}' cannot be configured as a trade good.");
            }

            if (!ItemIds.IsKnown(id))
            {
                throw new InvalidDataException($"Trade good references unknown item ID '{id}'.");
            }

            if (!ids.Add(id))
            {
                throw new InvalidDataException($"Trade good item ID '{id}' is defined more than once.");
            }

            var coinValue = entry.CoinValue
                ?? throw new InvalidDataException($"Trade good '{id}' must define coinValue.");
            if (coinValue <= 0)
            {
                throw new InvalidDataException($"Trade good '{id}' coinValue must be a positive integer.");
            }

            definitions.Add(new TradeGoodDefinition(id, coinValue));
        }

        return new TradeGoodCatalog(definitions);
    }

    public bool IsTradeable(string? itemId) =>
        !string.IsNullOrWhiteSpace(itemId) && _byId.ContainsKey(itemId);

    public bool TryGetCoinValue(string? itemId, out int coinValue)
    {
        if (!string.IsNullOrWhiteSpace(itemId)
            && _byId.TryGetValue(itemId, out var definition))
        {
            coinValue = definition.CoinValue;
            return true;
        }

        coinValue = 0;
        return false;
    }

    public int GetCoinValue(string? itemId)
    {
        if (TryGetCoinValue(itemId, out var coinValue))
        {
            return coinValue;
        }

        throw new KeyNotFoundException(
            string.IsNullOrWhiteSpace(itemId)
                ? "A trade-good item ID is required."
                : $"Item '{itemId}' is not a trade good.");
    }

    private sealed class TradeGoodData
    {
        public string? Id { get; init; }
        public int? CoinValue { get; init; }
    }
}
