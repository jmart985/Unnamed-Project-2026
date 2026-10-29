using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunEntityPresentationValidation() => EntityPresentationValidation.Run(this);
}

internal static class EntityPresentationValidation
{
    public static async void Run(Main main)
    {
        EntityView? fallbackView = null;
        try
        {
            var catalog = main.GetEntityPresentationCatalog();
            var cases = new[]
            {
                (Id: "human", IsCreature: false, BodyCategory: "humanoid"),
                (Id: "creature", IsCreature: true, BodyCategory: "quadruped"),
                (Id: "ape", IsCreature: true, BodyCategory: "primate"),
                (Id: "cow", IsCreature: true, BodyCategory: "ungulate"),
                (Id: "wolf", IsCreature: true, BodyCategory: "canid"),
                (Id: "fox", IsCreature: true, BodyCategory: "quadruped"),
                (Id: "cat", IsCreature: true, BodyCategory: "quadruped"),
                (Id: "pig", IsCreature: true, BodyCategory: "quadruped")
            };

            foreach (var entry in cases)
            {
                var presentation = catalog.Resolve(entry.Id, entry.IsCreature, entry.BodyCategory);
                Require(presentation.SpeciesId == entry.Id, $"{entry.Id} resolves to its own presentation definition");
                Require(presentation.FallbackCollisionHeight > 0 && presentation.FallbackCollisionRadius > 0,
                    $"{entry.Id} defines positive fallback collision dimensions");

                if (entry.Id == "creature")
                    continue;

                if (entry.Id == "human")
                {
                    RequireLoads(presentation.GetModelPath(BiologicalSex.Male), "male human model");
                    RequireLoads(presentation.GetModelPath(BiologicalSex.Female), "female human model");
                }
                else
                {
                    RequireLoads(presentation.GetModelPath(BiologicalSex.Unspecified), $"{entry.Id} model");
                }
            }

            var fox = catalog.Resolve("fox", true, "quadruped");
            var vulpine = catalog.Resolve("vulpine", true, "quadruped");
            Require(fox.GetModelPath(BiologicalSex.Unspecified) == vulpine.GetModelPath(BiologicalSex.Unspecified),
                "vulpine alias resolves to the fox presentation");

            var unknown = catalog.Resolve("unregistered-species", true, "canid");
            Require(unknown.GetModelPath(BiologicalSex.Unspecified) is null
                && unknown.FallbackCollisionHeight > 0 && unknown.FallbackCollisionRadius > 0,
                "unknown species receives a usable generic presentation fallback");

            var invalidCatalogRejected = false;
            try
            {
                EntityPresentationCatalog.LoadFromJson("""
                    {"species":[{"id":"invalid_dimensions","fallbackCollisionHeight":0,
                    "fallbackCollisionRadius":1,"proceduralBodySize":[1,1,1],"bodyColor":[0,0,0],
                    "accentColor":[0,0,0],"lowerBodyColor":[0,0,0],"locomotionFamily":"quadruped"}]}
                    """);
            }
            catch (InvalidDataException)
            {
                invalidCatalogRejected = true;
            }
            Require(invalidCatalogRejected, "catalog rejects invalid collision dimensions");

            var missingModelCatalog = EntityPresentationCatalog.LoadFromJson("""
                {"species":[{"id":"missing_model_test","modelPath":"res://assets/missing/presentation-validation.glb",
                "displayName":"MissingModelTest","modelHeight":1.5,"modelCollisionRadius":0.3,
                "bodyColor":[0.5,0.4,0.3],"accentColor":[0.2,0.2,0.2],"lowerBodyColor":[0.2,0.25,0.3],
                "proceduralBodySize":[0.7,0.6,1.0],"fallbackCollisionHeight":1.2,"fallbackCollisionRadius":0.35,
                "locomotionFamily":"quadruped"}]}
                """);
            fallbackView = new EntityView { Name = "MissingPresentationFallbackValidation" };
            fallbackView.Bind("missing-presentation-validation", 2f, isCreature: true,
                speciesId: "missing_model_test", bodyCategory: "quadruped", presentationCatalog: missingModelCatalog);
            main.AddChild(fallbackView);
            Require(fallbackView.PresentationModelAssetPath is null,
                "missing model import keeps the procedural fallback active");
            Require(fallbackView.GetChildren().OfType<CollisionShape3D>().Any(),
                "missing model fallback creates collision using catalog dimensions");
            Require(fallbackView.FindChildren("*", "MeshInstance3D", recursive: true, owned: false).Count > 0,
                "missing model fallback still creates a visible procedural body");

            fallbackView.QueueFree();
            fallbackView = null;
            await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("SPECIES PRESENTATION VALIDATION PASSED: catalog entries, model imports, aliases, unknown fallback, and missing-model fallback.");
            main.GetTree().Quit();
        }
        catch (Exception exception)
        {
            fallbackView?.QueueFree();
            await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.PushError($"SPECIES PRESENTATION VALIDATION FAILED: {exception}");
            main.GetTree().Quit(1);
        }
    }

    private static void RequireLoads(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || GD.Load<PackedScene>(path) is null)
            throw new InvalidOperationException($"The catalog's {description} path '{path ?? "<missing>"}' did not load.");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException($"Species presentation validation failed: {description}.");
    }
}
