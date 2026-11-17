using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SimulationFoundationSetup
{
    public static void ValidateAndBuild()
    {
        SimulationCompatibilitySmoke.Run();
        const string scenePath = "Assets/Scenes/SimulationFoundation.unity";
        if (!File.Exists(scenePath)) CreateFoundationScene(scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        PlayerSettings.companyName = "Unnamed Project";
        PlayerSettings.productName = "Simulation Foundation";
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "Builds/Windows/SimulationFoundation.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Unity player build failed: " + report.summary.result);
        Debug.Log("UNITY SIMULATION FOUNDATION BUILD PASSED");
    }

    private static void CreateFoundationScene(string scenePath)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Shared simulation").AddComponent<SimulationFoundation>();
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 40, -35);
        camera.transform.LookAt(Vector3.zero);
        camera.backgroundColor = new Color(0.13f, 0.19f, 0.24f);
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Foundation ground";
        ground.transform.localScale = new Vector3(40, 1, 40);
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
    }
}
