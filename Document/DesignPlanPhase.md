# Design Plan — Phased Execution

Phase-by-phase execution plan for refactoring `Program.cs` (v0 one-file implementation) into the layered architecture declared in `Document/DesignPlan.md` and `CLAUDE.md`.

Every phase is:
- **Small** — one commit, 15–60 minutes of work.
- **Behavior-preserving** — the app runs identically at the end of every phase.
- **Independently validated** — clear expected result, clear test procedure.
- **Reversible** — `git reset --hard HEAD~1` is a safe escape hatch.

Phases ordered leaf-first: Models → Config → Camera → Vision → Services → App → Network → Utilities. Dependencies only flow inward toward leaves.

---

## Ground rules (apply to every phase)

1. **One phase = one commit.** Never mix extractions across layers.
2. **End each phase with a working app.** `dotnet build` + `dotnet run` + manual LED test before calling the phase done.
3. **No magic numbers.** Any constant from `Program.cs` moves to the matching `Config/*.cs`.
4. **Mat lifetime discipline.** Every `Mat` / `VideoCapture` gets `using` or explicit `Dispose`. Reviewed at every phase close.
5. **Review gate.** Phases touching a layer end with `/review-layer <Name>` (architecture-guardian + csharp-reviewer). Blockers must be resolved before the next phase starts.
6. **Rollback policy.** If a phase breaks behavior and the cause isn't obvious in 30 minutes, `git reset --hard HEAD~1` and re-plan.

---

## Phase summary

| # | Phase | Touches | Est. effort |
|---|---|---|---|
| 0 | .NET 8 migration | `*.csproj` | 10 min |
| 1 | Test project scaffold | `Tests/` (new) | 15 min |
| 2 | Models — `LedStatus` replaces strings | `Models/`, `Program.cs` | 20 min |
| 3 | Config centralization | `Config/`, `Program.cs` | 30 min |
| 4 | Camera — `OpenCvCameraSource` impl | `Camera/`, `Program.cs` | 45 min |
| 5 | Camera — `FakeCameraSource` for tests | `Tests/` | 20 min |
| 6 | Vision — hysteresis + ROI helpers | `Vision/`, `Program.cs` | 45 min |
| 7 | Vision — `AutoDetect` | `Vision/`, `Program.cs` | 30 min |
| 8 | Services — `StatusService` wired | `Services/`, `Utilities/`, `Program.cs` | 30 min |
| 9 | App — `OverlayRenderer` | `App/`, `Program.cs` | 30 min |
| 10 | App — `InputHandler` | `App/`, `Program.cs` | 30 min |
| 11 | App — `AppController` + `Program.cs` shrink | `App/`, `Program.cs` | 30 min |
| 12 | Network — HTTP `/status` endpoint | `Network/`, `App/` | 45 min |
| 13 | Utilities — logging hardening | all layers | 20 min |
| 14 | Network — TCP publisher *(optional)* | `Network/`, `App/` | 45 min |
| 15 | v1 sign-off | all layers | 15 min |

Total core path (phases 0–13 + 15): ≈ 6 hours of focused work.

---

## Phase 0 — .NET 8 migration

**Goal.** Move target framework from `net5.0` to `net8.0`.

**Why this is first.** `net5.0` is end-of-life and gives build warnings. The refactor uses modern idioms (file-scoped namespaces, nullable reference types) that work better on .NET 8.

**Prerequisites.** VS 2026 installed, `.NET 8 SDK` on `PATH`. Confirm with `dotnet --info` showing an 8.x SDK.

**TODO**
- [ ] Open `ComputerVision_LED_Console.csproj`.
- [ ] Change `<TargetFramework>net5.0</TargetFramework>` → `<TargetFramework>net8.0</TargetFramework>`.
- [ ] Inside the same `<PropertyGroup>`, add:
  - `<Nullable>enable</Nullable>`
  - `<ImplicitUsings>enable</ImplicitUsings>`
- [ ] Run `dotnet restore`.
- [ ] Run `dotnet build`.

**Expected result**
- Build succeeds with 0 errors and 0 warnings.
- The `net5.0` EOL warning is gone.
- OpenCvSharp packages resolve against `net8.0` target.

**How to validate**
```bash
dotnet --info                # confirms 8.x SDK
dotnet restore
dotnet build                 # 0 errors, 0 warnings
dotnet run                   # app launches, camera opens, LED detection works
```
Visual test: point webcam at yellow LED, toggle power, confirm ON/OFF label flips.

**Commit**
```
chore: migrate target framework to net8.0

- Enable nullable reference types and implicit usings
- Drop EOL net5.0 warning
```

**Rollback.** `git reset --hard HEAD~1` then re-edit csproj.

---

## Phase 1 — Test project scaffold

**Goal.** Create an empty xUnit test project that builds and runs.

**Why this is next.** Later phases will add tests. The project should exist before we need it so each refactor phase can ship with its test in the same commit.

**Prerequisites.** Phase 0 complete.

