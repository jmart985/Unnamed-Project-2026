# Startup and controls regression repair (2026-10-04)

Base: origin/main bf67dec. User-requested repair after startup exceptions.

- Copy trade_goods.json into game build output using the existing data-file convention.
- Settings version 3 retains version 1/2 desktop/controller bindings and adds missing actions with unused keys. Migration does not rewrite files until a user saves a mapping. Incomplete settings retain defaults and report the failure.
- Needs regression fixtures explicitly supply their intended satiety drain and use named satiety overrides; production tuning is unchanged.
- Combat in catalog-free Worlds still applies damage without throwing from the optional temperament response.

Actual validation: Release solution build and Debug game build passed. Domain tests: 548 passed, 0 failed/skipped. Godot 4.7.2 headless startup, menus and selection passed. Selection checks version 1/2 migrations, conflicting G/T/F9 bindings, invalid current settings, source-file preservation and dispatched G greeting.

Remaining: gameplay-actions validation fails at its historical F-to-cow greeting assertion. Current main changed F to context-sensitive interaction and G to human greeting; this repair does not redefine those interaction rules or weaken that broader scenario. Nullable warnings and unavailable NuGet vulnerability audit (NU1900) remain. Rendered validation was not run.

GitHub connector could not create the tracking issue (404); the user request defines this correction scope.
