using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class IndividualTemperamentTests
{
    [Fact]
    public void TemperamentVariesByStableEntityIdWithinTraitBounds()
    {
        var species = new TemperamentProfile(0.5, 0.3, 0.4, 0.4, 0.6, 0.2);

        var first = IndividualTemperament.ForEntity("entity-000001", species);
        var repeated = IndividualTemperament.ForEntity("entity-000001", species);
        var second = IndividualTemperament.ForEntity("entity-000002", species);

        Assert.Equal(first, repeated);
        Assert.NotEqual(first, second);
        Assert.All(
            new[]
            {
                first.Boldness, first.Aggression, first.Fear, first.Curiosity, first.Sociability, first.Territoriality,
                second.Boldness, second.Aggression, second.Fear, second.Curiosity, second.Sociability, second.Territoriality
            },
            trait => Assert.InRange(trait, 0, 1));
    }

    [Fact]
    public void CuriousAndSocialIndividualsChooseDifferentWanderingGoals()
    {
        var catalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var species = catalog.GetRequired("creature");
        var world = new World();
        var candidates = Enumerable.Range(0, 6)
            .Select(_ => world.SpawnEntity(species.Id, new Vector3(10, 0, 0)))
            .ToArray();
        var social = candidates[4];
        var cautious = candidates[0];
        var curious = candidates[2];
        world.TrySetEntityPosition(social.Id, Vector3.Zero);
        world.TrySetEntityPosition(cautious.Id, Vector3.Zero);
        world.TrySetEntityPosition(curious.Id, Vector3.Zero);
        var goals = new[] { new Vector3(1, 0, 0), new Vector3(3, 0, 0), new Vector3(9, 0, 0) };
        var navigators = world.Entities.ToDictionary(
            entity => entity.Id,
            _ => new FakeNavigationAdapter(),
            StringComparer.Ordinal);
        var service = new WanderingService(world, entity => navigators[entity.Id], goals, decisionSeed: 42);
        var cautiousResult = service.Advance(cautious, species);
        var curiousResult = service.Advance(curious, species);
        world.TrySetEntityPosition(cautious.Id, new Vector3(10, 0, 0));
        world.TrySetEntityPosition(curious.Id, new Vector3(10, 0, 0));
        var socialResult = service.Advance(social, species);

        Assert.Equal(WildBehavior.CautiousRoaming, cautiousResult.Behavior);
        Assert.Equal(WildBehavior.CuriousExploration, curiousResult.Behavior);
        Assert.Equal(WildBehavior.SocialRoaming, socialResult.Behavior);
        Assert.Equal(goals[2], curiousResult.Target);
        Assert.Equal(goals[2], socialResult.Target);
    }
}
