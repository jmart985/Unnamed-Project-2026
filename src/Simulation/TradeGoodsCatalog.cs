using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed record TradeGoodDefinition(string ResourceId, int CoinValue);

public sealed class TradeGoodsCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly Dictionary<string, TradeGoodDefinition> _byResourceId;
    private readonly ReadOnlyCollection<TradeGoodDefinition> _tradeGoods;

    private TradeGoodsCatalog(IEnumerable<TradeGoodDefinition> tradeGoods)
    {
        var orderedTradeGoods = tradeGoods
            .OrderBy(tradeGood => tradeGood.ResourceId, StringComparer.Ordinal)
            .ToArray();
        _byResourceId = orderedTradeGoods.ToDictionary(
            tradeGood => tradeGood.ResourceId,
            StringComparer.Ordinal);
        _tradeGoods = Array.AsReadOnly(orderedTradeGoods);
    }

    public IReadOnlyCollection<TradeGoodDefinition> TradeGoods => _tradeGoods;

    public static TradeGoodsCatalog LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A trade-good data file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static TradeGoodsCatalog Load(Stream stream)
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
        var resourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Trade-good data cannot contain a null record.");
            }

            var resourceId = entry.ResourceId;
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                throw new InvalidDataException("Each trade good must define a nonblank resourceId.");
            }

            if (resourceId == ItemIds.Coin)
            {
                throw new InvalidDataException("The coin resource is reserved as currency and cannot be a trade good.");
            }

            if (!ItemIds.IsKnown(resourceId))
            {
                throw new InvalidDataException($"Trade good '{resourceId}' references an unknown resource ID.");
            }

            if (!resourceIds.Add(resourceId))
            {
                throw new InvalidDataException($"Trade good resource ID '{resourceId}' is defined more than once.");
            }

            var coinValue = entry.CoinValue
                ?? throw new InvalidDataException($"Trade good '{resourceId}' must define a coinValue.");
            if (coinValue <= 0)
            {
                throw new InvalidDataException($"Trade good '{resourceId}' coinValue must be a positive integer.");
            }

            definitions.Add(new TradeGoodDefinition(resourceId, coinValue));
        }

        return new TradeGoodsCatalog(definitions);
    }

    public bool IsTradeable(string? resourceId)
    {
        return TryGetCoinValue(resourceId, out _);
    }

    public bool TryGetCoinValue(string? resourceId, out int coinValue)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            coinValue = default;
            return false;
        }

        if (_byResourceId.TryGetValue(resourceId, out var tradeGood))
        {
            coinValue = tradeGood.CoinValue;
            return true;
        }

        coinValue = default;
        return false;
    }

    private sealed class TradeGoodData
    {
        public string? ResourceId { get; init; }

        [JsonNumberHandling(JsonNumberHandling.Strict)]
        public int? CoinValue { get; init; }
    }
}
