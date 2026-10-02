using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class IndividualTemperamentTests
{
    [Fact]
    public void TemperamentVariesByStableEntityIdWithinTraitBounds()
    {
        var species = new TemperamentProfile(0.5, 0.4, 0.6);

        var first = IndividualTemperament.ForEntity("entity-000001", species);
        var repeated = IndividualTemperament.ForEntity("entity-000001", species);
        var second = IndividualTemperament.ForEntity("entity-000002", species);

        Assert.Equal(first, repeated);
        Assert.NotEqual(first, second);
        Assert.All(
            new[] { first.Boldness, first.Curiosity, first.Sociability, second.Boldness, second.Curiosity, second.Sociability },
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

    [Fact]
    public void NewGameCreaturesExpressDifferentTemperamentDrivenWanderingStyles()
    {
        var catalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Test Player"));
        var creatures = session.World.Entities
            .Where(entity => entity.SpeciesId == NewGameSession.CreatureSpeciesId)
            .ToArray();
        var navigators = creatures.ToDictionary(
            entity => entity.Id,
            _ => new FakeNavigationAdapter(),
            StringComparer.Ordinal);
        var service = new WanderingService(
            session.World,
            entity => navigators[entity.Id],
            [new Vector3(70, 0, 50), new Vector3(110, 0, 80), new Vector3(150, 0, 40)],
            decisionSeed: 42);

        var behaviors = creatures
            .Select(entity => service.Advance(entity, catalog.GetRequired(entity.SpeciesId)).Behavior)
            .ToArray();

        Assert.Contains(WildBehavior.CautiousRoaming, behaviors);
        Assert.Contains(WildBehavior.CuriousExploration, behaviors);
        Assert.Contains(WildBehavior.SocialRoaming, behaviors);
    }
}