**TODO**
- [ ] From the repo root, run `dotnet new xunit -o Tests/ComputerVision_LED_Console.Tests -f net8.0`.
- [ ] Add project reference back to main:
  `dotnet add Tests/ComputerVision_LED_Console.Tests reference ComputerVision_LED_Console.csproj`
- [ ] Add test project to solution:
  `dotnet sln add Tests/ComputerVision_LED_Console.Tests/ComputerVision_LED_Console.Tests.csproj`
- [ ] Open `Tests/ComputerVision_LED_Console.Tests/UnitTest1.cs`, rename to `SmokeTests.cs`, rename class to `SmokeTests`, rename method to `TestHarnessIsAlive`, assert `true`.
- [ ] Confirm `Tests/` is already covered by `.gitignore`'s `[Bb]in/` and `[Oo]bj/` rules (no change expected).

**Expected result**
- `Tests/ComputerVision_LED_Console.Tests/` folder exists.
- Solution contains two projects.
- One trivial passing test.

**How to validate**
```bash
dotnet build                 # both projects build
dotnet test                  # 1 test passed
dotnet sln list              # shows both projects
```

**Commit**
```
test: add xunit test project skeleton
```

**Rollback.** `git reset --hard HEAD~1` + `rm -rf Tests/`.

---

## Phase 2 — Models: replace string state with `LedStatus`

**Goal.** Replace `"ON"` / `"OFF"` string state in `Program.cs` with the `LedStatus` enum and confirm `DetectionResult` / `SystemStatus` shape.

**Why this is small.** Leaf layer, no dependencies on other layers. The change is mechanical — one type substitution.

**Prerequisites.** Phase 1 complete.

**TODO**
- [ ] Verify `Models/LedStatus.cs` has values `Unknown = 0, Off = 1, On = 2`. Add XML doc comment on the enum explaining *why* `Unknown = 0` (default-safe).
- [ ] Verify `Models/DetectionResult.cs` has `MarkerId`, `Status`, `Brightness`, `TimestampUtc`.
- [ ] Verify `Models/SystemStatus.cs` has `TimestampUtc`, camera info, `List<DetectionResult> Leds`.
- [ ] In `Program.cs`, add `using ComputerVision_LED_Console.Models;`.
- [ ] In the nested `LedMarker` class, change `public string State = "OFF";` to `public LedStatus State = LedStatus.Off;`.
- [ ] In the main loop, replace:
  - `m.State == "OFF"` → `m.State == LedStatus.Off`
  - `m.State == "ON"` → `m.State == LedStatus.On`
  - `m.State = "ON"` → `m.State = LedStatus.On`
  - `m.State = "OFF"` → `m.State = LedStatus.Off`
- [ ] In label rendering, use `m.State.ToString().ToUpperInvariant()` so the on-screen text still reads `ON` / `OFF` (temporary bridge — later phases clean this up).
- [ ] Add `Tests/Models/LedStatusTests.cs`:
  - `DefaultValue_IsUnknown` — `default(LedStatus)` equals `LedStatus.Unknown`.
- [ ] Add `Tests/Models/DetectionResultTests.cs`:
  - `DefaultConstruction_HasUnknownStatus` — new `DetectionResult()` has `Status == Unknown`.

**Expected result**
- No string literals `"ON"` or `"OFF"` remain in `Program.cs` comparisons or assignments (only in display text via `ToString`).
- 2 new test files, 2 passing tests.

**How to validate**
```bash
dotnet build
dotnet test                  # 3 tests passed (1 smoke + 2 models)
dotnet run                   # app runs, label text still reads ON/OFF
```
Grep check:
```bash
grep -nE '"ON"|"OFF"' Program.cs
# Should return nothing or only display-text lines, never comparisons.
```

**Commit**
```
refactor(models): introduce LedStatus enum in Program.cs
```

**Rollback.** Safe — change is localized.

---

## Phase 3 — Config centralization

**Goal.** Move every constant at the top of `Program.cs` into the matching `Config/*.cs`. Pure mechanical move — no behavior change.

**Why this is next.** Leaf layer. Unblocks later phases that need to inject config into `OpenCvCameraSource` and `LedDetector`.

**Prerequisites.** Phase 2 complete.

**TODO**
- [ ] Verify `Config/CameraConfig.cs` defaults match `Program.cs:59-63` (1920×1080, 60fps, MJPG, buffer=1).
- [ ] Verify `Config/DetectionConfig.cs` defaults match `Program.cs:11-34` (thresholds 150/120, gap 5, step 5, margin 10, radii 4/60/20, HSV 20-35 / 100-255 / 150-255).
- [ ] Verify `Config/NetworkConfig.cs` has sensible defaults (already drafted).
- [ ] Verify `Config/AppConfig.cs` composes all three.
- [ ] In `Program.cs` `Main`, at the top, construct: `var config = new AppConfig();`.
- [ ] Replace every inline constant reference in `Program.cs` with `config.Camera.X`, `config.Detection.X`.
- [ ] Delete the now-unused `const` and `static readonly` declarations at the top of `Program.cs` (lines ~11-34).
- [ ] The nested `LedMarker.DefaultOn` / `DefaultOff` references become `config.Detection.DefaultOnThreshold` / `DefaultOffThreshold` where used.
- [ ] HSV scalars: build `new Scalar(config.Detection.HueLow, config.Detection.SaturationLow, config.Detection.ValueLow)` inside `DetectYellowLeds`.

