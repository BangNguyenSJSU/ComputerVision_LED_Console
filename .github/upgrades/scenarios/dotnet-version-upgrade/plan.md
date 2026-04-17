# .NET 8 Upgrade Plan

## Overview

**Target**: Upgrade ComputerVision_LED_Console from .NET 5.0 to .NET 8.0 LTS
**Scope**: Single console application, 22 code files, ~817 LOC, 2 NuGet packages (both compatible)

### Selected Strategy
**All-At-Once** — Single project upgraded in one operation.
**Rationale**: Single project with straightforward upgrade path. All packages (OpenCvSharp4) are compatible with .NET 8.0. No breaking API changes detected. Low complexity (0 LOC modifications estimated).

## Tasks

### 01-prerequisites: Validate Environment

Verify that .NET 8 SDK is installed and global.json (if present) allows .NET 8.

**Done when**: .NET 8 SDK confirmed available, no global.json conflicts blocking the upgrade

---

### 02-upgrade-project: Update Target Framework and Packages

Update the project file to target net8.0 and restore dependencies. Since all packages are already compatible, no package version updates are required.

**Affected**: ComputerVision_LED_Console.csproj
**Packages**: OpenCvSharp4 (4.13.0.20260330), OpenCvSharp4.runtime.win (4.13.0.20260302)

**Done when**: Project targets net8.0, solution restores and builds successfully with 0 errors

---

### 03-validate: Build and Test

Build the solution and verify all functionality works on .NET 8.

**Done when**: Solution builds with 0 errors, application runs successfully
