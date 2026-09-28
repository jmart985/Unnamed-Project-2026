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