**Expected result**
- `Program.cs` contains zero numeric literals for thresholds, HSV bounds, resolution, FPS, radii.
- `AppConfig` built once at startup, threaded into the helpers that need it.

**How to validate**
```bash
dotnet build
dotnet test                  # still 3 passing
dotnet run                   # identical detection behavior
```
Grep check:
```bash
grep -nE '150\.0|120\.0|1920|1080|"MJPG"|\bScalar\(20' Program.cs
# Should return nothing.
```
Then invoke review:
```
/review-layer Config
```
Must return PASS.

**Commit**
```
refactor(config): centralize camera, detection, network defaults
```

**Rollback.** Safe — constants are just moved, not transformed.

---

## Phase 4 — Camera layer: `OpenCvCameraSource` implementation

**Goal.** Move `VideoCapture` lifecycle and `SelectCamera` logic behind `ICameraSource`.

**Why this is a standalone phase.** The camera layer is cohesive: open, read, close, dispose. Testing comes in Phase 5 via `FakeCameraSource`.

**Prerequisites.** Phase 3 complete.

**TODO**
- [ ] Implement `Camera/FrameData.cs` (already stubbed as `Mat + DateTime + IDisposable` — verify it compiles as-is).
- [ ] Implement `Camera/OpenCvCameraSource.cs`:
  - Private fields: `VideoCapture? _capture`, current width/height/fps.
  - `Open()`:
    - Runs the camera-probe loop from `Program.cs:381-438` (refactored — see next bullet).
    - Opens the chosen index with `VideoCaptureAPIs.DSHOW`.
    - Applies FOURCC/width/height/FPS/buffer from `_config`.
    - Reads back actual width/height/fps from the device.
    - Returns `true` on success.
  - `TryReadFrame(out FrameData? frame)`:
    - Allocates a new `Mat`, reads into it.
    - If empty, disposes the Mat and returns `false` with `frame = null`.
    - Else wraps in `FrameData(mat, DateTime.UtcNow)` and returns `true`.
  - `Close()`: releases `_capture`, sets `IsOpen = false`.
  - `Dispose()`: calls `Close()`, disposes `_capture`.
- [ ] Create `Camera/CameraProbe.cs` — static helper:
  - `ScanAvailable(int maxIndexToProbe)` — returns `List<int>` of indices with working frames. Port from `Program.cs:381-400`.
  - `PromptUserSelection(IReadOnlyList<int> available)` — interactive console prompt. Port from `Program.cs:408-437`. Returns chosen index or `-1`.
- [ ] `OpenCvCameraSource.Open()` calls `CameraProbe.ScanAvailable` then `CameraProbe.PromptUserSelection`.
- [ ] In `Program.cs`, replace the inline `SelectCamera` call + `VideoCapture` construction with:
  ```csharp
  using var camera = new OpenCvCameraSource(config.Camera);
  if (!camera.Open()) return;
  ```
- [ ] Delete `SelectCamera`, the `VideoCapture` setup block, and the camera-info printouts from `Program.cs` (main loop keeps its own `using var frame = new Mat();` for now — Phase 7 migrates that).
- [ ] Inside main loop, replace `capture.Read(frame)` with:
  ```csharp
  if (!camera.TryReadFrame(out var frameData)) continue;
  using var frame = frameData!.Frame;  // shared reference; loop-local disposal
  ```
  (Careful: `FrameData` owns its `Mat` — decide once who disposes. Pattern: `FrameData.Dispose()` disposes `Mat`; the loop does `using var frameData = ...`.)

**Expected result**
- `Program.cs` no longer constructs `VideoCapture` directly.
- `OpenCvCameraSource` owns the full capture lifecycle.
- `CameraProbe` is reusable for future auto-select features.

**How to validate**
```bash
dotnet build
dotnet test                  # still 3 passing (no Camera tests yet — Phase 5)
dotnet run                   # camera probe lists cameras, selection works, capture runs at configured resolution
```
Manual checks:
- Disconnect camera mid-run → `TryReadFrame` returns `false`, app does not crash.
- Quit with `Q` → app exits cleanly, no OpenCvSharp disposal warnings.

```
/review-layer Camera
```
Must return PASS.

**Commit**
```
refactor(camera): extract capture lifecycle behind ICameraSource
```

**Rollback.** Phase is isolated to the Camera layer and one `Program.cs` callsite. `git reset --hard HEAD~1` is clean.

---

## Phase 5 — Camera: `FakeCameraSource` for tests

**Goal.** Provide a test double that replays frames from disk so Vision tests do not need a real webcam.

**Why it's small.** One file, no production code touched, used by later phases.

**Prerequisites.** Phase 4 complete.

