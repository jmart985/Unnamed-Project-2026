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

## D035 � Atomic fixed-price market exchange

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
