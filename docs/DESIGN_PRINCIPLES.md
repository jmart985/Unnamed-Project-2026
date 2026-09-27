# Design principles

1. **Use consistent rules.** People and creatures share entity, need, inventory, and navigation mechanics where appropriate. Species data supplies differences.
2. **Build interactions.** A new feature should improve an existing loop. Hunger connects to food, movement, jobs, and storage.
3. **Treat creatures as inhabitants.** Their needs and physical capabilities matter independently of player ownership.
4. **Keep player identity open.** Avoid a mandatory heroic role in future design.
5. **Preserve consequences.** Durable world changes belong in save data; transient engine objects do not define world truth.
6. **Prefer depth over quantity.** One functioning creature species is enough to validate the initial model.
7. **Make behavior inspectable.** Display an entity's current action, hunger, target, and reason for waiting. Failure must have a visible explanation.
8. **Keep changes small.** One issue should yield a reviewable change with observable acceptance criteria. Split work before broadening its scope.
9. **Use evidence to revisit decisions.** Record a problem, measured evidence, and alternatives before changing core architecture or engine.

These principles guide tradeoffs; they do not require speculative infrastructure for systems outside the milestone.
