# Godot interaction-anchor adapter

`SemanticAnchorResolver` in `game/scripts/SemanticAnchorResolver.cs` resolves a species-declared semantic ID to a model-scene `Node3D` named with that ID. The resolver checks the requested declaration collection, reports missing or invalid locators explicitly, rejects duplicate IDs, and returns the locator's global transform. It never falls back to the model origin.

`EntityView.ResolveAnchor` is the presentation entry point. Locators may be parented under imported bones or scene nodes, but callers use only semantic IDs. The returned transforms are transient presentation data and are not written to the World or save files.

The current human and creature GLBs do not yet contain semantic locator nodes. Their declared anchors therefore resolve as unavailable until the model assets are updated. This adapter does not enable interactions, infer anatomy from imported bones, or determine whether an action is permitted. Body-variant and species-specific availability must be checked separately.

## Godot validation

Run:

```powershell
dotnet build game/UnnamedProject2026.csproj --configuration Debug --no-restore
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-interaction-anchors
```

The scenario verifies a declared anchor's world transform through translated parents and checks undeclared categories, missing declared locators, non-`Node3D` nodes, and duplicate semantic IDs.
