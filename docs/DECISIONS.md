# Decisions log

Record date, status, decision, reason, consequences, and revisit trigger. Supersede old entries explicitly so future coding sessions can follow the reasoning.

## D001 — Godot 4 .NET + C#

- Date: 2026-09-27
- Status: Provisional; requested initial direction
- Decision: Start with Godot 4 .NET and C#. Pin exact versions in F01 after validating setup.
- Reason: The initial risk is integrating a small simulation; typed domain code and a lightweight scene workflow suit that experiment.
- Consequence: Separate simulation rules from engine adapters where it is useful for tests. Avoid speculative portability layers.
- Revisit: At the end of 0.1, or earlier for demonstrated tooling or engine blockers; later graphical or world-streaming demands may justify another engine.

## D002 — Ant Farm scope

- Date: 2026-09-27
- Status: Initial scope baseline
- Decision: One fixed map, 10 human NPCs, 3 individuals of one creature species, two resource types, eating, hauling, wandering, inspection, and save/load. D025 adds one separately identified player character.
- Reason: Prove one complete autonomous loop before adding content.
- Consequence: Earlier brainstorming about combat, factions, relationships, and captivity is deferred. No deadline is asserted.
- Revisit: After the milestone acceptance scenario passes.

## D003 — Composition and a single simulation owner

- Date: 2026-09-27
- Status: Provisional implementation guidance
- Decision: Plain C# domain records/services, stable entity IDs, a fixed tick, a small action state machine, and engine adapters.
- Reason: Keep ownership explicit and changes understandable for AI-assisted coding.
- Consequence: No custom ECS, plugin system, or inheritance tree for every species.
- Revisit: Only when a concrete extension or measured bottleneck makes this inadequate.

## D004 — Durable snapshots; transient jobs restart

- Date: 2026-09-27
- Status: Provisional implementation guidance
- Decision: Save durable world state and carried items; rebuild jobs, reservations, and paths after loading.
- Reason: Prevent dangling runtime references and constrain save complexity.
- Consequence: Activity can restart after load; resource counts and need values must remain correct.
- Revisit: If future player commands or scheduling require exact job continuity.

## D005 — Prototype presentation

- Date: 2026-09-27
- Status: Provisional planning assumption
- Decision: Flat 3D map, overhead camera, static obstacles, placeholder meshes, desktop-first validation.
- Reason: Test spatial movement with minimal art and map work.
- Consequence: Final art style, platforms, and world scale remain undecided.
- Revisit: After observing the first complete loop.

## D006 — F01 toolchain pin

- Date: 2026-09-27
- Status: Provisional; validated for the scaffold
- Decision: Pin Godot 4.7.2 .NET (Mono), build `4.7.2.stable.mono.official.ed1daf0bf`, with .NET SDK 10.0.401.
- Reason: This is the installed compatible editor/SDK pair used to restore, build, test, and launch the initial project.
- Consequence: Local setup and future CI must use these versions unless a later issue records a deliberate upgrade.
- Revisit: If Godot or .NET compatibility blocks the prototype or a later F01 task.

## D007 — Fixed simulation clock policy

- Date: 2026-09-27
- Status: Provisional; implemented for F02
- Decision: Advance domain time in 0.1-second ticks at 1x or 4x speed. Pause discards elapsed wall time, and each frame processes at most eight ticks; overflow is discarded.
- Reason: Fixed ticks make simulation behavior independent of render frame chunking while the cap prevents long-frame catch-up spirals.
- Consequence: Frame deltas up to 0.8 simulated seconds are fully processed; larger deltas intentionally lose excess elapsed time.
- Revisit: If measured frame stalls or later simulation requirements need a different overflow policy.

## D008 — Entity ID lifetime

- Date: 2026-09-27
- Status: Provisional; implemented for E01a
- Decision: Allocate readable entity IDs monotonically within each World. Removing entities or resetting a World does not rewind its ID sequence; a newly created World starts a new sequence.
- Reason: A stale reference from before removal or reset must not resolve to a different entity in the same World.
- Consequence: Repeated scenario resets in one World continue increasing IDs.
- Revisit: If save/load or world replacement requires IDs to be restored from durable data.

## D009 — Entity action lifecycle

- Date: 2026-09-27
- Status: Provisional; implemented for J01a
- Decision: Give each entity one domain-owned active action, selected by fixed priority and stable candidate ID. Completion, cancellation, and preemption run the active action cleanup exactly once.
- Reason: Hunger must be able to preempt lower-priority work without duplicating action state or dropping carried inventory.
- Consequence: Action completion, cancellation, and preemption provide a lifecycle hook for transient reservation cleanup. Bounded retry timing remains a separate concern for follow-up job issues.
- Revisit: If action selection needs measured scheduling or parallel work beyond the Milestone 0.1 scope.

## D010 — Action-scoped resource reservations

- Date: 2026-09-27
- Status: Provisional; implemented for J01b
- Decision: A World-owned reservation service tracks resource quantities and destination capacity by active entity action and stable target ID. Action completion/cancellation, entity removal, and target removal release transient reservations.
- Reason: Competing actions must not claim the same resource units or storage capacity, and cleanup must not leave stale claims after lifecycle changes.
- Consequence: Reservations do not mutate durable inventories; transfer operations remain atomic and separate. Removed target IDs cannot be reused in that World.
- Revisit: If later job systems require partial reservation transfer or target recreation semantics.

## D012 — Arrival-gated food consumption

- Date: 2026-09-27
- Status: Provisional; implemented for H02b
- Decision: A domain food-consumption service composes food selection, action cleanup, and the navigation adapter. It removes one reserved food unit only after navigation reports arrival and the authoritative entity position is within the request tolerance.
- Reason: Keep food and satiety authoritative in the simulation while preventing remote, repeated, or failed-navigation consumption.
- Consequence: World owns food-source lifetimes; cancelling an eat action cancels its navigation request and releases its reservation. The Godot tick synchronizes movement before processing food outcomes.
- Revisit: When eating duration, partial nutrition, or persisted in-progress actions become part of a later issue.

## D013 — Bounded entity inventory

- Date: 2026-09-27
- Status: Provisional; implemented for J02a
- Decision: Store entity inventory in a World-created `ResourceContainer` with a configurable World capacity and a default of 10 item units.
- Reason: Hauling needs the same atomic quantity and capacity checks as ground sources and stockpiles.
- Consequence: All entities use the same initial capacity; inventory counts remain read-only to callers and transfers use the domain resource-transfer service.
- Revisit: If species-specific carrying capacity is required by a later milestone.
## D013 — Generic cargo retention and bounded action retries

