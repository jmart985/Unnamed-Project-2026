# Contributing and AI-assisted work

Use [Implementation Guidelines](docs/IMPLEMENTATION_GUIDELINES.md) to make issues testable and keep system boundaries, persistence, validation, and AI handoffs consistent.

1. Choose one ready issue and read its acceptance criteria, dependencies, and linked design docs.
2. Inspect the current implementation. Propose the smallest change that satisfies the issue; split the issue if it cannot be reviewed as one focused change.
3. Implement on a branch. Preserve documented ownership and interfaces. Record consequential design changes in docs/DECISIONS.md.
4. Run tests appropriate to changed behavior and the relevant Godot scenario. Do not claim unrun tests passed.
5. Open a PR linking the issue with `Closes #N`. Explain observable behavior, validation evidence, and known limitations. Review before merging.
6. Update docs when behavior changes. Close the issue only when its acceptance criteria are satisfied.

## Line endings

The root `.gitattributes` owns the repository's line-ending policy on every platform:

- Text files use **LF**, including C# source, Godot scenes/configuration/UID files, JSON, Markdown, and workflow files. Save edited text with LF in your editor.
- Windows `.bat` and `.cmd` scripts use **CRLF** when checked out; Git stores their normalized text with LF.
- Binary assets retain their original bytes. Git automatically detects binary files; common game asset formats are also explicitly marked binary.

These attributes take precedence over `core.autocrlf`, so contributors do not need to change their personal Git configuration. If an editor reports that an LF source file will become CRLF, reload its Git status after pulling this policy and check the effective attributes:

```powershell
git check-attr text eol -- game/scripts/Main.cs CONTRIBUTING.md
git ls-files --eol -- game/scripts/Main.cs CONTRIBUTING.md
```

Expected attributes are `text: auto` and `eol: lf`. In `ls-files --eol`, `i/lf` describes the committed/index content and `w/lf` describes the working copy. Existing working copies may still show `w/crlf` or `w/mixed`: adding attributes does not rewrite files already on disk. Save files with LF as you edit them; fresh checkouts use the declared policy. Keep any repository-wide normalization in a separate reviewed change, and preserve file encodings and unrelated local edits.

## Work states

Ideas → Backlog → Ready → In Progress → Testing → Done.

Use the GitHub Project status field when available. Ready means dependencies are complete and acceptance criteria are clear. Testing means implementation awaits verification/review. Done means accepted and merged. The active milestone and current issue list change over time; do not assume F01 or Milestone 0.1 is still the next task. See docs/AI_WORKFLOW.md.

## AI task handoff

Provide the issue URL, current branch, relevant docs, and expected validation command/scenario. Require a summary of changed behavior, tests actually run, and remaining limitations. The issue and repository are the durable context; chat history is supplemental.

