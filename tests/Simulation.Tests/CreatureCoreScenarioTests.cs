using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureCoreScenarioTests
{
    [Fact]
    public void CreatureCoreScenarioIsDeterministicAndRecoversFromInvalidSnapshot()
    {
        var first = RunScenario();
        var repeated = RunScenario();

        Assert.Equal(first, repeated);
    }

    private static ScenarioResult RunScenario()
    {
        var catalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var cautiousSpecies = catalog.GetRequired("creature");
        var exploratorySpecies = catalog.GetRequired("wolf");
        Assert.Equal(new TemperamentProfile(0.3, 0.3, 0.5), cautiousSpecies.Temperament);
        Assert.Equal(new TemperamentProfile(0.85, 0.65, 0.45), exploratorySpecies.Temperament);

        var world = new World();
        var cautiousCreature = world.SpawnEntity("creature", Vector3.Zero, satiety: 70);
        var exploratoryCreature = world.SpawnEntity("wolf", Vector3.Zero, satiety: 70);
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [cautiousCreature.Id] = new FakeNavigationAdapter(),
            [exploratoryCreature.Id] = new FakeNavigationAdapter()
        };
        Vector3[] goals = [new(1, 0, 0), new(3, 0, 0), new(5, 0, 0)];
        var wandering = new WanderingService(world, entity => navigators[entity.Id], goals, decisionSeed: 42);

        var cautiousTrip = wandering.Advance(
            cautiousCreature,
            cautiousSpecies,
            simulationTick: 7);
        var exploratoryTrip = wandering.Advance(
            exploratoryCreature,
            exploratorySpecies,
            simulationTick: 7);

        Assert.Equal(WanderingStatus.Traveling, cautiousTrip.Status);
        Assert.Equal(WildBehavior.CautiousRoaming, cautiousTrip.Behavior);
        Assert.Equal(goals[0], cautiousTrip.Target);
        Assert.Equal(WanderingStatus.Traveling, exploratoryTrip.Status);
        Assert.Equal(WildBehavior.CuriousExploration, exploratoryTrip.Behavior);
        Assert.Equal(goals[2], exploratoryTrip.Target);

        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(cautiousCreature.Id));
        Assert.Equal(CreatureCaptivityState.Captive, cautiousCreature.CaptivityState);
        Assert.Equal(CreatureCaptureResult.AlreadyCaptive, world.TryCaptureCreature(cautiousCreature.Id));
        Assert.Equal(CreatureCaptureResult.TargetNotFound, world.TryCaptureCreature("missing-creature"));
        Assert.Equal(CreatureCaptivityState.Captive, cautiousCreature.CaptivityState);

        const double simulationTime = 0.7;
        var validSnapshot = WorldSnapshotSerializer.Serialize(world, simulationTime);
        var invalidSnapshot = validSnapshot.Replace(
            "\"CaptivityState\": \"Captive\"",
            "\"CaptivityState\": 42",
            StringComparison.Ordinal);
        Assert.NotEqual(validSnapshot, invalidSnapshot);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            invalidSnapshot,
            catalog,
            wandering));

        Assert.Same(cautiousCreature, Assert.Single(world.Entities, entity => entity.Id == cautiousCreature.Id));
        Assert.Same(exploratoryCreature, Assert.Single(world.Entities, entity => entity.Id == exploratoryCreature.Id));
        Assert.Equal(CreatureCaptivityState.Captive, cautiousCreature.CaptivityState);
        Assert.Equal(CreatureCaptivityState.Wild, exploratoryCreature.CaptivityState);
        Assert.All(navigators.Values, navigator =>
            Assert.Equal(NavigationOutcome.InProgress, Assert.Single(navigator.Requests).Value));
        Assert.NotNull(cautiousCreature.Actions.Current);
        Assert.NotNull(exploratoryCreature.Actions.Current);

        var restoredTime = WorldSnapshotSerializer.Load(world, validSnapshot, catalog, wandering);
        Assert.Equal(simulationTime, restoredTime);
        Assert.Equal(validSnapshot, WorldSnapshotSerializer.Serialize(world, restoredTime));

        var restoredCreatures = world.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(CreatureCaptivityState.Captive, restoredCreatures[0].CaptivityState);
        Assert.Equal(CreatureCaptivityState.Wild, restoredCreatures[1].CaptivityState);
        Assert.All(restoredCreatures, entity => Assert.Null(entity.Actions.Current));
        Assert.All(navigators.Values, navigator =>
            Assert.Equal(NavigationOutcome.Cancelled, Assert.Single(navigator.Requests).Value));

        return new ScenarioResult(cautiousTrip, exploratoryTrip, validSnapshot);
    }

    private sealed record ScenarioResult(
        WanderingResult CautiousTrip,
        WanderingResult ExploratoryTrip,
        string DurableSnapshot);
}