- Date: 2026-09-27
- Status: Provisional; implemented for J01c
- Decision: Extract the bounded-retry delay used by food selection into a reusable `RetryScheduler` (10-tick default, matching D011) that any waiting job keys per entity. Give `EntityRecord` atomic, uncapped carried-inventory mutation, and extend the single inventory-transfer domain service (`ResourceTransferService`) with `TryPickUp`/`TryDeliver` overloads that atomically move items between a `ResourceContainer` and an entity's carried inventory. Give `ActionCandidate` an opt-in `RequiresEmptyCargo` flag so `ActionLifecycle.TryStart` returns `ActionStartResult.CargoPending` instead of starting new pickup-style work while the owning entity still carries items.
- Reason: Action completion, cancellation, and preemption already never touch `EntityRecord.Inventory`, but nothing previously exercised that guarantee with real carried items, and no job had a documented, reusable way to require delivery before a new pickup or to retry without reselecting every tick. These primitives let J02 (hauling) implement "resume delivery before a new pickup" and bounded waiting without inventing job-specific mechanisms.
- Consequence: Carried inventory has no capacity limit yet; bounded haul capacity per species is deferred to the hauling job that defines it. Only whole-entity removal discards carried inventory; cancellation, preemption, and reservation release never do.
- Revisit: When the hauling job (J02) defines per-species haul capacity, or if other job kinds need a different cargo-completion signal than "any carried item."
## D014 - Versioned durable world snapshots

- Date: 2026-09-27
- Status: Provisional; implemented for S01a and extended by C06c
- Decision: Capture simulation time, the entity ID allocator position, entity durable state, carried inventory, food/resource-pile/stockpile containers, and directed relationship values in a versioned JSON snapshot. Runtime actions, reservations, navigation requests, and engine nodes remain transient. Snapshot schema version 3 restored durable state, then reset registered transient services before replacing the world so their next tick rebuilt valid work; D025 adds seed and player profile state in schema version 4.
- Reason: Save/load needs a stable, engine-independent boundary that can be validated before a later restore operation replaces the running world.
- Consequence: Snapshot schema changes require an explicit version update; transient work is rebuilt rather than serialized.
- Revisit: When S01b adds atomic validation and world restoration.

## D015 — Directed relationship values

- Date: 2026-09-30
- Status: Provisional; implemented for C06a
- Decision: Represent an entity's relationship toward another as a directed pair with familiarity, trust, fear, and loyalty values. Each starts at 0, is bounded to [0, 100], and changes only through explicit finite deltas; reverse-direction values are separate.
- Reason: Trust, fear, familiarity, and loyalty can differ by observer, and later social systems need a deterministic, engine-independent foundation.
- Consequence: The relationship state type does not yet define memory, decay, autonomous interactions, recruitment, or captivity.
- Revisit: When a later 0.6 issue defines how social events update or decay relationship values.

## D016 — World-owned relationship lifecycle

- Date: 2026-09-30
- Status: Provisional; implemented for C06b
- Decision: The World owns directed relationship state keyed by observer and target entity IDs. Relationships are available only while both participants exist and are removed when either participant is removed or the World resets.
- Reason: Relationship records must follow the lifetime of their authoritative entity identities and must not retain stale references after removal or reset.
- Consequence: Relationship records are tied to the current World's entity identities; runtime memories and event histories are not persisted.
- Revisit: When a later 0.6 issue extends character identity beyond one World.

## D017 — Persist directed relationship values

- Date: 2026-09-30
- Status: Provisional; implemented for C06c
- Decision: Store each directed relationship's familiarity, trust, fear, and loyalty in the world snapshot and increment the snapshot schema version to 3. Reject invalid pairs or values before changing the running world.
- Reason: Relationship values represent durable consequences and should survive save/load, while validation must preserve the existing atomic restore guarantee.
- Consequence: Snapshots from schema versions 1 and 2 remain explicitly unsupported by the current loader.
- Revisit: If future migration work introduces backward-compatible snapshot upgrades.

## D018 — Atomic social relationship adjustments

- Date: 2026-09-30
- Status: Provisional; implemented for C06d
- Decision: Apply explicit familiarity, trust, fear, and loyalty deltas to a directed relationship as one operation. Validate participant identity and every finite delta before creating or mutating relationship state.
- Reason: A single social interaction can affect multiple relationship dimensions, and rejecting an invalid event must not leave a partial result.
- Consequence: The adjustment operation does not assign default effects to named events; interaction rules supply explicit deltas.
- Revisit: When later social interaction issues define concrete event rules or data-driven effect profiles.

## D019 — Data-defined social event effects

- Date: 2026-09-30
- Status: Provisional; implemented for C06e
- Decision: Represent help, rescue, harm, and threat effects as validated data profiles that provide explicit familiarity, trust, fear, and loyalty deltas to the atomic relationship adjustment service.
- Reason: Keep social consequences inspectable and tunable without embedding event balance in simulation code or introducing autonomous behavior prematurely.
- Consequence: Initial values in `data/relationship_events.json` are prototype tuning only; callers explicitly decide when an event occurs.
- Revisit: After observing the relationship behavior in a playable interaction loop.

## D020 — Provisional relationship bond levels

- Date: 2026-09-30
- Status: Provisional; implemented for C06f
- Decision: Derive Stranger, Familiar, Trusted, Loyal, or Fearful as a read-only projection of directed relationship values. Fearful takes precedence; Loyal requires configured familiarity, trust, loyalty, and maximum-fear thresholds.
- Reason: Later recruitment and domestication systems need a deterministic summary while preserving the underlying continuous values.
- Consequence: The initial thresholds are tuning defaults only and can be replaced through a validated threshold profile.
- Revisit: After bond levels are observed in a playable social interaction loop.

## D021 — Loyalty-gated voluntary recruitment readiness

- Date: 2026-09-30
- Status: Provisional; implemented for C06g
- Decision: A prospective recruit is eligible for voluntary recruitment only when their directed relationship toward the recruiter classifies as Loyal. Missing relationship state, all lower bond levels, and Fearful are ineligible.
- Reason: Make loyalty consequential without adding dialogue, faction membership, captivity, or a recruitment transaction before those systems are scoped.
- Consequence: Eligibility is only a read-only policy result; it does not recruit or change either character.
- Revisit: When recruitment actions, faction ownership, or captive release enter scope.

## D022 — Successful melee damage records harm

- Date: 2026-09-30
- Status: Provisional; implemented for C06i
- Decision: Resolve melee damage before applying the configured `harmed` event to the defender-to-attacker relationship. Require that event profile when constructing the social combat service.
- Reason: Combat should create a durable social consequence only when damage actually succeeds, while misconfiguration must not silently omit that consequence.
- Consequence: Rejected attacks do not create or change relationship state; reverse-direction relationships remain independent. This service does not provide combat input handling, retaliation, or event selection.
- Revisit: When combat adds additional outcomes such as blocks, misses, or non-damaging threats.

## D023 — Relationship pairs require distinct entities

- Date: 2026-09-30
- Status: Provisional; implemented for C06j
- Decision: Reject relationships whose observer and target are the same entity, reject self-targeted melee requests, and enforce the distinct-participant rule during snapshot validation.
- Reason: Recruitment and social consequences represent interactions between individuals; permitting self-edges could create false recruitment eligibility or let an attack apply harm to its own attacker.
- Consequence: Invalid self-pairs cannot be created, retrieved, or restored. Recruitment readiness for identical candidate and recruiter IDs reports no relationship.
- Revisit: If a future system introduces meaningful self-reflection or self-affecting needs, model those separately rather than as interpersonal relationships.

## D024 — Combat consequences require a live combatant and applied damage

