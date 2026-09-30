using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerCommandBoundaryTests
{
    [Fact]
    public void PauseResumeSpeedAndSelectionReturnAcceptedResults()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var clock = new SimulationClock();
        var commands = new PlayerCommandBoundary(world, clock);

        Assert.Equal(PlayerCommandResultCode.Accepted, commands.Execute(new(PlayerCommandKind.Pause)).Code);
        Assert.True(clock.IsPaused);
        Assert.Equal(PlayerCommandResultCode.Accepted, commands.Execute(new(PlayerCommandKind.Resume)).Code);
        Assert.Equal(PlayerCommandResultCode.Accepted, commands.Execute(new(PlayerCommandKind.SetSpeed, Speed: SimulationSpeed.FourX)).Code);
        Assert.Equal(PlayerCommandResultCode.Accepted, commands.Execute(new(PlayerCommandKind.SelectEntity, entity.Id)).Code);
        Assert.Equal(entity.Id, commands.SelectedEntityId);
    }

    [Fact]
    public void InvalidEntitiesAndPausedMovementProduceStableResults()
    {
        var world = new World();
        var clock = new SimulationClock();
        var commands = new PlayerCommandBoundary(world, clock);
        var missing = commands.Execute(new(PlayerCommandKind.SelectEntity, "missing"));
        Assert.Equal(PlayerCommandResultCode.InvalidEntity, missing.Code);

        var entity = world.SpawnEntity("human", Vector3.Zero);
        clock.SetPaused(true);
        var pausedMove = commands.Execute(new(PlayerCommandKind.Move, entity.Id, Target: Vector3.One));
        Assert.Equal(PlayerCommandResultCode.DeferredWhilePaused, pausedMove.Code);
        Assert.Equal("Movement resumes when simulation is running.", pausedMove.Reason);
    }

    [Fact]
    public void MalformedCommandsDoNotThrow()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var commands = new PlayerCommandBoundary(world, new SimulationClock());
        Assert.Equal(PlayerCommandResultCode.InvalidCommand, commands.Execute(null).Code);
        Assert.Equal(PlayerCommandResultCode.InvalidSpeed, commands.Execute(new(PlayerCommandKind.SetSpeed)).Code);
        Assert.Equal(PlayerCommandResultCode.InvalidCommand, commands.Execute(new(PlayerCommandKind.Move, entity.Id, Target: new(float.NaN, 0, 0))).Code);
    }
}