**TODO**
- [ ] Create `Tests/Fixtures/` folder. Add 2 PNGs for now (placeholders acceptable until Phase 7 writes Vision tests):
  - `led_on_yellow.png` — frame containing a clearly-lit yellow LED.
  - `led_off_dark.png` — same scene with the LED dark.
  - *(If real fixtures are not ready, commit 2 solid-color 640×480 PNGs generated via `Cv2.ImWrite` in a tiny throwaway script — or synthesize them in-memory in Phase 7.)*
- [ ] Create `Tests/Camera/FakeCameraSource.cs`:
  - Implements `ICameraSource`.
  - Constructor takes `IReadOnlyList<string> framePaths` + optional `bool loop = true`.
  - `Open()`: sets `IsOpen = true`, resets index.
  - `TryReadFrame(out FrameData?)`: reads the next image file via `Cv2.ImRead`, wraps in `FrameData`, advances index (wraps if `loop`). Returns `false` when exhausted and not looping.
  - `FrameWidth/Height`: taken from the first frame at `Open()`.
- [ ] Create `Tests/Camera/FakeCameraSourceTests.cs`:
  - `TryReadFrame_ReturnsFramesInOrder` — feed 2 fixtures, read twice, verify both frames returned.
  - `TryReadFrame_ReturnsFalseWhenExhausted` — non-looping, read past end, expect `false`.
  - `Loop_RewindsToFirstFrame` — looping, read past end, next read returns first frame.

**Expected result**
- `FakeCameraSource` available for downstream Vision tests.
- 3 new passing tests.

**How to validate**
```bash
dotnet test                  # 6 tests passing
```
Confirm `Tests/Fixtures/*.png` are committed (not gitignored).

**Commit**
```
test(camera): add FakeCameraSource backed by fixture frames
```

**Rollback.** Test-only phase. Safe.

---

## Phase 6 — Vision: hysteresis + ROI helpers

**Goal.** Move hysteresis evaluation, gap enforcement, ROI clamping, and marker-lookup from `Program.cs` into `Vision/LedDetector` + helpers.

**Why this is split from AutoDetect.** Hysteresis is the critical correctness piece; it deserves tight test coverage independent of the noisier detection pipeline.

**Prerequisites.** Phase 5 complete.

**TODO**
- [ ] Replace `Program.cs`'s nested `LedMarker` class: move its runtime fields (`Center`, `Radius`, `State`, `OnThreshold`, `OffThreshold`, `LastBrightness`, `CalibrationPhase`) into `Vision/RoiConfig.cs`. Per-LED defaults come from `config.Detection`.
- [ ] Implement in `Vision/LedDetector.cs`:
  - `Evaluate(FrameData frame, RoiConfig roi)`:
    - Build bounding `Rect` from roi center ± radius.
    - Call `ClampRectToFrame(rect, frame.Width, frame.Height)`.
    - If clamped rect has zero area, return `DetectionResult` with `Status = Unknown`.
    - `using var roiMat = new Mat(frame.Frame, safe);`
    - `using var gray = new Mat(); Cv2.CvtColor(roiMat, gray, BGR2GRAY);`
    - `var brightness = Cv2.Mean(gray).Val0;`
    - Update `roi.LastBrightness = brightness`.
    - Apply hysteresis: OFF→ON at `brightness >= OnThreshold`, ON→OFF at `brightness <= OffThreshold`.
    - Return `DetectionResult { MarkerId = roi.Id, Status = roi.State, Brightness = brightness, TimestampUtc = frame.TimestampUtc }`.
  - `EvaluateAll(FrameData frame)`: loop through `_rois`, call `Evaluate`, collect results.
  - `EnforceThresholdGap(RoiConfig roi, bool adjustOn)` — port from `Program.cs:359-364`.
  - `ClampRectToFrame(Rect, int width, int height)` — port from `Program.cs:440-451`. Private static helper.
  - `FindRoiAt(float x, float y)` — port from `Program.cs:366-379`. Returns index or `-1`.
  - Public mutators: `AddRoi(RoiConfig)`, `RemoveRoiAt(int)`, `TuneOnThreshold(int roiIndex, double delta)`, `TuneOffThreshold(int, double)`, `BeginCalibration(int)`, `FinishCalibration(int)`.
- [ ] In `Program.cs`, replace all direct hysteresis math with calls to `detector.EvaluateAll(frameData)` and iterate `DetectionResult`s for rendering.
- [ ] Remove the now-unused nested `LedMarker` class.
- [ ] Tests — `Tests/Vision/LedDetectorHysteresisTests.cs`:
  - `[Theory]` with `InlineData` table of brightness sequences and expected state transitions. Cover: OFF→ON at `On`, ON→OFF at `Off`, no flicker in the gap band, Off-at-exact-threshold, On-at-exact-threshold.
  - `EnforceThresholdGap_NarrowsOff_WhenAdjustOnFalse`.
  - `EnforceThresholdGap_RaisesOn_WhenAdjustOnTrue`.
  - `ClampRectToFrame_ReturnsEmpty_WhenRectFullyOutside`.
  - `FindRoiAt_ReturnsIndex_WhenInsideRadius`.
  - `FindRoiAt_ReturnsMinusOne_WhenOutside`.