- Date: 2026-09-30
- Status: Provisional; implemented for C06k
- Decision: Resolve melee damage through `CharacterHealth`, reject incapacitated attackers and dead defenders or destroyed body parts, and report success only when a positive representable health change occurs. Apply social harm only after that success.
- Reason: Combat state and social memory must reflect actual world consequences; attacks on incapacitated actors, corpses, destroyed body parts, or below-precision amounts must not appear to succeed.
- Consequence: Invalid combatants cannot mutate health or relationship values through melee resolution. `CharacterHealth.TryDamage` can report the actual health change to combat callers.
- Revisit: When non-damaging attack outcomes such as blocks, misses, or intimidation are modeled as explicit events.

## D025 — Deterministic player creation and session bootstrap

- Date: 2026-10-01
- Status: Provisional; implemented for issue #319
- Decision: Create a new game through one simulation-owned bootstrap containing 10 human NPCs, 3 individuals of one creature species, and one additional World-owned human player. The player starts with empty carried inventory; initial food and wood remain in world resource piles. The default seed is 2026, and the player spawn position is selected deterministically from the seed. Before the world starts, a creation form requires a name and offers Small/Medium/Tall height, Lean/Athletic/Average/Stocky/Thick body type, and Female/Male sex, defaulting to Medium/Average/Female. Stats use the existing human defaults.
- Reason: A repeatable new game needs an explicit player identity and appearance without duplicating the authoritative World or changing the existing NPC population.
- Consequence: The player entity is bound to the existing Godot PlayerCharacter view/controller. Placeholder geometry reflects height/body type and uses simple chest primitives for Female; final art is deferred. Seed and player profile are durable snapshot state, so the snapshot schema advances to version 4. The milestone's 10 humans are now explicitly 10 NPCs plus the player; player-directed survival actions remain outside this issue.
- Revisit: When player-controlled eating/actions or final character art are scoped.

## D026 — Creature Core boundary

- Date: 2026-10-01
- Status: Provisional planning decision for Milestone 0.3
- Decision: Extend the existing creature foundation with validated temperament data, deterministic wild behavior, and durable domain capture/captivity state. Validate these as a simulation scenario without adding player-facing capture controls or a taming loop.
- Reason: Creature Core needs a distinct, testable gameplay signal while preserving adjacent milestone boundaries: shared character data is established by 0.2, relationship-driven domestication/recruitment belong to 0.6, player interaction is deferred to 1.0, and advanced creature behavior belongs to 1.2. D033 authorizes only the additional entity lifecycle state transitions.
- Consequence: The milestone reuses the existing species catalog and wandering behavior. It does not add cages, capture equipment, player commands, bonds, recruitment, breeding, combat, or advanced ecology. Durable creature lifecycle state must participate in snapshot validation and round trips.
- Revisit: When 1.0 defines player-controlled creature interactions or a later milestone defines housing, release, taming, or escape behavior.

## D027 — First Real Game is a small player-controlled loop

- Date: 2026-10-01
- Status: Accepted milestone scope; implementation pending
- Decision: Define 1.0 as one fixed-map survival/settlement slice: direct player movement and eating, one human squad with move/follow/stop orders, and one friendly creature greeting gated by a species capability. Reuse the existing camp, finite resources, stockpile, World state, and durable save/load. A successful greeting applies the validated `greeted` relationship profile (+5 familiarity, zero other deltas) only to the directed player-to-creature relationship.
- Reason: The original milestone description grouped new-game, squad, survival, creatures, construction, combat, save/load, faction interaction, and regional content without a single end-to-end playable target. A smaller slice combines existing simulation foundations into observable player gameplay.
- Consequence: Construction, combat, faction play, capture, taming, recruitment, breeding, advanced creature behavior, autonomous social simulation, economy depth, and world streaming are not 1.0 commitments. Squad membership is durable, while orders/navigation reset and are re-evaluated after load; the snapshot schema must increment when membership is implemented. The milestone is accepted only when the deterministic integrated scenario and its documented failure/recovery checks pass.
- Revisit: After the 1.0 acceptance scenario is implemented and observed in Godot; reassess deferred systems from prototype evidence rather than promoting them automatically.

## D028 — Bounded creature temperament profile

- Date: 2026-10-02
- Status: Provisional; implemented for C03a and extended for #298
- Decision: Represent species temperament with immutable boldness, aggression, fear, curiosity, sociability, and territoriality values, each finite and normalized to [0, 1]. Require all six values in the existing species catalog.
- Reason: These independent, data-driven dimensions provide a small, explainable basis for later deterministic wild-behavior choices without conflating temperament with needs or relationships.
- Consequence: Every species definition must provide valid temperament data; deterministic behavior continues using boldness, curiosity, and sociability. Aggression, fear, and territoriality are data only until a separate issue defines and tests their behavior; reuse this profile rather than introducing another species registry.
- Revisit: After the deterministic creature behavior scenario demonstrates how these dimensions produce distinct outcomes.

## D029 — Bounded Economy Prototype scope

- Date: 2026-10-01
- Status: Accepted milestone scope; implementation pending
- Decision: Define 0.8 as one deterministic settlement economy slice that reuses existing farming, workstation recipes, storage, World ownership, and resource transfers. Add one known resource ID, `coin`, as the sole currency; each tradeable good has a validated fixed positive integer value in coins. One finite-stock caravan can exchange goods and coins with the settlement market, and trades are atomic across all participating containers.
- Reason: Resources, production chains, markets, and caravans are the milestone promise, but those systems need a bounded integrated outcome rather than open-ended infrastructure or a regional simulation.
- Consequence: Dynamic prices, supply/demand, multiple settlements, route networks, autonomous caravan travel, advanced finance, economic AI, and player-facing trading are deferred. Economy depth remains outside 1.0 acceptance. Coins use the existing resource-container and snapshot model; durable settlement totals use existing World snapshot behavior, while in-progress caravan visits are transient.
- Revisit: After the E08d integrated scenario is exercised and its economic signal is observed.

## D030 — Durable creature captivity

- Date: 2026-10-02
- Status: Implemented for C03b
- Decision: Store creature captivity on the authoritative entity, permit one capture transition for a live non-human entity, and persist the state in snapshot schema version 5. Migrate version 4 snapshots by defaulting entities to wild. D033 narrowly extends the lifecycle.
- Reason: Creature Core needs durable capture state without a separate registry or player-facing capture workflow, and existing saves should remain loadable when the new state is introduced.
- Consequence: Missing, non-creature, dead, or already-captive targets are rejected without changing entity state. Invalid captivity values are rejected during complete snapshot validation before transient services or the live World are reset.
- Revisit: When a later milestone defines player-controlled capture eligibility, release, or captive housing.

## D033 — User-directed creature lifecycle expansion

