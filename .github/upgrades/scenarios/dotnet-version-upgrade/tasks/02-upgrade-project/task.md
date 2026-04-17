# 02-upgrade-project: Update Target Framework and Packages

Update the project file to target net8.0 and restore dependencies. Since all packages are already compatible, no package version updates are required.

**Affected**: ComputerVision_LED_Console.csproj
**Packages**: OpenCvSharp4 (4.13.0.20260330), OpenCvSharp4.runtime.win (4.13.0.20260302)

**Done when**: Project targets net8.0, solution restores and builds successfully with 0 errors