- [ ] All tests use synthetic `Mat`s built with `new Mat(h, w, MatType.CV_8UC3, scalar)` — no fixture images needed for this phase.

**Expected result**
- Detection behavior at runtime is unchanged (same thresholds, same transitions).
- Hysteresis logic is tested at the unit level.
- `Program.cs` main loop shrinks noticeably — no more inline `Mat` gray conversions.

**How to validate**
```bash
dotnet build
dotnet test                  # ≥13 tests passing (6 prior + 7 new)
dotnet run                   # identical ON/OFF behavior, interactive controls still work
```
Manual: place LED in frame, tune thresholds via `[` `]` `;` `'` — verify nudges still take effect.

```
/review-layer Vision
```
Must return PASS (or only Nit-severity findings).

**Commit**
```
refactor(vision): extract hysteresis evaluation into LedDetector
```

**Rollback.** Medium risk — most of the hot-path logic moves. Keep the commit atomic; if validate fails, reset and split into finer sub-steps (e.g., extract `EnforceThresholdGap` alone first).

---

## Phase 7 — Vision: `AutoDetect` extraction

**Goal.** Move the HSV-based yellow LED auto-detection pipeline out of `Program.cs`.

**Why this is separate from Phase 6.** AutoDetect runs once at startup (or on `R`-rescan); hysteresis runs every frame. Different call patterns, different test needs, different risk — split for clean rollback.

**Prerequisites.** Phase 6 complete.

**TODO**
- [ ] Implement `LedDetector.AutoDetect(FrameData frame)`:
  - Port `DetectYellowLeds` logic from `Program.cs:258-305`.
  - Use `_config.HueLow/High`, `SaturationLow/High`, `ValueLow/High` instead of the inline `Scalar`.
  - Use `_config.MinDetectRadius` / `MaxDetectRadius` for filter.
  - Use `_config.DefaultOnThreshold` / `DefaultOffThreshold` when creating new `RoiConfig`s.
  - Dedup check identical to existing (squared-distance against existing radius).
  - Log detected count via `Logger.Info` (not `Console.WriteLine`).
- [ ] In `Program.cs`, replace initial `DetectYellowLeds(frame)` call and the `R`-key handler with `detector.AutoDetect(frameData)`.
- [ ] Remove the local `DetectYellowLeds` method from `Program.cs`.
- [ ] Tests — `Tests/Vision/LedDetectorAutoDetectTests.cs`:
  - `AutoDetect_OnFixtureWithOneLed_Adds1Marker` — use `Tests/Fixtures/led_on_yellow.png`.
  - `AutoDetect_OnFixtureWithNoLeds_AddsNoMarkers` — use a solid-black 640×480 synthetic Mat.
  - `AutoDetect_CalledTwice_DoesNotDuplicateMarkers` — regression for dedup logic.
  - `AutoDetect_FiltersTooSmallContours` — synthetic image with a 1-px yellow dot, assert no marker created.

**Expected result**
- `Program.cs` no longer contains any HSV / contour / morphology code.
- AutoDetect has ≥4 unit tests.

**How to validate**
```bash
dotnet build
dotnet test                  # ≥17 tests passing
dotnet run                   # on startup + on R-rescan, detection behavior identical
```

```
/review-layer Vision
```
Must return PASS.

**Commit**
```
refactor(vision): extract AutoDetect into LedDetector
```

**Rollback.** Low risk — AutoDetect is isolated.

---

## Phase 8 — Services: `StatusService` wired into the capture loop

**Goal.** Publish each frame's detection results into a thread-safe `StatusService` so the Network layer (later phases) has a read source.

**Prerequisites.** Phase 7 complete.

**TODO**
- [ ] Verify `Services/StatusService.cs` uses lock-based `Update(SystemStatus)` and `GetLatest()` (already stubbed).
- [ ] Modify `GetLatest()` to return a deep-copied `SystemStatus` (new list with copied `DetectionResult`s) so callers cannot mutate stored state.
- [ ] Add `StatusService.StatusUpdated` event (`EventHandler<SystemStatus>`) fired inside the lock *after* state is updated. Used by Phase 14's TCP publisher.
- [ ] Flesh out `Utilities/TimeProvider.cs` — `ITimeProvider` + `SystemTimeProvider` (already stubbed — no change expected).
- [ ] Inject `ITimeProvider` into `LedDetector` constructor. Use `_time.UtcNow` for `DetectionResult.TimestampUtc`.
- [ ] In `Program.cs`:
  - Construct `var status = new StatusService();` and `var time = new SystemTimeProvider();`.
  - Pass `time` into `LedDetector`.
  - After `detector.EvaluateAll(...)`, build a `SystemStatus { TimestampUtc = time.UtcNow, FrameWidth = camera.FrameWidth, FrameHeight = camera.FrameHeight, CameraIndex = <chosen>, FramesPerSecond = camera.FramesPerSecond, Leds = results.ToList() }` and call `status.Update(systemStatus)`.
