---
description: Scaffold a new architectural layer folder with interface + stub implementation files, following the DesignPlan.md conventions.
argument-hint: <LayerName> [--with-interface] [--namespace-suffix=Name]
---

Scaffold the layer named `$1` under the project root.

Rules:
- Folder name is PascalCase (e.g., `Calibration`, `Overlay`, `Storage`).
- Namespace is `ComputerVision_LED_Console.$1`.
- Create these files, each as a minimal compileable stub:
  - `$1/I$1Service.cs` (interface — skip only if the user passes `--no-interface`)
  - `$1/$1Service.cs` (class implementing the interface, methods throw `NotImplementedException`)
  - `$1/$1Config.cs` under the existing `Config/` folder if the layer needs config
- Do NOT add magic numbers — push defaults into the matching `Config/*.cs`.
- Do NOT reference forbidden layers. Consult `.claude/agents/architecture-guardian.md` for the allowed dependency matrix.
- After writing files, run `dotnet build` and report the result.

If `$1` matches an existing folder, stop and ask the user whether to merge or rename.
