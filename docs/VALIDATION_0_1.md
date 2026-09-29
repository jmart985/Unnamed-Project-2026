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

## F03b3: Final-unit food contention and resource accounting

**Date:** 2026-09-28  
**Branch:** `43-f03b3-verify-final-unit-food-contention-and-resource-accounting`

### Command

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj `
  --configuration Release --no-restore `
  --filter "FullyQualifiedName~FoodSelectionServiceTests|FullyQualifiedName~FoodConsumptionServiceTests" `
  /p:UseSharedCompilation=false
```

### Result

The focused run passed **29 tests**, with 0 failures and 0 skips.

The scenarios covered:

- both claimant orders for two hungry entities competing for the final food unit leave exactly one successful consumption;
- the failed claimant exposes `No available food.`, has no active action, honors the bounded retry delay, and remains recoverable;
- unreachable, depleted, and removed targets recover through alternate selection or bounded waiting;
- no-food retries stay deferred for the documented delay and recover after replenishment;
- consumed-food counters reconcile with remaining world food after successful contention and multi-source consumption. The final-unit runs each ended with 0 food remaining, 1 food consumed, and the invariant `initial = remaining + consumed` satisfied.

This is deterministic domain-level evidence. It does not replace the full Godot 600-second milestone run or prove simultaneous multithreaded execution; the Milestone 0.1 simulation advances entities sequentially on one fixed tick loop.

## F03b2: Full-storage and removed-target haul recovery

**Date:** 2026-09-28  
**Branch:** `issue-42-haul-recovery-validation`

### Command

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj `
  --configuration Release --no-restore `
  --filter "FullyQualifiedName~WoodHaulingServiceTests" `
  /p:UseSharedCompilation=false
```

### Result

The focused run passed **12 tests**, with 0 failures and 0 skips.

The focused failure scenarios produced these outcomes:

- A stockpile filled after pickup rejected the deposit with `Wood deposit failed: CapacityExceeded`. The hauler entered the visible `Waiting` activity, retained all 2 carried wood, released the stale capacity reservation, honored the 10-tick retry delay, and delivered after capacity was freed.
- Removing a source before pickup produced `The wood source was removed.`, released both reservations, preserved the source's 2 wood without creating cargo, and selected the alternate source after the retry delay. The alternate trip ended with 2 wood in storage and all 4 initial wood accounted for across the removed source, removed alternate source, inventory, and storage.
- Removing a stockpile before pickup produced `The stockpile target was removed.`, released the source reservation, and left the source's 2 wood unchanged. Removing the replacement stockpile after pickup produced the same visible waiting reason while retaining all 2 carried wood. After a new stockpile was added, the hauler resumed delivery and ended with 0 carried wood and 2 stored wood.

Reservation release was checked by immediately acquiring the released resource or capacity with a competing action. Every waiting result was inspectable through both the returned reason and the entity's `Waiting` activity and waiting reason.

These are deterministic domain-level failure scenarios using the simulation's fixed-tick service. They do not replace the full 600-second Godot milestone run or exercise engine-node removal during a rendered frame.
