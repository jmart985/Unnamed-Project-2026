using System;
using System.IO;
using System.Linq;
using Simulation;
using UnityEngine;

// First migration gate: the shared World runs in Unity. Navigation/action hosting
// is intentionally a later slice; these markers are read-only domain views.
public sealed class SimulationFoundation : MonoBehaviour
{
    private SpeciesCatalog catalog;
    private World world;
    private readonly SimulationClock clock = new SimulationClock();
    private readonly SimulationSaveBoundary saves = new SimulationSaveBoundary();
    private string snapshot;
    private string status = "Shared simulation running";

    private void Awake()
    {
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--simulation-smoke"))
            {
                SimulationCompatibilitySmoke.Run();
                Debug.Log("UNITY SIMULATION PLAYER SMOKE PASSED");
                Application.Quit(0);
                enabled = false;
                return;
            }
            catalog = SpeciesCatalog.LoadFromFile(Path.Combine(Application.streamingAssetsPath, "Simulation/species.json"));
            world = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Unity foundation"), 42).World;
            foreach (var entity in world.Entities)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = entity.Id + " - " + entity.SpeciesId;
                marker.transform.SetParent(transform);
                marker.transform.position = new Vector3(entity.Position.X, entity.Position.Y + 1, entity.Position.Z);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            status = error.Message;
            enabled = false;
            if (Environment.GetCommandLineArgs().Contains("--simulation-smoke")) Application.Quit(1);
        }
    }

    private void Update()
    {
        clock.Advance(Time.unscaledDeltaTime, seconds =>
        {
            foreach (var entity in world.Entities)
            {
                entity.Health.AdvanceTime(seconds);
                entity.AdvanceNeeds(seconds, entity.SleepState == SleepState.Sleeping);
            }
        });
    }

    private void OnGUI()
    {
        if (world == null) return;
        GUILayout.BeginArea(new Rect(20, 20, 450, 310), GUI.skin.box);
        GUILayout.Label("UNITY SIMULATION FOUNDATION");
        GUILayout.Label("World, clock, needs and snapshot compatibility");
        GUILayout.Label("Movement and gameplay actions are the next migration slice.");
        GUILayout.Label($"Time: {clock.SimulationTime:F1}s | Entities: {world.Entities.Count}");
        var player = world.Entities.First(entity => entity.Id == world.PlayerEntityId);
        GUILayout.Label($"Player satiety: {player.Needs.Satiety:F6}");
        if (GUILayout.Button(clock.IsPaused ? "Resume" : "Pause")) clock.SetPaused(!clock.IsPaused);
        if (GUILayout.Button("Save checkpoint"))
        {
            clock.SetPaused(true);
            snapshot = saves.Save(world, clock).Snapshot;
            status = "Checkpoint saved in memory; simulation paused.";
        }
        GUI.enabled = snapshot != null;
        if (GUILayout.Button("Restore checkpoint"))
        {
            clock.SetPaused(true);
            var result = saves.Load(world, clock, snapshot, catalog);
            status = "Restore: " + result.Status;
        }
        GUI.enabled = true;
        GUILayout.Label(status);
        GUILayout.EndArea();
    }
}
