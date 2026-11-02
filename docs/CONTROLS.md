# Unified player controls

The owner-approved layout supports keyboard/mouse and a standard controller. Direct character control remains primary. Squad and settlement commands use menus; there is no tactical camera.

## Ownership and activation

ControlCatalog is the presentation source for stable action IDs, defaults, categories, contexts and availability. Existing compatibility IDs retain their values. A reserved binding does not implement a mechanic. Unsupported actions must not execute or appear as available HUD prompts.

ControlInputPolicy resolves rebinding, text entry, modal UI, wheels, build and gameplay in that order. Both event handling and continuous input sampling must respect ownership. A consumed gesture cannot leak into the next context. Context switches, focus loss and disconnected devices cancel gestures and require affected buttons/triggers to return to neutral before another action.

## Default layout

Xbox names are used below. PlayStation equivalents: A/Cross, B/Circle, X/Square, Y/Triangle, LB/L1, RB/R1, LT/L2, RT/R2, View/touchpad button, Menu/Options.

| Action | Keyboard/mouse | Controller |
| --- | --- | --- |
| Move / look | WASD / mouse | Left / right stick |
| Jump / contextual vault | Space | A |
| Sprint | Hold Shift | L3 toggle |
| Crouch | Hold Ctrl | Hold B, 0.30 seconds |
| Dodge | Alt | Tap B, on release |
| Primary / secondary equipment action | Left / right mouse | RT / LT |
| Alternate equipment action | R | LB + RT |
| Ready / stow; switch weapon set | X; Z | Y; D-pad down |
| Target lock | Middle mouse | R3 |
| Next / previous target | T / Shift+T | Deliberate right-stick flick while locked |
| Default / additional interactions | Tap / hold E | Tap / hold X |
| Inspect | H | LB + X |
| Inventory | I | View |
| Select quickslot | 1–9, 0 | D-pad left/right |
| Quickslot wheel | Hold Tab | Hold RB |
| Use selected quickslot | F | D-pad up |
| Squad wheel | Hold Q | LB then hold RB |
| Self/social actions | G | LB + D-pad down |
| Build | B | LB + Y |
| Map; character/skills; settlement | M; K; N | LB + D-pad up; left; right |
| Journal; crafting | J; C | Corresponding menu tabs |
| First/third person; zoom | V; mouse wheel | LB + A; LB + right-stick vertical |
| Pause; slower/faster | P; minus/equal | LB + View; pause menu |
| Pause/settings; controls | Escape; F1 | Menu; pause menu |
| Quicksave; confirmed quickload | F5; F9 | Pause menu |

Sprint, crouch, dodge, weapon alternate/stance/set, quickslots, map, skills, journal and personal crafting remain reserved until their mechanics are implemented. Do not substitute an unrelated action. Existing offhand strikes remain supported; block, aim and reload are equipment capabilities, not assumptions.

## Crouch gesture

A short controller B press emits dodge only on release. At 0.30 seconds, a hold enters crouch and suppresses dodge. Default release exits crouch. Toggle Crouch instead changes stance once per qualifying hold and retains stance on release; short taps still request dodge. Keyboard Ctrl supports immediate hold-to-crouch or the selected toggle preference without delaying the independent Alt dodge.

Use unscaled monotonic time. Repeated key events do not restart the threshold. Cancellation never produces a tap. Menus and build mode own B as Back/Cancel; those gestures cannot crouch or dodge. The former LB+B crouch is removed.

## Wheels and interaction

Quickslot wheels select only. Using the selected item is a separate command. A hold interaction opens a latched contextual menu at 0.30 seconds and suppresses tap; the opener can be released before selecting. Command/quickslot wheels normally commit on opener release outside the center. Their context is latched at opening. Toggle-open accessibility uses explicit confirm/cancel.

Movement continues; mouse/right stick selects instead of turning the camera. Commands identify their recipients and use existing authority/capability/navigation services. Target interactions offer currently supported collect, greet, feed/pet, work and Follow/Stay actions. Medical, carry and additional social actions require their own gameplay implementations. Destructive actions require explicit selection/confirmation.

## Build controls

| Action | Keyboard/mouse | Controller |
| --- | --- | --- |
| Select slot | 1–9, 0 | RB build wheel; D-pad left/right |
| Place | Left mouse | RT |
| Rotate | Q / R | LT + D-pad left/right |
| Catalog | C | X |
| Snap, when supported | G | Y |
| Cancel preview, then exit | Right mouse / Escape | B |

Movement/look remain available. A fresh confirm places one instance. Rotation and snap are reserved: unfinished construction sites do not yet persist facing. They remain inactive rather than displaying a rotation that placement cannot preserve. Future rotation must use the definition's supported increment. Build and quickslot selection are separate. The full catalog pauses and retains the selected preview definition without auto-placement on return.

## Menus and preferences

Full inventory/settings/management menus pause single-player time; future map menus must use the same ownership. Live wheels/building do not. Removing a UI pause owner cannot resume a manual pause or another UI owner's pause. Keep selected simulation speed.

Menu defaults: Enter/A confirm, Escape/B back, Tab/Shift+Tab keyboard focus, Q/E or LB/RB tabs, right-click/X actions on a focused equipment slot; LT/RT cycle the item/control category where available. Equipment eye visibility is separate from equip/ownership; Y toggles a focused equipment slot.

Settings version 5 activates the new layout once and preserves the previous file as a backup. Bindings support keys, mouse, joypad buttons/axes, modifiers, gestures and alternate assignments. Allow shared bindings only in disjoint contexts or an explicit tap/hold pair. Save atomically before applying. Defaults: 0.30-second holds and 0.20 stick deadzones; sensitivity, inversion, hold/toggle and thresholds are configurable. Controls never enter world saves.

## Acceptance

Exercise actual player/menu/build dispatch with keyboard and controller. Verify B threshold boundaries and toggle behavior, no action on cancellation, no overlay-close attacks/placements, neutral rearming, device/focus recovery, context-aware conflicts, migration/write failure recovery, and manual/UI pause ownership. Register --validate-controls and its CONTROLS VALIDATION PASSED marker in Main and the CI manifest. The production routing scenario exercises nonzero controller IDs, live wheel movement, menu pause ownership, held-trigger rearming, controller build entry/cancel, interaction holds, toggle wheels and focus/device recovery. Domain tests cover tap/hold/toggle thresholds. Run `--validate-controls`, `--validate-menus`, `--validate-gameplay-actions`, `--validate-construction` and all other registered scenarios; require exit zero and each manifest marker. Rendered `--validate-controls` also writes settings/wheel captures to the system temporary directory. Synthetic input does not replace a physical-controller usability playtest.
