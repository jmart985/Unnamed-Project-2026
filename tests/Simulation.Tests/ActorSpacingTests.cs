using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class ActorSpacingTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static NewGameSession Session(int seed) => NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Spacing Tester"), seed);

    [Fact]
    public void MixedBodyActorsCannotMoveIntoEachOther()
    {
        var session = Session(4);
        var human = session.World.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != session.PlayerEntityId);
        var cow = session.World.Entities.First(entity => entity.SpeciesId == "cow");
        foreach (var entity in session.World.Entities.Where(entity => entity.Id != human.Id && entity.Id != cow.Id)) session.World.TrySetEntityPosition(entity.Id, new Vector3(100, 0, 100));
        session.World.TrySetEntityPosition(human.Id, Vector3.Zero);
        session.World.TrySetEntityPosition(cow.Id, new Vector3(3, 0, 0));

        Assert.False(session.World.TryMoveEntityPosition(cow.Id, new Vector3(0.5f, 0, 0)));
        Assert.Equal(new Vector3(3, 0, 0), cow.Position);
        Assert.True(session.World.TryMoveEntityPosition(cow.Id, new Vector3(2, 0, 0)));
    }

    [Fact]
    public void DeadActorsDoNotBlockMovement()
    {
        var session = Session(5);
        var human = session.World.Entities.First(entity => entity.SpeciesId == "human" && entity.Id != session.PlayerEntityId);
        var cow = session.World.Entities.First(entity => entity.SpeciesId == "cow");
        foreach (var entity in session.World.Entities.Where(entity => entity.Id != human.Id && entity.Id != cow.Id)) session.World.TrySetEntityPosition(entity.Id, new Vector3(100, 0, 100));
        session.World.TrySetEntityPosition(human.Id, Vector3.Zero);
        session.World.TrySetEntityPosition(cow.Id, new Vector3(3, 0, 0));
        human.Health.TryDamage("Torso", 1_000);

        Assert.True(session.World.TryMoveEntityPosition(cow.Id, new Vector3(0.5f, 0, 0)));
    }
}
