# 2.0 predator/prey encounter validation

## Playable target

In a fresh local region, the starter cow detects the configured wolf before it reaches attack distance and navigates away. The wolf still follows the cow, kills it through the existing combat path, creates one carcass, and eats through the shared food system. If the threat leaves detection range, prey behavior returns to its normal needs and territory rules.

## Automated checks

`WildPreyThreatServiceTests` cover deterministic nearest-threat selection and ties, inclusive range boundaries, configured prey relationships, friendly/dead/incapacitated/unrelated exclusions, action priority, transient cleanup when a predator leaves range, bounded route-failure retry, switching the active threat without repeated route requests, and resetting a failed-escape retry through world load. `SpeciesCatalogTests` cover the nonnegative detection-range data contract, legacy zero default, and invalid negative values.

Run the Godot integration scenario:

```powershell
dotnet build game\UnnamedProject2026.csproj --configuration Debug
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-wild-prey-threat
```

Require exit code 0 and `WILD PREY THREAT VALIDATION PASSED`. The scenario positions the wolf and cow in open ground, checks the cow's escape action and away-facing destination, observes the cow move away, then waits for the wolf to complete the existing hunt and feeding loop.

Also run `--validate-wolf-hunting` and `--validate-wild-territories` to guard the existing 1.2 behavior.

## Manual play check

1. Start a fresh game and watch the wolf approach the cow in the countryside.
2. Confirm the cow turns and moves away while the wolf follows, then verify the wolf can still complete the hunt and eat from the resulting carcass.
3. When the predator is no longer within threat-detection range, confirm the cow is no longer stuck in a flee action and resumes its ordinary needs and territory behavior.

The cow's 18 m threat-detection range is provisional tuning. Detection is species-pair based and geometric in this slice; line of sight, terrain, pack/herd behavior, and ecological population changes are deferred.

## Far-population transitions and save/load

Far population records belong to `World` and are keyed by region and species. A transition for one species must not remove, merge, or overwrite another species in the same region. Schema 19 saves persist count, average satiety, and resource quantities; older saves restore with no far-population records.

Run the Godot integration check after a Debug build:

```powershell
dotnet build game\UnnamedProject2026.csproj --configuration Debug
& "C:\Users\James Home\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path game --headless -- --validate-far-population-transitions
```

Require exit code 0 and `FAR POPULATION TRANSITIONS VALIDATION PASSED`. The scenario places cow and wolf aggregates in one region, promotes and demotes only cows, then verifies both species and their resources survive save/load without duplication. Population growth, mortality, respawning, and migration are not covered by this scenario.

## Carcass freshness

Harvested carcasses have a species-configurable freshness lifetime, currently 300 simulation seconds. The timer advances only when the simulation advances, so pause stops decay and 4x speed advances it four times faster. Generic food sources do not expire. If a carcass expires while an entity is traveling to it, the existing food-consumption flow releases its reservation and selects another valid source or waits. Remaining freshness is saved; schema 18 and earlier saves migrate with existing food sources treated as nonperishable to avoid deleting old resources.

Run the Godot integration check after a Debug build:

```powershell
dotnet build game\UnnamedProject2026.csproj --configuration Debug
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-carcass-decay
```

Require exit code 0 and `CARCASS DECAY VALIDATION PASSED`. Also run `--validate-wolf-hunting` to verify the hunt, carcass creation, and feeding loop still work.
