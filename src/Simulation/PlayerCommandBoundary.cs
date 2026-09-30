using System.Numerics;

namespace Simulation;

public enum PlayerCommandKind { Pause, Resume, SetSpeed, SelectEntity, Move }
public enum PlayerCommandResultCode { Accepted, DeferredWhilePaused, InvalidCommand, InvalidEntity, InvalidSpeed }

public sealed record PlayerCommand(
    PlayerCommandKind Kind,
    string? EntityId = null,
    SimulationSpeed? Speed = null,
    Vector3? Target = null);

public sealed record PlayerCommandResult(PlayerCommandResultCode Code, string? EntityId = null, Vector3? Target = null, string? Reason = null);

/// <summary>Domain-only command boundary shared by player UI and future AI callers.</summary>
public sealed class PlayerCommandBoundary
{
    private readonly World _world;
    private readonly SimulationClock _clock;

    public PlayerCommandBoundary(World world, SimulationClock clock)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string? SelectedEntityId { get; private set; }

    public PlayerCommandResult Execute(PlayerCommand? command)
    {
        if (command is null || !Enum.IsDefined(command.Kind)) return new(PlayerCommandResultCode.InvalidCommand, Reason: "Unknown command.");
        switch (command.Kind)
        {
            case PlayerCommandKind.Pause:
                _clock.SetPaused(true);
                return new(PlayerCommandResultCode.Accepted);
            case PlayerCommandKind.Resume:
                _clock.SetPaused(false);
                return new(PlayerCommandResultCode.Accepted);
            case PlayerCommandKind.SetSpeed:
                if (command.Speed is null) return new(PlayerCommandResultCode.InvalidSpeed, Reason: "A simulation speed is required.");
                try { _clock.SetSpeed(command.Speed.Value); return new(PlayerCommandResultCode.Accepted); }
                catch (ArgumentOutOfRangeException) { return new(PlayerCommandResultCode.InvalidSpeed, Reason: "Only supported simulation speeds are accepted."); }
            case PlayerCommandKind.SelectEntity:
                if (!_world.TryGetEntity(command.EntityId, out _)) return new(PlayerCommandResultCode.InvalidEntity, command.EntityId, Reason: "Entity does not exist.");
                SelectedEntityId = command.EntityId;
                return new(PlayerCommandResultCode.Accepted, command.EntityId);
            case PlayerCommandKind.Move:
                if (!_world.TryGetEntity(command.EntityId, out _)) return new(PlayerCommandResultCode.InvalidEntity, command.EntityId, Reason: "Entity does not exist.");
                if (command.Target is null || !IsFinite(command.Target.Value)) return new(PlayerCommandResultCode.InvalidCommand, command.EntityId, Reason: "A finite movement target is required.");
                if (_clock.IsPaused) return new(PlayerCommandResultCode.DeferredWhilePaused, command.EntityId, command.Target, "Movement resumes when simulation is running.");
                return new(PlayerCommandResultCode.Accepted, command.EntityId, command.Target);
            default:
                return new(PlayerCommandResultCode.InvalidCommand, Reason: "Unknown command.");
        }
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
