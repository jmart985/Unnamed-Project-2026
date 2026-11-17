using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Simulation;
using UnityEngine;

public static class SimulationCompatibilitySmoke
{
    public static void Run()
    {
        var target = (TargetFrameworkAttribute)Attribute.GetCustomAttribute(typeof(World).Assembly, typeof(TargetFrameworkAttribute));
        Require(target.FrameworkName == ".NETStandard,Version=v2.1", "portable assembly target");
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(Application.streamingAssetsPath, "Simulation/species.json"));
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Compatibility proof"), 42);
        var world = session.World;
        Require(world.Entities.Count == 14, "full starter population");
        var clock = new SimulationClock();
        var initialSatiety = session.Player.Needs.Satiety;
        for (var i = 0; i < 100; i++)
            clock.Advance(0.1, seconds => { foreach (var entity in world.Entities) entity.AdvanceNeeds(seconds); });
        Require(session.Player.Needs.Satiety < initialSatiety, "needs advance");
        clock.SetPaused(true);
        var savedTime = clock.SimulationTime;
        Require(clock.Advance(1) == 0 && clock.SimulationTime == savedTime, "pause freezes clock");
        var boundary = new SimulationSaveBoundary();
        var saved = boundary.Save(world, clock);
        Require(saved.Status == SaveBoundaryStatus.Accepted, "snapshot saved");
        var restored = new World();
        Require(boundary.Load(restored, clock, saved.Snapshot, catalog).Status == SaveBoundaryStatus.Accepted, "snapshot restored");
        Require(WorldSnapshotSerializer.Serialize(restored, clock.SimulationTime) == saved.Snapshot, "round-trip durable state");
        Require(boundary.Load(restored, clock, "{}", catalog).Status == SaveBoundaryStatus.InvalidSnapshot, "invalid snapshot rejected");
        Require(WorldSnapshotSerializer.Serialize(restored, clock.SimulationTime) == saved.Snapshot, "invalid load leaves state intact");

        var path = Path.Combine(Application.temporaryCachePath, "simulation-smoke-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SaveSlotStore();
            store.Save(path, () => saved.Snapshot);
            store.Save(path, () => saved.Snapshot + "\n");
            Require(store.Load(path) == saved.Snapshot + "\n", "atomic replacement on Unity runtime");
            try { store.Save(path, () => throw new InvalidDataException("injected serialization failure")); }
            catch (SaveWriteException) { }
            Require(store.Load(path) == saved.Snapshot + "\n", "failed write preserves previous save");
            Require(!File.Exists(path + ".tmp"), "temporary save cleaned");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Debug.Log("UNITY SIMULATION COMPATIBILITY PASSED: target, 14 entities, needs, pause, snapshot, invalid-load rollback, file replacement");
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException("Compatibility check failed: " + check);
    }
}
