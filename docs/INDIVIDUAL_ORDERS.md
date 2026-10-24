# Individual Follow and Stay orders

Issue #487 adds direct orders for one NPC or creature at a time. These orders are
separate from squad-wide orders and use the same authoritative simulation and
navigation path as other movement.

## Player controls

- Select an eligible entity to show **Follow** and **Stay** in its inspector.
- Open **Settlement**, then right-click an entity in the people/squad list to open
  the individual-order menu.
- **Follow** tracks the conscious actor who issued the command. The ordered
  entity stops within 2 m and resumes following when farther than 3 m.
- **Stay** records the ordered entity's current position and returns there if it
  is displaced.
- World right-click remains available for its existing targeting/combat behavior;
  the individual-order context menu is opened from a roster row.

The issuing actor must pass the existing authority checks. The player entity,
dead or incapacitated targets, self-targeting, unsupported commands, and species
without movement capability are rejected without replacing the current order.
Mobile creatures can receive Follow and Stay when authority permits. Attack is
not part of this command set.

## Behavior and persistence

Order intent belongs to the World entity. Follow stores the issuer's stable entity
ID; Stay stores a finite world position. The simulation owns the intent while
`OrderService` creates and updates navigation requests through the existing
navigation adapter. The saved state contains no live route or engine node.

Urgent hunger, fleeing, or sleep behavior may interrupt movement. The order
remains saved and is issued again after the urgent behavior ends. Removing an
entity clears its order. On load, routes are rebuilt; an order to Follow a missing
or dead issuer is discarded, while a Stay order remains valid. Snapshots before
schema 22 load with no individual orders. Ordered entities remain individual
World entities during near/far transitions so their intent cannot be lost in a
regional population aggregate.

## Validation

Run the focused domain tests and actual Godot UI/navigation scenario:

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~IndividualOrderTests|FullyQualifiedName~NearFarTransitionServiceTests"
dotnet build game\UnnamedProject2026.csproj --configuration Debug --no-restore
godot --path game --headless -- --validate-individual-orders
```

Use the project's pinned Godot 4.7.2 .NET executable if `godot` is not on `PATH`.

The scenario invokes the inspector and roster controls, follows a non-player
issuer using live navigation, applies Stay from the roster menu, displaces and
returns the ordered entity, then saves and restores the Stay intent and resumes
navigation. Success logs `INDIVIDUAL ORDER VALIDATION PASSED`; failure exits 1.