- [ ] Tests — `Tests/Services/StatusServiceTests.cs`:
  - `Update_FollowedByGetLatest_ReturnsSameData`.
  - `GetLatest_ReturnsSnapshot_MutatingItDoesNotAffectStore`.
  - `StatusUpdated_FiresAfterUpdate`.
  - `ParallelWritersReaders_DoesNotThrow` — 1000 writers + 1000 readers via `Parallel.For`, no exceptions.

**Expected result**
- `StatusService.GetLatest()` returns fresh data every frame.
- Timestamps in `DetectionResult` are sourced from `ITimeProvider`, not `DateTime.UtcNow` directly.

**How to validate**
```bash
dotnet build
dotnet test                  # ≥21 tests passing
dotnet run                   # behavior unchanged
```
Temporary probe: after `status.Update` call, add a `if (frameCount % 60 == 0) Console.WriteLine(JsonSerializer.Serialize(status.GetLatest()));` and confirm JSON looks right. Remove before committing.

```
/review-layer Services
```
Must return PASS.

**Commit**
```
feat(services): publish SystemStatus snapshots via StatusService
```

**Rollback.** Low risk — Services is a leaf that gets written to but nothing reads from yet.

---

## Phase 9 — App: `OverlayRenderer`

**Goal.** Move display-frame rendering (circles, labels, HUD text, downscale) out of `Program.cs`.

**Prerequisites.** Phase 8 complete.

**TODO**
- [ ] Create `App/OverlayRenderer.cs`:
  - Constructor takes `DetectionConfig config`, `double displayScale`.
  - `Render(Mat displayFrame, IReadOnlyList<RoiConfig> rois, IReadOnlyList<DetectionResult> results, int selectedIndex, bool inCalibrationPhase1)`:
    - Loop through ROIs + results; draw circle + label via port from `Program.cs:130-151`.
    - Draw count + help HUD (ports from `Program.cs:154-170`).
    - Draw calibration prompt if applicable (`Program.cs:172-183`).
  - `ApplyDownscale(Mat displayFrame, out Mat shown)` — ports `Program.cs:187-196`. Returns whether `shown` is a new Mat the caller must dispose.
- [ ] In `Program.cs`, replace the inline drawing block with `renderer.Render(...)` and `renderer.ApplyDownscale(...)`.
- [ ] Dispose the downscaled Mat correctly: `using var shown = renderer.ApplyDownscale(displayFrame);` (or similar with clear ownership).
- [ ] No new tests for renderer — visual validation only.

**Expected result**
- `Program.cs` no longer contains `Cv2.Circle`, `Cv2.PutText`, `Cv2.Resize` in the main loop.
- Display visuals identical to before.

**How to validate**
```bash
dotnet build
dotnet test                  # 21 tests still passing
dotnet run                   # visually identical overlay, same font sizes, same HUD layout
```
Side-by-side screenshot comparison against a pre-phase screenshot is ideal.

```
/review-layer App
```
Must return PASS.

**Commit**
```
refactor(app): extract overlay rendering into OverlayRenderer
```

**Rollback.** Visual-only impact — safe to reset if layout regresses.

---

## Phase 10 — App: `InputHandler`

**Goal.** Move mouse + keyboard handling out of `Program.cs`.

**Prerequisites.** Phase 9 complete.

**TODO**
- [ ] Create `App/InputHandler.cs`:
  - Constructor takes `LedDetector detector`, `AppState state`.
  - `OnMouse(MouseEventTypes, int x, int y, MouseEventFlags, IntPtr)`:
    - Ports `Program.cs:307-357`.
    - Uses `state.DisplayScale` for coordinate unscaling.
    - Uses `detector.AddRoi / RemoveRoiAt / FindRoiAt` instead of touching a static list.
  - `HandleKey(int key)`:
    - Ports key dispatch from `Program.cs:198-251`.
    - Returns `bool shouldQuit` so the main loop can break.
    - For calibration / threshold nudges, calls `detector.TuneOnThreshold`, `detector.BeginCalibration`, etc.
- [ ] `App/AppState.cs` already has `SelectedMarkerIndex`, `DragMarkerIndex`, `DisplayScale` — no change expected.
- [ ] In `Program.cs`, replace the inline mouse callback and key dispatcher with:
  ```csharp
  var input = new InputHandler(detector, appState);
  Cv2.SetMouseCallback(WindowName, input.OnMouse);
  // inside loop:
  if (input.HandleKey(Cv2.WaitKey(1))) break;
  ```
- [ ] Remove the static `OnMouse`, `DragIndex`, `SelectedIndex`, `Markers` from `Program.cs` (all now live in `LedDetector` + `AppState`).

**Expected result**
- All interactive controls work identically (left-click add/drag, right-click delete, `C` calibrate, `[]` tune On, `;'` tune Off, `R` rescan, `Q` quit).
- `Program.cs` has no keyboard/mouse callbacks of its own.

**How to validate**
```bash
dotnet build
dotnet test
dotnet run                   # every control works as before
```
Manual checklist — exercise every keybinding at least once.

```
/review-layer App
```
Must return PASS.

**Commit**
```
refactor(app): extract mouse and keyboard handling into InputHandler
```

