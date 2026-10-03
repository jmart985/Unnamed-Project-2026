# Milestone 0.8: Economy Prototype validation

## E08d fixed-input production and market scenario

`EconomyScenarioTests.ProductionToCaravanTradeIsRepeatableAndRecoversWithoutResourceLoss`
composes the existing farm, work-order queue, workstation recipe execution, World
stockpiles, atomic transfers, trade-good catalog, caravan visits, and snapshots.
It runs the complete operation order twice and compares the production result,
completed order, transaction result, visit state, serialized World, and external
cargo totals.

One human at `(0, 0, 0)` claims `meal-order`. A one-slot wheat plot requires five
growth units, advanced explicitly by two then three, and yields three food.
Premature and duplicate harvests return zero. The scenario deposits that finite
yield into the existing input container, which initially holds two wood. The
`meal` recipe requires two work units and transforms three food plus two wood
into six food. Incomplete execution preserves every World resource. A completed
execution consumes all inputs, completes the order, and transfers the six food
from output storage to the market. The queue then has no available order.

These are explicit domain operations, not autonomous workers or a new production
mechanic. The existing farm boundary returns a yield which the scenario deposits
after checking capacity. The existing work-order target identifies the recipe;
the harness explicitly advances its work and executes it.

| Stage | Input food / wood | Output food | Market food / coin | Caravan food / coin |
| --- | --- | --- | --- | --- |
| Initial | 0 / 2 | 0 | 0 / 10 | 1 / 12 |
| Harvest | 3 / 2 | 0 | 0 / 10 | 1 / 12 |
| Recipe complete | 0 / 0 | 6 | 0 / 10 | 1 / 12 |
| Transfer to market | 0 / 0 | 0 | 6 / 10 | 1 / 12 |
| Caravan buys 3 food | 0 / 0 | 0 | 3 / 16 | 4 / 6 |
| Departure and settlement restore | 0 / 0 | 0 | 3 / 16 | 4 / 6 (external) |

Input and output storage each have capacity 10; the market has capacity 40.
Caravan cargo has capacity 13 and opens `fixed-visit` before explicit arrival.
The repository catalog prices food at two coin, so the successful purchase moves
three food and six coin in opposite directions. After production there are seven
food and 22 coin across settlement and caravan; trade and departure conserve
both totals. Production deliberately consumes two wood and transforms three
food into six; it is accounted separately from resource-conserving transfers.

## Rejection and recovery evidence

- Loading a replacement catalog containing a valid food entry followed by a
  forbidden coin entry throws `InvalidDataException`. The live catalog remains
  unchanged (food still costs two coin), as do all World and cargo totals.
- Buying seven food rejects with `InsufficientMarketGoods`; a zero quantity
  rejects with `InvalidQuantity`; attempting to buy coin rejects with
  `NotTradeable`. The same arrived visit then successfully buys three food.
- Selling four food after that purchase rejects with
  `CounterpartyCapacityExceeded`: cargo would contain 14 coin in capacity 13.
  Selling five food rejects with `InsufficientCounterpartyGoods`. Each rejection
  preserves the complete serialized World and cargo counts/capacity, and keeps
  the visit Arrived.
- Departure succeeds once. Repeat departure and trade after departure return
  `AlreadyDeparted` without mutation.
- After departure, serialize at simulation time 12.5. A snapshot with negative
  market coin is rejected before any transient reset or World replacement;
  original entity/container references and all counts remain unchanged.
- Loading the valid snapshot with the caravan service as transient state restores
  time 12.5 and identical durable World data. The departed visit remains closed,
  and its external cargo is unchanged; it cannot trade again after restoration.

Existing catalog, market, and caravan unit tests additionally cover malformed
data, unknown/duplicate goods, currency shortages, market capacity, stock
exhaustion, cancellation, stale visits, and removal recovery.

## Executed validation (2026-10-03)

Windows; .NET SDK `10.0.401`; Godot
`4.7.2.stable.mono.official.ed1daf0bf` (verified with `--version`).

| Exact command | Actual result |
| --- | --- |
| `dotnet restore UnnamedProject2026.slnx --verbosity minimal` | Passed; all three projects restored. |
| `dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~EconomyScenarioTests --verbosity minimal` | Passed: 1 test, 0 failed, 0 skipped. The test executes the scenario twice. |
| `dotnet build UnnamedProject2026.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 errors, 2 pre-existing CS8604 warnings in `game/scripts/EntityView.cs` at lines 126 and 178. |
| `dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-build --verbosity minimal` | Passed: 496 tests, 0 failed, 0 skipped. Includes production, market, caravan, snapshot, and Ant Farm regressions. |
| `dotnet build game/UnnamedProject2026.csproj --configuration Debug --no-restore --verbosity minimal` | Passed: 0 errors, the same 2 warnings. Builds the assembly used by the editor/headless launch. |
| `& 'C:/Developement/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe' --path game --headless --quit` | Passed after the Debug build with normal user-directory access: exit 0 and no engine errors. Startup smoke check only. |

The first headless attempt after only the Release build exited 0 but reported
missing C# classes and sandbox log/certificate errors; that attempt was not a
passing check. Building Debug and running with normal user-directory access
resolved those setup errors. The first focused test attempt also failed because
the fixture addressed the wrong snapshot property; it was corrected to
`Contents.Counts`, and the focused and full suites above passed afterward.

## Limits

No player-facing economy UI or economy-specific Godot scene exists, so the
economy scenario runs through the domain integration test. The headless launch
does not prove visible gameplay or autonomous farming. No windowed scenario was
run. Farms, recipe progress, and work orders are existing transient domain
objects; only settlement containers/entities are restored. External caravan
cargo is intentionally absent from World snapshots. No save schema, compatibility,
runtime behavior, or tuning values change in this validation issue.

The repository defines no separate formatting/lint command. `git diff --check`
is used for whitespace verification; the build and full tests are the documented
CI checks.
