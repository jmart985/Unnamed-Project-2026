# Pull request validation

Every pull request runs the solution build and all test projects included in
`UnnamedProject2026.slnx`, imports the Godot project, then runs the session-profile,
first-game, NPC schedule, and integrated NPC schedule headless scenarios. Each
scenario must exit successfully and print its expected success marker.

The workflow saves each scenario's console output as a uniquely named GitHub Actions
artifact, including on failure. Use the failed workflow step to identify the scenario,
then download `godot-validation-logs-<run id>` for its full log. Build and test output
remains available in the workflow job log.

The focused `InteractionTests` project was removed because it was not part of the
solution and its `InteractionResolverTests` duplicated the class and assertions in
`Simulation.Tests`. The solution is now the single source of truth for which test
projects CI executes.

## Branch rule requirement

The `main` branch must require pull requests and the passing `Build / dotnet` status
check, while disallowing force pushes and branch deletion. The workflow cannot
activate repository branch protection itself. Verify the repository ruleset after
an administrator configures it; do not treat the presence of CI checks as proof that
the rule is enforced.
