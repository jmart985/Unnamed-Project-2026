namespace Simulation;

/// <summary>Inputs for one deterministic aggregate update of a far record.</summary>
public sealed record FarEntitySimulationRequest(
    FarEntityRecord Record,
    int ElapsedTicks,
    int Seed,
    int AvailableFood = 0,
    int MigrationPressure = 0,
    string? MigrationTargetRegionId = null);

/// <summary>Aggregate changes produced without creating scene nodes.</summary>
public sealed record FarEntitySimulationResult(
    FarEntityRecord Record,
    int Births,
    int Deaths,
    int FoodConsumed,
    bool Migrated,
    int Seed);

/// <summary>
/// Advances far records using integer boundaries and fixed-point satiety.
/// Rules: hunger drains floor(rate * ticks * 1000) / 1000; each food unit
/// restores 50 satiety points; starvation removes one entity per full 1000
/// fixed-point satiety deficit per hunger-rate tick; a well-fed group with
/// two food units per member gains floor(count / 10) births; migration occurs
/// at pressure 100 or greater when a target region is supplied. Seed is carried
/// through the result so callers can partition deterministic simulations.
/// </summary>
public sealed class FarEntitySimulationService
{
    private const int FixedPointScale = 1000;
    private const double NutritionPerFood = 50;

    private readonly SpeciesCatalog _catalog;

    public FarEntitySimulationService(SpeciesCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public FarEntitySimulationResult Advance(FarEntitySimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ElapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(request.ElapsedTicks));
        if (request.AvailableFood < 0) throw new ArgumentOutOfRangeException(nameof(request.AvailableFood));
        if (request.MigrationPressure is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(request.MigrationPressure));
        if (!_catalog.TryGet(request.Record.SpeciesId, out var species) || species is null) throw new ArgumentException("Record species is not in the catalog.", nameof(request));

        var count = request.Record.Count;
        var foodConsumed = Math.Min(request.AvailableFood, count);
        var satietyFixed = (long)Math.Round(request.Record.AverageSatiety * FixedPointScale, MidpointRounding.ToEven);
        var drainFixed = (long)Math.Floor(species.HungerRate * request.ElapsedTicks * FixedPointScale);
        satietyFixed = Math.Clamp(satietyFixed - drainFixed + (long)(foodConsumed * NutritionPerFood * FixedPointScale), 0, 100 * FixedPointScale);

        var deaths = count == 0 ? 0 : (int)Math.Min(count, Math.Max(0, (drainFixed - (long)(request.Record.AverageSatiety * FixedPointScale)) / Math.Max(1L, (long)Math.Ceiling(species.HungerRate * FixedPointScale))));
        var survivors = count - deaths;
        var foodForBirths = (long)survivors * 2;
        var births = survivors > 0 && satietyFixed >= 60L * FixedPointScale && request.AvailableFood >= foodForBirths ? survivors / 10 : 0;
        var nextCountLong = (long)survivors + births;
        if (nextCountLong > int.MaxValue) throw new InvalidOperationException("Far population exceeds the supported range.");
        var nextCount = (int)nextCountLong;
        var regionId = request.Record.RegionId;
        var migrated = request.MigrationPressure >= 100 && !string.IsNullOrWhiteSpace(request.MigrationTargetRegionId) && request.MigrationTargetRegionId != regionId;
        if (migrated) regionId = request.MigrationTargetRegionId!;

        var nextResources = request.Record.Resources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (nextResources.TryGetValue(ItemIds.Food, out var storedFood)) nextResources[ItemIds.Food] = Math.Max(0, storedFood - foodConsumed);
        return new FarEntitySimulationResult(new FarEntityRecord(regionId, request.Record.SpeciesId, nextCount, satietyFixed / (double)FixedPointScale, nextResources), births, deaths, foodConsumed, migrated, request.Seed);
    }
}