- Date: 2026-10-02
- Status: Implemented for issue #300
- Decision: Extend durable non-human creature lifecycle state through `Wild → Captured → Captive → Domesticated`. Explicit release from Captured, Captive, or Domesticated returns the creature to Wild. Keep death authoritative in `CharacterHealth.IsDead` rather than duplicating it in lifecycle state; dead creatures cannot transition. Preserve the existing snapshot shape and schema version because the existing state field carries the additional lifecycle values.
- Reason: Issue #300 explicitly requests capture, captivity, domestication, release, and death transitions. The user authorized this as a narrow expansion beyond the accepted 0.3 foundation, guided by the long-term vision in `docs/LONG_TERM_VISION.md` at `origin/docs-ai-guidance` commit `3e9de38abf0bfd07849a612bbedc4e01c977ba71`.
- Consequence: Release is an operation that returns state to Wild, not a separate durable state. Death remains the existing health signal and is not added to snapshots; health persistence, capture methods/equipment, housing, escape behavior, relationship-driven bonding, taming progression, UI, and autonomous creature behavior remain deferred.
- Revisit: When a later issue defines health persistence or player-facing capture, housing, escape, or domestication behavior.
## D031 — Deterministic temperament-driven roaming

- Date: 2026-10-02
- Status: Implemented for C03c
- Decision: Classify roaming as cautious when the mean of boldness and curiosity is below 0.5, otherwise classify it as exploratory. Cautious roaming selects the nearest configured goal; exploratory roaming selects the farthest. Goals already within arrival tolerance are skipped when another goal is available. Equal-distance choices use a stable hash of the session seed, entity ID, simulation tick, and that entity's trip count.
- Reason: Creature Core needs explainable behavior differences from existing bounded wandering goals without introducing random state, new ecology systems, or a behavior framework.
- Consequence: Fixed profiles and inputs produce repeatable goals, and navigation outcomes continue through the existing action lifecycle. Sociability remains profile data for future behavior rather than affecting this roaming decision.
- Revisit: When the integrated Creature Core scenario is exercised or a later issue introduces explicit social stimuli or additional wild actions.
## D030 — Bounded faction foundation

- Date: 2026-10-02
- Status: Implemented for F07a–F07d
- Decision: Reuse the authoritative World for a small fixed faction catalog and optional entity allegiance. Track entity-to-faction reputation as an explicit bounded value in [0, 100]; represent hostility as a separate directed faction-to-faction value, not as an inference from individual reputation. Add optional faction owner metadata to existing settlement resource containers without enforcing access policy.
- Reason: The roadmap milestone names allegiance, reputation, hostility, and ownership, but these are foundations for later behavior rather than a requirement to simulate full faction politics now. Separating reputation from hostility avoids silently inventing a policy threshold; metadata on current containers avoids a parallel inventory/property system.
- Consequence: Allegiance, reputation, hostility, and stockpile owner references are durable and validated with existing World snapshot rules. Snapshot schema version 8 adds optional stockpile ownership; versions 4–7 migrate with stockpiles unowned. Explicit invalid operations reject without partial mutation. Autonomous reputation changes, diplomacy, law/crime, territory, warfare, ownership enforcement/theft, UI, and faction authoring tools remain out of scope.
- Revisit: After the F07d scenario is exercised; later behavior must be scoped from observed prototype needs.

## D032 — Bounded creature perception queries

- Date: 2026-10-02
- Status: Provisional; implemented for issue #296
- Decision: Provide read-only nearby-food, nearby-entity, and nearby-track queries in the existing simulation domain. The observer species' smell range bounds all three; food must be available and compatible with the observer's diet, nearby-entity results exclude the observer, and tracks are queryable only when the species has tracking capability. Results sort by distance and then stable identifiers.
- Reason: Make existing smell-range and tracking capability data usable through deterministic, directly testable query behavior without coupling perception to rendering or a speculative behavior framework.
- Consequence: Tracks are explicit immutable observations supplied to the query and are not generated, aged, or persisted by this feature. Queries do not reserve food, mutate World state, or introduce hunting/tracking behavior.
- Revisit: When a bounded hunting/tracking consumer defines observation creation, expiry, and persistence requirements.

## D031 — Vertical integration is an evidence gate

- Date: 2026-10-02
- Status: Provisional planning decision for Milestone 0.9; implementation pending
- Decision: Define 0.9 as one deterministic test-region scenario composing accepted 0.3–0.8 systems. It adds only the integration harness and evidence; missing product behavior becomes a separately scoped issue.
- Reason: “All core systems” is otherwise unbounded and could pull multiple settlements, regional simulation, streaming, or broad AI forward without evidence.
- Consequence: The milestone verifies existing creature, combat, relationship, faction, settlement, and local economy state together, with repeatability, exact resource accounting, and rejection/recovery checks. It does not expand those systems or turn the long-term vision into a delivery commitment. Issue #341 is gated on acceptance of prerequisites #325, #333, and #340.
- Revisit: After the one-region scenario has recorded results and limitations.

## D032 — Capability-gated creature greeting

- Date: 2026-10-02
- Status: Implemented for issue #297
- Decision: Permit the player's selected target to receive a friendly greeting only when its species data enables `canAcceptFriendlyInteraction`. Missing capability data fails closed. Bind the Interact action to `G`, require a maximum distance of 2 world units, and apply the validated `greeted` relationship event only from player to target.
- Reason: The greeting is the one player-facing creature interaction in the 1.0 slice; explicit capability, range, and directed effects keep the behavior constrained and testable.
- Consequence: Unsupported, invalid, removed, self, and out-of-range targets produce a rejection without relationship mutation. No capture, taming, recruitment, or general-purpose interaction framework is introduced.
- Revisit: After the 1.0 integrated acceptance scenario is exercised in Godot.

## City-scale world map

- Decision: User-directed scope expansion. The playable map is a flat 400 x 400 m area: a 120 x 120 m central town (roads, building blocks as navigation holes) with countryside beyond (fields, tree clumps, country roads), built from procedural primitives. The navigation mesh is a coarse grid over the whole map with holes for buildings and the central obstacle. Humans, food, wood, and stockpile start in town; creatures start and wander in the countryside.
- Consequence: Replaces the 20 x 20 placeholder map. Scenery is presentation only and not saved.

## Entity visual and inspector detail

- Decision: Humans, apes, and creatures get procedural primitive bodies (head, eyes, limbs, species features, per-entity color variation) and the inspector shows species traits, diet, temperament, and capabilities from the species catalog, following the long-term vision of people and creatures with diets, temperaments, and capabilities.
- Consequence: Presentation only; no new simulation state. Guided by docs/LONG_TERM_VISION.md (origin/docs-ai-guidance, 3e9de38): Character layer detail (body parts, captivity, personality) and anatomy-driven creature capabilities.

## Procedural movement and jointed models

- Decision: Player, humans, apes, and creatures use jointed primitive rigs (hips, knees, shoulders, elbows, head, tail) driven by a presentation-only ProceduralGait (walk cycle from observed speed, idle breathing and sway, facing movement direction). Quadrupeds use diagonal gaits; apes lean forward with long arms. Per docs/LONG_TERM_VISION.md, anatomy differs by species and drives how a body moves; final art and animation are deferred.
- Consequence: No simulation state or save data changes.

## Temporary character-creation bypass

- Decision: For the current play-test phase, the startup scene skips the character-creation form and starts a new game with the default medium, average, female `Test Player` profile. The exported `Main.SkipCharacterCreationForTesting` flag controls this behavior; set it to `false` to restore character creation.
- Reason: Let play-testing focus on movement and the shared world simulation before the creation flow is ready for broader testing.
- Consequence: Every fresh launch starts a new test session. The character-creation form remains in the scene and its regular new-game path is unchanged.
- Revisit: Set the flag to `false` when character creation should return to the test flow.