**Rollback.** Medium risk — user interaction regressions only surface during manual test. If anything misbehaves, reset.

---

## Phase 11 — App: `AppController` + `Program.cs` shrink

**Goal.** `Program.cs` becomes a ~20-line bootstrap. All loop logic lives in `AppController.Run()`.

**Prerequisites.** Phase 10 complete.

**TODO**
- [ ] Implement `App/AppController.cs`:
  - Constructor takes `AppConfig`, `ICameraSource`, `LedDetector`, `StatusService`, `OverlayRenderer`, `InputHandler`, `ITimeProvider`, plus optional `IReadOnlyList<IStatusPublisher>`.
  - `Run()`:
    - Open camera; return on failure with log.
    - Register mouse callback.
    - `Cv2.NamedWindow(WindowName)`.
    - Start all publishers.
    - Main loop:
      - `if (!camera.TryReadFrame(out var frameData)) continue;`
      - `using (frameData)` …
      - On first frame, call `detector.AutoDetect(frameData)`.
      - Evaluate, build `SystemStatus`, `status.Update(...)`.
      - Clone frame for display, renderer.Render, renderer.ApplyDownscale, `Cv2.ImShow`.
      - `if (input.HandleKey(Cv2.WaitKey(1))) break;`
      - If `R` pressed: `detector.Clear(); detector.AutoDetect(frameData);`.
    - On exit: stop publishers, dispose all.
  - `Stop()`: flips `AppState.IsRunning = false`.
- [ ] Rewrite `Program.cs` to ~20 lines:
  ```csharp
  using ComputerVision_LED_Console.App;
  using ComputerVision_LED_Console.Camera;
  using ComputerVision_LED_Console.Config;
  using ComputerVision_LED_Console.Services;
  using ComputerVision_LED_Console.Utilities;
  using ComputerVision_LED_Console.Vision;

  var config = new AppConfig();
  var time = new SystemTimeProvider();
  using var camera = new OpenCvCameraSource(config.Camera);
  var detector = new LedDetector(config.Detection, time);
  var status = new StatusService();
  var state = new AppState();
  var renderer = new OverlayRenderer(config.Detection, state);
  var input = new InputHandler(detector, state);
  var app = new AppController(config, camera, detector, status, renderer, input, time);
  app.Run();
  ```

**Expected result**
- `Program.cs` ≤ 25 lines.
- `AppController.Run()` is the main loop.
- All behavior preserved.

**How to validate**
```bash
wc -l Program.cs             # ≤ 25
dotnet build
dotnet test
dotnet run                   # full manual exercise
```

```
/review-layer App
```
Must return PASS.

**Commit**
```
refactor(app): wire AppController and shrink Program.cs to a bootstrap
```

**Rollback.** Keep pre-phase Program.cs backup; if the shrink regresses anything, reset.

---

## Phase 12 — Network: HTTP `/status` endpoint

**Goal.** Serve `SystemStatus` as JSON over HTTP for localhost integration.

**Prerequisites.** Phase 11 complete. *(`StatusService` is the only dependency — HTTP does not touch OpenCvSharp.)*

**TODO**
- [ ] Implement `Network/HttpStatusServer.cs`:
  - Fields: `HttpListener _listener`, `Task? _acceptTask`, `CancellationTokenSource? _cts`.
  - `Start()`: configure listener on `config.HttpBindAddress:Port`, `Start()`, spawn accept loop task.
  - Accept loop (async): on each request, route `GET /status` → 200 JSON of `_status.GetLatest()`; anything else → 404 or 405.
  - JSON options: `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } }`.
  - `Stop()`: cancel `_cts`, call `_listener.Stop()`, await accept task, `_listener.Close()`.
  - `Dispose()` calls `Stop()`.
- [ ] `AppController` constructs `HttpStatusServer` only if `config.Network.HttpEnabled`. Passes it into the publisher list.
- [ ] Default: `NetworkConfig.HttpEnabled = false` — do **not** open a port unless opted in.
- [ ] Tests — `Tests/Network/HttpStatusServerTests.cs`:
  - `Get_Status_ReturnsJsonOfLatest`. Spin up server on a random high port with a pre-seeded `StatusService`, use `HttpClient.GetAsync`, parse JSON, assert shape.
  - `Get_Unknown_Returns404`.
  - `Post_Status_Returns405`.
  - `Stop_ClosesListener`.

**Expected result**
- With `HttpEnabled = true`, `curl http://127.0.0.1:8080/status` returns JSON.
- Default behavior unchanged (HTTP disabled).

**How to validate**
```bash
dotnet build
dotnet test                  # ≥25 tests passing
```
Temporarily flip `HttpEnabled = true` in `Program.cs`, run, then in another terminal:
```bash
curl -sS http://127.0.0.1:8080/status | jq .
# Expect camelCase JSON with leds array, timestampUtc, camera info.
```
Revert the flip before committing.

```
/review-layer Network
```
Must return PASS — specifically verify no `using OpenCvSharp` in any Network file.

**Commit**
```
feat(network): add HTTP GET /status endpoint
```

