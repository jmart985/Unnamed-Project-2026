# Milestone 0.1 Validation Record

## F03b1: Food depletion and blocked-food recovery

**Date:** 2026-09-27  
**Branch:** `issue-41-food-recovery-validation`

### Command

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj `
  --configuration Release --no-restore `
  --filter "FullyQualifiedName~FoodSelectionServiceTests|FullyQualifiedName~FoodConsumptionServiceTests" `
  /p:UseSharedCompilation=false
```

### Result

The focused run passed **26 tests**, with 0 failures and 0 skips.

The scenarios covered:

- depleted sources are skipped without consuming food remotely;
- unreachable sources release their reservations and select an alternate reachable source;
- removed sources release travel state and select an alternate source when available;
- no available or reachable food produces an inspectable waiting reason;
- retries are deferred for the documented bounded delay rather than evaluated every tick;
- replenished food is discovered after the retry delay.

Resource counts and entity needs remained unchanged on failed or deferred attempts. A full Godot 600-second milestone run is not recorded here; that broader scenario also requires the hauling and save/load issues to be complete.