## Individual temperament-driven behavior

- Decision: Keep validated species temperament as the baseline, then derive bounded, deterministic individual values for all six temperament traits from each entity's stable ID. Current behavior continues to use boldness, curiosity, and sociability: creatures choose cautious nearby roaming, farther curious exploration, or goals near their same-species group according to those traits, and hauling entities carry a boldness-weighted amount of wood per trip rather than every worker filling the same load. Aggression, fear, and territoriality remain unused by behavior systems.
- Reason: Make shared needs produce observably different behavior without introducing job frameworks, autonomous social systems, or separate creature rules.
- Consequence: Individual traits are a read-only projection and need no new save fields; they remain stable while entity IDs remain stable. Food seeking and hunger priority are unchanged. Species temperament and individual variation remain inspectable.
- Revisit: When traits need to be player-authored, trained, inherited, or changed by memories and relationships; then store them as explicit world state.

## New-game creature variety

- Decision: Replace the three identical starter creatures in `NewGameSession` with one ape, one wolf, and one cow, each placed in the countryside and validated against the species catalog. Add finite plant and meat food sources near their habitats while retaining the town's human food, wood, and stockpile. Keep `AntFarmScenario`'s three generic creatures unchanged as a historical regression scenario.
- Reason: Make the species differences already modeled by anatomy, diet, capabilities, and temperament visible in the playable session, consistent with the long-term vision's ecological species roles.
- Consequence: A new session still has three creatures, but each species has distinct behavior, needs, visual form, and accessible compatible food. The 1.0 setup acceptance now names the starter species.
- Revisit: Adjust population or food quantities when the integrated Godot scenario provides evidence about survival or resource balance.

## Player body rig and combat groundwork

- Decision: Replace the player's whole-node scale with a code-built jointed humanoid rig (`PlayerBodyRig`) generated from `PlayerBodyProportions`, a pure simulation-side mapping of character-creation height, body type, and sex to limb lengths, torso/limb thickness, shoulder/hip balance, and chest. The collision capsule and camera height follow the proportions instead of being scaled. The player uses `ProceduralGait` for hips, knees, shoulders, elbows, torso twist, head sway, and breathing like NPCs. Melee input now drives an `AttackSwing` (wind-up, strike, recovery) that layers over the walk, twists the torso, and raises `PlayerCharacter.StrikeReleased(left, bodyPart)` once at impact. Mouse vertical movement tilts the head, and `CombatAim` maps that pitch to Head, Torso, or the swinging side's leg.
- Reason: User-directed groundwork so the player has the same procedural body as other characters, reflects creation choices, and has independent arm, leg, and head joints for later combat. This is scope beyond Milestone 1.0; no hits, damage, or injuries are resolved yet.
- Consequence: Nothing is wired to `MeleeCombatService`; the strike event and aimed body part names match `CharacterHealth.CreateHumanoid` so reach validation and damage can attach later. Visuals are unverified in a windowed run.
- Revisit: When combat is integrated into the playable scene (Milestone 0.5 scope), resolve strikes through the domain combat services and drive poses from equipment and injuries.

## Issue #299 — Minimal wild-creature decision policy

- Date: 2026-10-02
- Status: Implemented for issue #299
- Decision: Add a deterministic, read-only policy over explicitly supplied stimuli. Prioritize flee from a detected threat, seek compatible available food when hungry, return to a supplied area target when outside the area, pursue a supplied target, then idle. Use the long-term vision (`origin/docs-ai-guidance`, `3e9de38abf0bfd07849a612bbedc4e01c977ba71`) as direction, not as permission to broaden this implementation.
- Reason: Establish an explainable decision signal using the existing entity needs and perception foundation without adding another world owner or behavior framework.
- Consequence: The caller supplies already-resolved food, threat, pursuit, and area inputs. The policy does not sense threats, select targets from candidate sets, mutate the World, or execute navigation, fleeing movement, pursuit/combat, hunting, or territory behavior. Those execution systems remain deferred and must be scoped separately; existing temperament-driven roaming remains unchanged.
- Revisit: When a later issue defines how these decisions connect to fixed-tick action execution, threat classification, target selection, and area ownership.

## D034 — Fixed prototype trade-good values

- Date: 2026-10-03
- Status: Provisional; implemented for E08a
- Decision: Store the known tradeable resource values in `data/trade_goods.json`; start with food 2, fruit 3, meat 4, plant 1, and wood 2 coins. Keep `coin` known to the resource model but reserved from the priced-good catalog.
- Reason: The first economy slice needs deterministic, inspectable values without introducing dynamic pricing or a second item registry.
- Consequence: Each catalog load exposes the same fixed values and sorted resource IDs. These values are prototype tuning, not a claim about balanced market behavior.
- Revisit: After the E08d integrated market scenario records exchange outcomes.

## D035 — Atomic fixed-price market exchange

- Date: 2026-10-03
- Status: Provisional; implemented for E08b
- Decision: `SettlementMarket` resolves a World stockpile on each request and prices one catalog good in `coin` with checked integer arithmetic. Purchase and sale share one rule that calls `ResourceTransferService.TryExchange`, which prevalidates both legs (stock, and capacity after the legs net out) before mutating either container.
- Reason: A compensating rollback could leave partial state; whole-operation prevalidation keeps one authoritative inventory and no partial fills.
- Consequence: Rejections return explicit statuses and leave all counts unchanged. The counterparty is any `ResourceContainer`; caravan wiring is deferred to E08c. No dynamic pricing, credit, or UI.
- Revisit: After E08c/E08d show how counterparties are modeled.

## D036 — World-owned squad membership, transient movement orders

- Date: 2026-10-03
- Status: Implemented for #312
- Decision: Keep one squad's stable human NPC IDs in World and schema 9. A transient SquadOrderService owns move/follow/stop and uses existing navigation and action cleanup. Movement priority 250 sits between hunger 300 and hauling 200. Orders suppress member hauling/roaming; hunger preempts and movement resumes afterward. Stop permits hunger response. Follow retargets after one unit of player movement; arrival tolerance is 0.5 units.
- Reason: Reuse authoritative positions and reservation/cancellation rules without persisting live navigation.
- Consequence: Schema 4–8 migrates to empty membership while preserving schema 8 ownership. Invalid member IDs reject before changing live state. Failed navigation waits for a changed order, avoiding request churn. No formations, creature recruitment, multiple squads, or tactical AI.
- Revisit: When a scoped issue requires spacing, recruitment eligibility, or persistent orders.

## 1.1 daily work/rest slice (2026-10-03, #200 / #380)

- Decision: Deliver configurable daily schedules for human NPCs as the user-selected 1.1 slice. Use one World-owned work window per NPC and the existing simulation clock: a 240-second day beginning at 08:00, default work 08:00-18:00, inclusive start and exclusive end, overnight windows supported.
- Consequence: Rest cancels new/unfinished pickups before navigation advances; carried wood can finish delivery. Hunger and squad orders retain priority. Schema 10 stores overrides and migrates schemas 4-9 to defaults. No fatigue, sleep infrastructure, job priorities or new jobs are introduced.
- Validation: Focused behavioral invariants and actual Godot inspector/navigation evidence are in docs/NPC_SCHEDULES.md; #381 accepts the combined milestone.
- Revisit: Tune day length and default hours from gameplay evidence; future job systems can consume this policy without another clock or duplicate world state.

