# Repository working instructions

Read README.md, docs/TECHNICAL_ARCHITECTURE.md, docs/DECISIONS.md, and the assigned issue before editing code. Read docs/AI_WORKFLOW.md for current issue-selection and milestone rules.

Apply docs/IMPLEMENTATION_GUIDELINES.md when defining boundaries, data ownership, issue acceptance criteria, tests, and performance work.

- Keep work inside the assigned issue and its milestone scope. Select work from the active GitHub milestone and concrete Ready implementation issues. Milestone 0.1 documents are historical acceptance references; they are not a repository-wide restriction on later milestones.
- Follow CONTRIBUTING.md. Keep one authoritative world state and atomic inventory changes.
- Godot 4 .NET + C# is provisional. Do not silently switch engines or introduce an ECS/framework.
- Exact build/test commands are established by F01; do not invent successful validation before a scaffold exists.
- Test behavioral invariants and failure recovery. Report actual results.
- Record significant decisions and keep acceptance criteria current.