**Rollback.** Self-contained — Network layer doesn't touch the capture loop except for the publisher start/stop hook.

---

## Phase 13 — Utilities: logging hardening

**Goal.** Route all logging through `Utilities/Logger`. Confirm the architectural contract holds.

**Prerequisites.** Phase 12 complete.

**TODO**
- [ ] Grep: `grep -nR "Console.WriteLine" --include="*.cs" .` — enumerate every call site outside `Program.cs`, `App/AppController.cs`, `Camera/CameraProbe.cs` (the probe's interactive prompt is legitimately a console operation), and `Utilities/Logger.cs`.
- [ ] Replace each with `Logger.Info` / `Warn` / `Error`.
- [ ] Verify the probe is the only layer-level code calling `Console.WriteLine` / `Console.ReadLine` — that is an acceptable exception because the probe is the interactive camera selector.
- [ ] Run both guardian and reviewer **project-wide**:
  - `Agent(architecture-guardian)` — prompt: "Enforce layer boundaries across the entire project, not a single folder."
  - `Agent(csharp-reviewer)` — prompt: "Review every .cs file for Mat lifetime, thread-safety, nullability, and hot-path allocation."
- [ ] Fix any blockers.

**Expected result**
- Guardian returns PASS with zero violations.
- Reviewer returns zero Blockers; Important findings addressed or consciously deferred.
- Logger is the single logging funnel.

**How to validate**
```bash
grep -nR "Console.WriteLine" --include="*.cs" . | grep -vE "(Program\.cs|App/AppController|Camera/CameraProbe|Utilities/Logger)"
# Should return nothing.
dotnet build
dotnet test                  # still green
```

**Commit**
```
chore: route logging through Logger and fix final layer violations
```

**Rollback.** Easy — Logger calls are mechanical substitutions.

---

## Phase 14 — Network: TCP publisher *(optional, can ship post-v1)*

**Goal.** Stream line-delimited JSON to TCP clients on every status update.

**Prerequisites.** Phase 13 complete.

**TODO**
- [ ] Implement `Network/TcpStatusServer.cs`:
  - `TcpListener` on `config.TcpBindAddress:Port`.
  - Accept loop spawns per-client task.
  - Subscribes to `StatusService.StatusUpdated`; writes one JSON line per update to every connected client. Disconnects dead clients silently.
- [ ] `AppController` starts/stops it only if `config.Network.TcpEnabled`.
- [ ] Tests — `Tests/Network/TcpStatusServerTests.cs`:
  - `ConnectedClient_ReceivesJsonLine_OnStatusUpdate`.
  - `DeadClient_DoesNotBlockOthers`.

**Expected result**
- `ncat 127.0.0.1 9090` prints a JSON line per status change when enabled.

**How to validate**
```bash
dotnet test
# Manual:
ncat 127.0.0.1 9090          # in one terminal
dotnet run                    # in another, with TcpEnabled=true
```

**Commit**
```
feat(network): add TCP line-delimited JSON publisher
```

---

## Phase 15 — v1 sign-off

**Goal.** Close out the refactor with a clean audit.

**Prerequisites.** Phases 0–13 complete. Phase 14 optional.

**TODO**
- [ ] Run `/review-layer` against every folder: Models, Config, Camera, Vision, Services, App, Network, Utilities.
- [ ] Confirm the empty legacy `ComputerVision_LED_Console/ComputerVision_LED_Console/` subfolder is deleted (ask user to confirm first if still present).
- [ ] Final full build: `dotnet build` → 0 errors, 0 warnings.
- [ ] Final full test run: `dotnet test` → all green.
- [ ] Final manual smoke: launch app, auto-detect LEDs on a known scene, toggle, verify ON/OFF. Exercise every keybinding.
- [ ] Tag: `git tag v1.0.0-refactor-complete && git push --tags`.

**Expected result**
- Zero architecture violations.
- All tests passing.
- Program.cs ≤ 25 lines.
- Codebase matches `Document/DesignPlan.md` exactly.

**How to validate**
The audit checklist above is the validation.

**Commit**
No commit for this phase — only a tag.

---

## Out of scope for v1

- GUI (WinForms/WPF) — stubs only. v2 concern.
- Config file loading (JSON/YAML) — `AppConfig` is code-configured. Trivial to add later.
- Multi-camera support — `ICameraSource` permits it, v1 uses one source.
- Calibration persistence across runs — in-memory only.
- `gRPC`, WebSocket, SignalR publishers — design supports them but they are not requirements.

---

## Quick reference — dependency matrix

```
Program.cs      →  App
App             →  Camera, Vision, Services, Network, Config, Models, Utilities
Camera          →  Config, Models, Utilities, OpenCvSharp
Vision          →  Camera (FrameData), Config, Models, Utilities, OpenCvSharp
Services        →  Models, Utilities
Network         →  Config, Services, Models, Utilities         (NOT OpenCvSharp)
Config / Models / Utilities  →  (leaves; plain C# only)
```

Any violation is a Phase-level blocker. When in doubt, run `/review-layer <Name>`.
