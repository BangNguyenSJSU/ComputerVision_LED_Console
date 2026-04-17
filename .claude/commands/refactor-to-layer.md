---
description: Extract a cohesive piece of logic from Program.cs into its target architectural layer, preserving behavior.
argument-hint: <target-layer> <what-to-extract>
---

Target layer: `$1`
What to extract: `$2`

Workflow:

1. **Locate** the relevant code in `Program.cs`. Read the whole method(s) involved plus their callers so you understand the inputs/outputs.
2. **Identify dependencies** the code currently has on local mutable state (`Markers`, `DragIndex`, `SelectedIndex`, `DisplayScale`). Those become constructor parameters or fields on the destination class, or move to `App/AppState.cs` if they are runtime UI state.
3. **Write the new code** in the `$1/` layer, using the existing stub file if one is already there. Respect the layer's allowed dependencies — check `.claude/agents/architecture-guardian.md`.
4. **Wire it in** via `App/AppController.cs`, not by calling from `Program.cs` directly. `Program.cs` should only be responsible for bootstrapping and invoking `AppController.Run()`.
5. **Remove the extracted code** from `Program.cs`. Do not leave dead helpers, commented-out blocks, or "removed X" notes.
6. **Build** with `dotnet build` and fix errors.
7. **Delegate** a review to the `architecture-guardian` agent. Only report success once it returns PASS.

Do not extract more than what `$2` describes — other refactors are separate passes. If the extraction requires changes outside layer `$1`, list them for the user before proceeding.
