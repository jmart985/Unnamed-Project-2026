namespace Simulation;

public sealed record WorldEvent(string Id, long ScheduledTick, string Type, string Payload);

public sealed class WorldEventQueue
{
    private readonly List<WorldEvent> _events = [];

    public int Count => _events.Count;

    public void Enqueue(WorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        if (string.IsNullOrWhiteSpace(worldEvent.Id) || worldEvent.ScheduledTick < 0 || string.IsNullOrWhiteSpace(worldEvent.Type))
        {
            throw new ArgumentException("World events require an ID, nonnegative scheduled tick, and type.", nameof(worldEvent));
        }

        if (_events.Any(existing => existing.Id == worldEvent.Id))
        {
            throw new InvalidOperationException($"World event ID '{worldEvent.Id}' is already queued.");
        }

        _events.Add(worldEvent);
    }

    public IReadOnlyList<WorldEvent> ProcessDue(long simulationTick, Action<WorldEvent> process)
    {
        ArgumentNullException.ThrowIfNull(process);
        var due = _events
            .Where(worldEvent => worldEvent.ScheduledTick <= simulationTick)
            .OrderBy(worldEvent => worldEvent.ScheduledTick)
            .ThenBy(worldEvent => worldEvent.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var worldEvent in due)
        {
            process(worldEvent);
            _events.Remove(worldEvent);
        }

        return due;
    }
}