## Player menu repair and future placeholders (2026-10-03, #384)

- Decision: Provide distinct Inventory (Status/Equipment/Items/Other), Settlement (Overview/People/Squad) and Save/Settings (Game/Controls) screens before 1.2 work, as requested by the user. Reuse existing World projections, settlement handlers, controller and atomic save store.
- Consequence: Menu state owns visibility and gameplay input suppression; preferences are separate version-1 settings JSON, without a World schema change. Current inventory is read-only; equipment shows existing hand/shield configuration. Disabled Planned wearable slots and Audio/Display tabs reserve UI space without implementing future systems. Defaults and conflicts have one validated mapping boundary; writes precede InputMap mutation.
- Validation: docs/PLAYER_MENUS.md records dispatched Godot input, UI controls, physics/input isolation, persistence failure recovery and actual rendered screenshots.
- Revisit: Enable equipment/audio/display controls only when their concrete issues define rules, assets, persistence and acceptance criteria; placeholder layout is not a scope commitment.

## Gameplay target selection (2026-10-03, #388)

- Decision: Fix menu-only selection with a configurable target-cycle key (default T), a gameplay ring/HUD and one transient typed target reference shared with existing entity actions. The user accepted a target key to preserve mouse attack/look. New/restored greeting defaults use F; existing preferences are preserved.
- Consequence: First selection favors the camera center; subsequent cycling follows visible target type/ID order within 40 m. Visibility checks camera frustum and conservative mesh-bounds occlusion for the current visual-only scenery. Selection does not mutate World or persist. Settings version 2 adds the target binding; version 1 migrates in memory with an unused key and no file rewrite until a settings change is saved. No new physics obstacles, inventory model or World schema.
- Validation: docs/WORLD_SELECTION.md records dispatched input, rendered target feedback, stale-reference cleanup, preference migration and gameplay regressions.
- Revisit: Define mouse picking and final-art/transparent occlusion separately if needed; map, sprint, dodge, abilities and combat hit resolution retain their existing scope boundaries.

## Gameplay refinement after playtesting (2026-10-03, #390)

- Decision: Supersede the earlier unwired-strike and center-only targeting limitations. Capture the entity when an actual swing begins and resolve once at impact through existing melee/social combat; sample actual rendered body parts for visibility so low storage cannot hide an exposed human. Show action outcomes in gameplay.
- Consequence: Tap the existing collect binding on short release for one atomic, capacity/reservation-aware pickup; hold 0.6 s to use one compatible carried food. No simultaneous pickup/use, input repeats or settings/schema changes. A supported successful greeting displays the directed effect and turns the view toward the greeter for two seconds without replacing its job. Health and facing persistence remain deferred under #302.
- Validation: Domain invariants and actual input/physics/renderer scenarios are documented in docs/GAMEPLAY_REFINEMENT.md. Prototype damage is 10 times strength with 2.2 m reach and a forward cone; final-art obstruction, combat AI and equipment remain separate.

- User refinement: Facing is required during and after greeting. The view tracks the live greeter for at least two seconds and continues while idle until purposeful movement needs its attention. The user also prioritized health as core gameplay; complete a focused persistence follow-up under #302 rather than leaving injuries transient as the final state.

## Interaction resolver repair (2026-10-04, #393)

- Decision: Repair the current capture/feed/pet/give contract with present conscious participants, 2 m reach, truthful result messages, one-unit compatible feeding and atomic bounded giving. ProvidedItem describes a requested item; World inventories retain ownership. Positive pet/feed/give responses are directed from recipient to actor; greeting retains its existing direction.
- Consequence: Existing F feed/pet and G greeting bindings show actual mapped hints and sustained attention during/after successful responses. New and restored Worlds configure their species catalog. Remove the obsolete backtick-suffixed resolver artifact; eliminate nullable build warnings with explicit lifecycle guards. No save schema or new interaction menu/keys.
- Validation: docs/INTERACTION_REPAIR.md records success/failure invariants, actual Godot input and rendered evidence. Health persistence remains the next separately scoped user correction under #302.
- User refinement: Sustained greeting attention has no timeout. A new task overrides it immediately, including stationary tasks; separation greater than 2 m also ends it. Exactly 2 m retains attention, and a new nearby interaction is required to reacquire attention after the cutoff.

## Follow player clearance (2026-10-04, #395)

- Decision: Follow stops at 2 m horizontal separation and resumes beyond 3 m. Clip every active follow movement segment against the live player boundary before assigning the NPC collider position; keep World synchronized first. Use a 1 cm arrival tolerance for numeric stability.
- Consequence: A stationary player cannot be driven across the map by follow penetration/replanning. Already-near followers hold, including when the player approaches them; no snapping/teleporting or inter-NPC formation framework. Move-to, hunger, stop and load/reset retain existing behavior and schema.
- Validation: docs/FOLLOW_SPACING.md records 570 domain tests and actual Godot navigation/player-physics checks for one and ten followers, stale paths, large steps, holding/resume and 4x ticks. Current body capsule sums are below 0.82 m; future body sizes must revisit the clearance policy.

## 2026-10-04: recipient social feedback and reversible Trusted petting (#397)

User playtesting selected unit positive/negative social steps for creatures, NPCs and faction standing. Production profiles now use nonzero +1/-1 deltas. Gameplay hides persistent totals and shows actual applied changes above the recipient; clamps, rejections, defaults and loads emit no fabricated gains. Greetings now use recipient-to-actor direction, consistent with feed/pet/give and defender-to-attacker harm. The player's outgoing relationship UI is hidden; human/player capture remains rejected.

Reuse the existing Trusted/Loyal/Fearful classifier for pet permission and green selection rings. Domestic cows start at trust 50 and the existing Familiar threshold toward humans and non-predators. These are provisional data defaults for new records only; existing and saved bonds restore exactly, including zero or betrayed trust. No schema change or passive reset. Hungry creatures receive carried food through F; full qualified creatures may be petted without wasting food. World notifications carry actual applied deltas; explicit setup/import adjustments keep their supplied quantities. The faction unit event API does not introduce quest/reward triggers. See SOCIAL_FEEDBACK.md for scope, evidence and validation.

## 2026-10-04: town-center workbench presentation (#399)

User reported only an obstacle at the expected workbench. Replace that legacy central cube with a labeled procedural workbench using the shared NewGameSession starter placement. Keep the existing navigation exclusion and use actual mesh parts for sight/strike occlusion. The bench is a static town landmark; this visual repair does not fabricate World workstation records after loading or claim recipe/progress persistence. Existing crafting domain execution is unchanged, and playable crafting controls/workstation persistence remain separate scope. See TOWN_WORKBENCH.md for rendered evidence and reset/load/regression checks.

## D037 — Linked settlement storage networks (2026-10-05)

