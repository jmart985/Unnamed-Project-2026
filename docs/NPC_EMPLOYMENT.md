# Ongoing NPC employment

Issue #545 separates an NPC's ongoing workplace position from its current action. An unemployed settlement resident automatically fills a vacant compatible position. Completing a delivery, waiting for resources, resting, eating, fleeing or receiving an explicit movement order does not remove employment.

## Current playable role

The first supported role is `wood_hauler`, using the existing wood-hauling action, reservations, bounded navigation retries and stockpile capacity rules. Its capability requirement is `CanHaul`; there is no species-name or sex restriction. Only explicitly registered settlement workers participate. Players, visitors and wild creatures are not automatically enrolled. Registering a resident is an explicit World operation; merely spawning an entity or giving it a home does not enroll it during normal play.

New sessions declare ten positions at the starter stockpile, preserving the existing ten-resident hauling scenario. Residents fill those positions automatically during session initialization. Each later simulation tick reconciles removed/dead employees and workplace access, then fills vacancies. Worker IDs and position IDs provide a deterministic selection order. Existing employment, including manual assignments, is preserved. An employee's pickups reserve delivery capacity at its assigned stockpile; a nearer unrelated stockpile does not silently become its workplace.

Farming, production and construction already expose actor commands but do not yet have autonomous task executors in this employment slice. Their roles cannot be advertised as available jobs until those complete work loops are implemented. The existing standalone work-order queue is not a second employment owner.

## Management

Open Settlement > People and select a resident. The employment controls show ongoing employment separately from the existing activity, schedule and waiting-reason fields.

- Enable **Automatically find a job when unemployed** to allow automatic placement. Turning it off does not fire an employed worker.
- Choose a vacant position and **Assign selected NPC** for explicit assignment. A worker cannot occupy two positions or replace someone else.
- **Unassign and pause auto** returns the worker to unemployment and disables automatic placement so the next tick does not immediately undo the player's decision. Re-enable automatic placement or assign a position explicitly when desired.
- Select a stockpile and **Add wood-hauler position** to declare an additional finite opening. **Remove vacancy** removes an unoccupied position.

Temporary interruptions preserve the position. Death or entity removal releases it; removing the workplace removes its positions. Losing access to a faction-owned workplace releases employment immediately. None of these operations deletes carried resources. An interrupted carrier may complete a safe delivery even after losing its position; new pickup work requires employment. Hunger and off-shift schedule handling still run for unemployed NPCs.

## Ownership and persistence

World owns `SettlementWorkerSetting`, `WorkplacePosition` and the `UsesWorkplaceEmployment` mode. The UI only invokes World operations. Actions, paths, reservations and retry timers remain transient, owned by the existing services. No new movement system or event framework is introduced.

Schema 27 records the resident roster, each resident's automatic-assignment preference, positions and employee IDs. Validation rejects duplicate/missing workers, double employment, unknown roles/workplaces, incompatible species, unauthorized workplace access and missing employment lists before mutating the current world or resetting active services.

Schemas through 26 migrate recorded home residents into vacancies at the first saved stockpile; they start unemployed and fill openings on the next simulation tick. Player appearance and existing equipment remain intact. Saves without a recorded resident roster retain the earlier free-hauling mode; they do not invent residents or jobs. Standalone domain fixtures likewise retain that mode until employment is explicitly configured.

## Verification

Run the solution build/tests, build the Debug Godot project, import resources, then run:

```powershell
godot --headless --path game -- --validate-employment
```

Require exit 0 and `EMPLOYMENT VALIDATION PASSED`. This scenario uses real management controls, competing residents, the production simulation tick and Godot navigation to pick up and deliver wood, interrupts a loaded worker with a squad order, saves/loads employment and cargo, and checks off-shift job retention. The scenario is included in the complete CI validation manifest. `EmploymentTests` cover deterministic finite openings, capabilities/residency, explicit release, temporary interruptions, faction access, incapacitation/death, unreachable-work retries, assigned destinations, migration and rejected-load preservation.

For a rendered management capture, pass `--employment-screenshot=<absolute PNG path>` without `--headless`.

Recorded verification on 2026-10-31: Release solution and Debug Godot builds passed with zero compiler warnings/errors; all 769 domain tests passed, including 23 employment cases. All 28 registered Godot scenarios passed against the final code. The employment scenario also passed with graphical rendering; the [management capture](evidence/npc-employment/management.png) was inspected for control layout and ongoing-job status. Godot emitted shutdown ObjectDB/RID cleanup warnings despite successful process exits and pass markers.
