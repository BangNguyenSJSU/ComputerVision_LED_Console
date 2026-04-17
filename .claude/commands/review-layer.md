---
description: Run architecture-guardian + csharp-reviewer in parallel against a target layer folder, collate results.
argument-hint: <LayerName>
---

Review layer: `$1/`

Run these two agents **in parallel** (single message, multiple Agent calls):

1. `architecture-guardian` — scoped to files under `$1/`. Ask for a pass/fail verdict with specific violations cited as `file:line`.
2. `csharp-reviewer` — scoped to files under `$1/`. Ask for findings grouped by severity (Blocker / Important / Nit).

After both return, collate into one report:

```
## $1/ review

### Architecture
<guardian output>

### Code quality
<reviewer output>

### Summary
<one line: "Ready to merge" OR "N blockers must be fixed">
```

If either agent reports blockers, list them at the top under "Must fix first". Do not attempt to fix anything in this command — it is read-only. The user decides what to act on.