- Status: Design direction; implementation not started.
- Decision: Keep physical storage units as authoritative access points, but link units with the same settlement, storage family, and tier into one logical inventory network. Settlement management aggregates each resource ID across the network while retaining detail about physical contributors, reservations, capacity, and local buffers.
- Reason: Production buildings and settlement management need a useful total such as `wood 200`, `food 200`, and `water 1000` instead of fragmented per-container counts. A network projection keeps the player-facing model simple without introducing a global inventory.
- Consequence: Production buildings may consume inputs and place outputs through their linked network using the existing atomic transfer and reservation services. Physical placement, access, capacity, ownership, destruction, and hauling distance remain meaningful. Existing containers migrate into a default network without changing totals.
- Revisit: When the first implementation issue defines network membership, reservation participants, removal/spill behavior, and snapshot migration tests.

## D038 — Character creation and settlement Build mode boundaries (2026-10-05)

- Status: Design direction; implementation not started.
- Decision: Put authoritative body parameters (height, weight, muscle/build, named body-part X/Y/Z bounds, symmetry, and derived proportions) in 0.2 Character Core. Put Settlement-menu Build mode, ribbon selection, ghost blueprints, staged structures, linked storage, and completed object menus in 0.4 Basic Settlement. Put player-facing Sims-style sliders and advanced body-part controls in 1.0 First Real Game after the shared profile-to-rig projection is stable.
- Reason: Body data is a reusable simulation contract; settlement construction is a separate interaction mode; and player creation needs the accepted 1.0 profile/save flow. Keeping these boundaries avoids mixing UI, building placement, and arbitrary mesh editing.
- Consequence: Body changes are validated proportions consumed by collision, joints, camera, and animation. They cannot grant arbitrary skills or bypass species capabilities. Build mode is opened through Settlement and does not edit character proportions. Each slice can be tested and persisted independently.
- Revisit: When the 0.2 body model, 0.4 Build mode, and 1.0 player-creation acceptance scenarios provide evidence about range, rig fidelity, and usability.

## D039 — Separate direct placement and Sims-style planning camera (2026-10-05)

- Status: Design direction; implementation not started.
- Decision: Direct placement uses the moving player, third-person camera, hotbar/ribbon selection, and player-relative placement for the 0.4 prototype. Sims-style planning uses an explicit free camera from the Settlement menu so layouts can be placed or queued without moving the player model.
- Consequence: Both workflows submit to the same authoritative construction systems, but each has separate mode state, camera rules, input tests, and acceptance criteria. Planning-camera work follows the direct-placement slice.
- Revisit: When bounded free-camera navigation, multi-blueprint placement, save/load, and clean return to the player-follow camera are demonstrated.

## D040 — Entity movement safety and unstuck recovery (2026-10-05)

- Status: Design direction; implementation not started.
- Decision: Add a shared safety layer for humans and creatures that detects blocked progress, crowd overlap, and repeated failed movement; cancels or replans blocked orders, preserves player control, and chooses nearby valid free positions without moving through solid geometry or teleporting.
- Reason: Crowding and follow behavior must not permanently trap the player or deadlock entities.
- Consequence: Recovery remains compatible with existing navigation, orders, construction movement, and save/load. Formation AI, tactical crowd behavior, and arbitrary teleportation remain out of scope.
- Validation: Use the scenarios in docs/ENTITY_SAFETY.md, including narrow-space follow, converging crowds, no-progress detection, responsive player input, save/load, and regression checks.

## D041 — Dense entity crowds remain physically meaningful (2026-10-05)

- Status: Design direction; implementation is being staged.
- Decision: Active entities may collide with the player and with one another. A group should form a dense, difficult-to-push-through mob, while the movement-safety layer detects prolonged enclosure and creates a bounded escape or replan opportunity.
- Reason: Entity crowds should have physical presence comparable to a mob in a survival or action game rather than behaving as ghosts.
- Consequence: Crowd resistance and safety recovery are separate acceptance concerns. The player may be slowed or redirected by a mob, but never permanently trapped, teleported, or allowed to clip through solid geometry.
- Revisit: When a playable crowd test demonstrates density, resistance, enclosure recovery, and normal movement regression behavior.


## D042 - Semantic interaction anchors are the anatomy-to-animation boundary (2026-10-22)

- Status: Accepted provisional implementation standard.
- Decision: Species definitions expose anatomy regions, locomotion anchors, equipment/carry anchors, interaction anchors, and breeding anchors through stable lowercase semantic IDs. Interaction and breeding anchors are declared data; they do not imply gameplay state, capability, consent, fertility, or a specific animation. Imported bones, node paths, and mesh-specific labels remain behind a rig adapter.
- Reason: Breeding, medical examination, feeding, nursing, waste handling, grooming, creature handling, and species-specific social interactions need reliable alignment points across humanoids, mammals, birds, egg-layers, and future species without coupling simulation or saves to one downloaded model.
- Consequence: Animation and IK systems resolve semantic IDs, while each species validates its own capability, state, relationship, range, and animation requirements. Existing species records may load empty anchor collections for compatibility, but new production species must declare and validate applicable anchors. Asset metadata with older anchor shapes requires migration before production use.
- Revisit: When the first breeding or medical interaction slice defines capability/state validation, anchor resolution in imported Godot scenes, and save/load migration evidence.

## D043 — Human anatomy reference and future breeding compatibility boundary (2026-10-22)

- Status: Reference architecture and migration target; implementation not started.
- Decision: Keep simulation anatomy and save identifiers separate from visual skeleton bones. The current `CharacterHealth` six-region contract (`Head`, `Torso`, `LeftArm`, `RightArm`, `LeftLeg`, `RightLeg`, in that order) remains authoritative for runtime combat and existing saves. The detailed human regions in `HUMAN_ANATOMY_AND_BREEDING_REFERENCE.md` are a future migration target only. Treat species-pair compatibility defaults (`viable`, `sterile`, `incompatible`, or `unknown`) as explicit data rules rather than an inference from shared rigs or visual similarity.
- Reason: Human anatomy and future breeding need enough semantic detail for injuries, equipment, body conditions, and lineage without coupling gameplay to an imported rig. Keeping the current six-region schema stable avoids silently invalidating combat or save restoration while the reference work remains non-runtime.
- Consequence: This PR does not change health behavior, save schema, breeding, pregnancy, genetics, or hybrid generation. Any future region expansion must include a versioned save migration, an old-save compatibility path, deterministic tests, and an explicit mapping from visual bones to simulation regions. Breeding work must define supported species pairs and safe rejection for unknown or unsupported pairs before mutating state.
- Revisit: When a concrete health-region migration or first playable breeding issue is scoped with schema/versioning, mapping, and deterministic acceptance tests.

## D044 — Wild home territories are durable World-owned data (2026-10-23)

- Status: Accepted for the Milestone 1.2 prototype.
- Decision: Store an optional territory per entity in `World`, keyed by its stable entity ID and defined by a finite center and positive finite radius. Territory data is descriptive state only; behavior systems must separately check whether the entity is currently wild before using it. Removing an entity or resetting a World removes its territory. Persist it in snapshot schema 17; older supported snapshots migrate with no territory assigned.
- Reason: Territory return needs a stable home area that survives save/load without coupling simulation state to a scene node, navigation path, or roaming service.
- Consequence: Snapshot validation must reject unknown or duplicate territory owners and invalid geometry before applying any state. Territory assignment does not add roaming, capture policy, hunting, packs, or migration by itself.
- Revisit: When the 1.2 territory-return and wolf hunting/feeding scenarios are integrated, or when world/region ownership replaces per-entity territory records.

## D045 — Wild roaming stays within a local home territory (2026-10-23)

- Status: Accepted prototype behavior for Milestone 1.2.
- Decision: Starter wild creatures receive a 35 m home territory centered on their new-game spawn. Normal roaming selects deterministic points inside that territory. A wild creature displaced outside returns through `WildCreatureDecisionPolicy.ReturnToArea` and the existing navigation adapter when it has no urgent food target. Hunger-driven hunting may leave the territory to pursue compatible prey within smell range; if no prey is available, the hungry creature returns home and resumes its food search there. Captured and domesticated creatures are not forced back to their former territory.
- Reason: Local roaming and recovery make wildlife feel tied to a place while keeping this first behavior slice bounded and testable.
- Consequence: Failed return routes clear their active movement and wait ten simulation ticks before another attempt. Return stops when the creature re-enters its territory; a later tick resumes normal local roaming. No threat detection, pack/herd movement, migration, or terrain-aware boundary adaptation is added.
- Revisit: When the 1.2 hunting/feeding loop and a larger representative world are playable; tune territory sizing from observed movement and map scale.

## D046 — Starter wolf hunting uses species-declared prey and shared food systems (2026-10-23)

- Status: Accepted for the Milestone 1.2 prototype.
- Decision: Predator species declare eligible prey IDs and carcass meat yield in the validated species catalog. A transient hunting service chooses the nearest eligible wild prey by distance and stable entity ID, uses existing navigation and combat, then delegates carcass creation and eating to the existing harvest and food systems. Hunt targets, routes, and retry timers remain transient; the world stores only the harvested carcass as durable resource state. The starter wolf begins hungry and the starter world does not provide free meat.
- Reason: A small playable hunt-and-feed loop proves that creature traits, autonomy, combat, harvesting, and needs can interact without adding a second resource or combat pipeline.
- Consequence: Initial prey eligibility is limited to configured species, wild and living state, smell range, and same-faction exclusion; territory does not constrain hunger-driven pursuit. If no compatible prey is in range, the separate wandering behavior can return the predator home. Unreachable or invalid targets clear the hunt and are reconsidered after a bounded retry delay. `CreatureHarvestService` only accepts dead entities, preventing a live prey target from being removed as a carcass.
- Revisit: When pack behavior, prey defense, carcass decay/handling, regional simulation, or cross-faction hostility should alter target eligibility or hunting outcomes.

## D047 — Wild prey flees configured predators using transient navigation (2026-10-23)

- Status: Accepted prototype behavior for the first 2.0 Living Region slice.
- Decision: Species may declare a finite, nonnegative `threatDetectionRange`; missing legacy values default to zero, which disables threat response. A living predator threatens an entity when its species definition lists that prey species and it is within the prey species' configured range. `WildPreyThreatService` chooses the nearest eligible threat with a stable entity-ID tie-break, uses the existing flee decision and navigation contracts, and moves the prey away. Flee actions preempt food seeking and ordinary movement. Routes, threat references, and retries remain transient.
- Reason: The 1.2 wolf hunt made the predator loop playable, but prey had no response. A configured, deterministic evasion rule makes hunting interactive without adding a separate combat or perception framework.
- Consequence: The starter cow detects its configured wolf predator at 18 m. Failed routes clear their action and retry after the shared bounded delay; when no threat remains, normal food and territory behavior resumes. Species data remains outside world save state, so this adds no save migration. The first version does not add pack/herd behavior, threat animations, terrain-aware escape scoring, or population/ecosystem simulation.
- Revisit: When playtesting shows that species need distinct senses, terrain-aware escape destinations, group defense, or predator/prey hostility beyond declared prey lists.

## D048 — Harvested carcass freshness is durable simulation state (2026-10-23)

- Status: Accepted for the first 2.0 Living Region implementation.
- Decision: A harvested carcass food source owns an optional finite `FreshnessSecondsRemaining` value. Species definitions provide `carcassFreshnessSeconds`, defaulting to 300 simulation seconds for legacy or omitted data. The decay service advances this timer only from simulation ticks and removes the source, its reservations, and its world marker at expiry. Generic food sources remain nonperishable. Remaining freshness is durable and is serialized in snapshot schema 18.
- Reason: Hunting should produce food that can be eaten or stored through the shared food system without leaving unlimited meat in the world indefinitely. Simulation time keeps freshness consistent with pause and speed controls.
- Consequence: Schema 17 and earlier snapshots retain existing food as nonperishable when migrated, so loading older saves does not unexpectedly discard resources. Expiring a carcass removes it from new selections; entities already traveling to it use the existing removed-target recovery path. This does not add meat processing, refrigeration, spoilage UI, migration, population growth, or predator group tactics.
- Revisit: When storage and food preservation are implemented, or playtesting shows that the provisional 300-second species default does not fit the intended hunting and settlement loop.

## D049 — Far wildlife populations are World-owned durable aggregates (2026-10-23)

- Status: Accepted for the first 2.0 Living Region implementation.
- Decision: The authoritative World owns far-population aggregates keyed by the pair `(regionId, speciesId)`. Each aggregate stores count, average satiety, and resource quantities. Near/far transitions operate on one species record at a time; the legacy region-only transition overload is allowed only when that region has exactly one matching aggregate. Snapshot schema 19 persists these records; older snapshots migrate with no far-population records.
- Reason: Regional populations must survive service recreation and save/load, and multiple species in one region must not overwrite one another during transitions.
- Consequence: `NearFarTransitionService` is a transition adapter, not the owner of distant population state. This establishes durable storage and scoped transitions only; it does not define births, deaths, respawning, migration, pack behavior, food production, or spawn placement.
- Revisit: When regional population rules are implemented and can define how individual state, resources, and counts change over time, or when measured save size/simulation needs require a different aggregate representation.

## D050 — Starter wildlife proximity tracks durable active membership (2026-10-23)

- Status: Accepted for the first playable 2.0 proximity slice.
- Decision: Keep the starter region's identity, 90 m activation radius, 110 m deactivation radius, species, and deterministic spawn anchors in validated data. A fresh player session begins with World-owned far aggregates; entering the inner radius promotes configured species to entities, and leaving the outer radius demotes their surviving members. `World` owns each active `(regionId, speciesId)` membership and its entity IDs. Snapshot schema 20 persists this mapping and non-player terminal death state; schema 19 and earlier snapshots migrate without inferred active membership.
- Reason: The World must know which active animals belong to regional counts after service recreation or save/load. This prevents a hunted or dead animal from being counted again and prevents repeated proximity checks from duplicating entities.
- Consequence: Proximity checks and view creation are transient adapters over World-owned data. A removed entity is removed from its near group; a dead entity remains excluded when the group is demoted. The prototype supports one starter region only. It does not add births, population growth, respawning, migration, pack behavior, or general world streaming. Older saves do not invent wildlife-region membership. Non-player injury-part health remains outside this slice; only terminal death is retained for these entities.
- Revisit: When playtesting establishes better region radii or spawn placement, or a later regional simulation issue needs population changes, migration, multiple regions, or full non-player injury persistence.
