# Plan — Tri-color Detection + Settings Lock

## Context

Today the LED detector recognizes Yellow (one HSV band) and Red (two-band wrap-around) but throws both into a single mask before contour-finding, so it cannot tell the caller which color each marker is. We are adding **Green** detection AND **per-marker color tagging** so downstream consumers (HTTP/JSON, binary TCP) can distinguish red/yellow/green LEDs in a mixed scene.

In parallel, every keyboard/mouse interaction on the OpenCV window currently mutates state (markers, thresholds, camera settings, persisted config) with no access control. We are adding a **session-scoped password lock** so casual users can view but not edit. This is a UX guardrail — the JSON config file remains hand-editable, so it is not real authentication.

User-confirmed design choices:
- Color is encoded into the **upper nibble** of the existing TCP-binary status byte; the 1+N×2 frame layout stays.
- Lock covers mouse edits, threshold/calibration keys, camera-control keys, and save (S + auto-save on quit). `Q`/`ESC`/`R` always work.
- First launch prompts to set a password on the console; empty input skips and leaves the session unlocked with a warning.
- Per-session lock — every launch starts locked once a password is configured.

## Approach (one-line per layer)

- `Models/`: new `LedColor` enum; `DetectionResult` carries `Color`.
- `Config/`: new `SecurityConfig`; `DetectionConfig` gains green band; `MarkerSnapshot` gains color.
- `Vision/LedDetector`: `AutoDetect` runs `FindContours` per-band so each new ROI is color-tagged at creation; new `ClassifyColorAt` for manual clicks.
- `App/InputHandler`: takes a `Func<float,float,LedColor>` color sampler set per-tick by `AppController`; gates every editing path on `AuthGate.IsUnlocked`; new `KeyAction.UnlockPrompt` + `KeyAction.Lock`.
- `App/AuthGate` (new): PBKDF2-SHA256 / 100k iterations / `FixedTimeEquals`; mirrors unlocked state to `AppState.Unlocked` so `OverlayRenderer` can show a "LOCKED" HUD chip without a new dependency.
- `App/AppController`: owns first-run prompt, unlock prompt, and gates `ConfigStore.Save` (manual `S` and auto-save on quit).
- `Network/TcpBinaryStatusServer`: encodes `(Color << 4) | Status` in the existing status byte.

## Feature 1 — Tri-color detection (5 commits)

### 1A — Enum + config + model surface (no behavior change)

Files:
- `Models/LedColor.cs` (new): `Unknown=0, Red, Yellow, Green` — preserves the `Unknown=0` default-safe convention used by `LedStatus`.
- `Models/DetectionResult.cs`: `public LedColor Color { get; set; }`.
- `Vision/RoiConfig.cs`: `public LedColor Color { get; set; } = LedColor.Unknown;`.
- `Config/DetectionConfig.cs`: `DetectGreen=true`, `GreenHueLow=40`, `GreenHueHigh=85`. Reuses existing `SaturationLow/High`, `ValueLow/High`.
- `Config/MarkerSnapshot.cs`: `public LedColor Color { get; set; }`. The existing `JsonStringEnumConverter` in `App/ConfigStore.cs` already gives string round-trip — no other plumbing needed.
- `App/ConfigStore.Save`: copy `Color = m.Color` into the snapshot.
- `Vision/LedDetector.RestoreMarkers`: copy `m.Color` back into the new `RoiConfig`.

Test: `Tests/Config/MarkerSnapshotColorRoundTripTests.cs` — round-trip a snapshot with Red/Yellow/Green markers through ConfigStore JSON.

### 1B — AutoDetect refactor (per-band contour pass)

File: `Vision/LedDetector.cs` only.

Replace `BuildColorMask` with a private helper:
```csharp
private void RunBand(Mat hsv, LedColor color, int hLo, int hHi)
```
that does one InRange + one morphology open + one FindContours, passing `color` into the existing radius/dedupe filter then to a new private `AddRoi(float, float, int, LedColor)`. Order: yellow → red(low) → red(high) → green so existing yellow-first behavior is preserved. Cross-color overlaps still suppress duplicates via the existing `IsDuplicate` check.

Public `AddRoi(float, float, int)` keeps its signature (defaults `Color = Unknown`) — leaves existing tests untouched.

Mat lifetime: every per-band Mat (`band`, `kernel`) stays `using`-scoped.

Tests in `Tests/Vision/LedDetectorAutoDetectTests.cs`:
- `AutoDetect_RedDot_TagsMarkerRed` — BGR `(0, 0, 255)` dot.
- `AutoDetect_GreenDot_TagsMarkerGreen` — BGR `(0, 255, 0)` dot.
- `AutoDetect_MultiColorFrame_TagsEachByItsBand` — three dots; assert sorted set is `{Red, Yellow, Green}`.
- Existing yellow tests stay green.

### 1C — Manual marker color sampling (`InputHandler` stays Mat-free)

- `Vision/LedDetector.cs`: add `public LedColor ClassifyColorAt(FrameData frame, float x, float y)` — bounds-check then sample HSV and test against each enabled band; return `Unknown` if no match. Add overload `AddManualRoi(float x, float y, LedColor color)`; keep the existing 2-arg overload for back-compat.
- `App/InputHandler.cs`: add `Func<float,float,LedColor>? _colorSampler` (nullable, falls back to `Unknown` when null) plus `public void UpdateColorSampler(Func<float,float,LedColor>?)`. In `LButtonDown` else-branch:
  ```csharp
  var color = _colorSampler?.Invoke(nx, ny) ?? LedColor.Unknown;
  int newIdx = _detector.AddManualRoi(nx, ny, color);
  ```
- `App/AppController.cs::Loop`: just before `EvaluateAll`, do `_input.UpdateColorSampler((x, y) => _detector.ClassifyColorAt(fd, x, y));`. The captured `fd` lives only inside the loop tick, which is the intent.

Trade-off rejected: routing the click as a return value from `InputHandler` to `AppController` (cleaner layering but bigger blast radius — InputHandler would also have to surface drag state). The injected `Func` keeps Mat out of `InputHandler` with one constructor arg.

### 1D — TCP binary wire-format pack

File: `Network/TcpBinaryStatusServer.cs::Encode`.

```
byte 0     = N
byte 1+i*2 = MarkerId
byte 2+i*2 = (Color << 4) | Status     // Color in {0..3}, Status in {0..2}
```

Implementation: `byte packed = (byte)(((int)led.Color << 4) | (int)led.Status); output[offset + 1] = packed;`

This is a wire-format break for any reader that does strict byte-equality on the status byte. Existing `Tests/Network/TcpBinaryStatusServerTests.cs` does exactly that, so:
- Update `ConnectedClient_ReceivesBinaryFrame_OnStatusUpdate` — seed `Color=Yellow`/`Color=Yellow`, expect `0x22` and `0x21`.
- Update `MarkerIdAboveByteMax_IsSkippedAndCountReflectsOnlyValidLeds` and `MultipleUpdates_ProduceMultipleFrames` to match (default `Color=Unknown` keeps high-nibble 0, so Off→`0x01`, On→`0x02` when no color is set — back-compat for legacy callers that don't fill the new field).
- New `ColorAndStatus_PackedIntoStatusByte` — table-driven over {Red,Yellow,Green} × {Off,On}.

### 1E — Overlay label + docs

- `App/OverlayRenderer.DrawMarkers`: prepend a color tag to `stateText`:
  ```csharp
  string colorTag = result.Color switch {
      LedColor.Red => "R-", LedColor.Yellow => "Y-", LedColor.Green => "G-", _ => "" };
  ```
  Ring color stays state-driven (green=ON, red=OFF) — do not change.
- `Document/UsageGuide.md`:
  - HSV defaults table — add Green row (40–85).
  - Status JSON shape — add `"color": "Yellow"` to the example.
  - Binary wire format — replace the `sK` enum block with a status-byte layout sub-section: bits 7–4 color, bits 3–0 status; include `0x21` / `0x22` / `0x32` examples.

## Feature 2 — Settings lock (4 commits)

### 2A — `SecurityConfig` + `AuthGate` (leaf layers, no app wiring)

- `Config/SecurityConfig.cs` (new):
  ```
  bool LockEnabled = true;
  string PasswordHash = "";    // base64
  string PasswordSalt = "";    // base64
  int PasswordIterations = 100_000;
  ```
- `Config/AppConfig.cs`: `public SecurityConfig Security { get; set; } = new();`
- `App/AppState.cs`: `public bool Unlocked { get; set; }`.
- `App/AuthGate.cs` (new — App layer; depends on `AppState`, so it cannot live in `Services/`):
  ```
  ctor(SecurityConfig, AppState)
    -> _state.Unlocked = !cfg.LockEnabled || !IsConfigured
  bool IsUnlocked            => _state.Unlocked
  bool IsConfigured          => hash + salt non-empty
  bool TryUnlock(string pw)  => PBKDF2-SHA256 (32 bytes, configured iterations), FixedTimeEquals
  void Lock()                => _state.Unlocked = false
  (string hash, string salt) HashNewPassword(string pw)   // 16-byte salt
  ```
  All hashing via `System.Security.Cryptography.Rfc2898DeriveBytes` + `CryptographicOperations.FixedTimeEquals` — zero new NuGet dependencies.

Tests:
- `Tests/App/AuthGateTests.cs` — `TryUnlock_WrongPassword_StaysLocked`, `TryUnlock_CorrectPassword_Unlocks`, `Lock_SetsLocked`, `IsConfigured_FalseWhenHashEmpty`, `Hash_RoundTripSurvivesSerialization` (hash → JSON round-trip through ConfigStore options → re-verify password).
- `Tests/Config/SecurityConfigPersistenceTests.cs` — round-trip via `ConfigStore.Save` / `TryLoad`.

### 2B — `InputHandler` lock gates

`App/InputHandler.cs`:
- Constructor adds `AuthGate auth`.
- `OnMouse` first line: `if (!_auth.IsUnlocked) return;`.
- Extend enum: `public enum KeyAction { Continue, Quit, Rescan, Save, UnlockPrompt, Lock }`.
- `HandleKey` ordering:
  1. `Q/ESC` → `Quit` (always).
  2. `R` → `Rescan` (always).
  3. `U` → `UnlockPrompt`. `L` → `Lock`. (always recognized; AppController owns the prompt.)
  4. `if (!_auth.IsUnlocked) return KeyAction.Continue;` — gates `S`, camera keys (`+ - , . A`), and per-marker keys (`C [ ] ; '`).

Tests `Tests/App/InputHandlerLockTests.cs` (new — uses existing `Tests/Camera/FakeCameraSource.cs`):
- Locked: `LButtonDown` does NOT add a marker; `HandleKey('s')` returns `Continue`; camera keys do not increment `FakeCameraSource.ZoomAdjustCalls` etc.; `Q` still returns `Quit`; `R` still returns `Rescan`.
- Unlock then re-run mouse + `S`: behavior restored.

### 2C — `AppController` owns prompt, gates save, runs first-run flow

`App/AppController.cs`:
- Constructor receives `AuthGate auth`.
- Before entering `Loop()`:
  - If `_config.Security.LockEnabled && !_auth.IsConfigured` → `Console.Write("Set a password to enable settings lock (or press Enter to skip): "); var pw = Console.ReadLine();`. Non-empty → `_auth.HashNewPassword(pw)` + write into `_config.Security` + `ConfigStore.Save` immediately so the hash persists. Empty → log `Logger.Warn("Settings lock disabled — no password set.")` and continue (AuthGate's ctor already set `Unlocked = true` for unconfigured state).
- Inside `Loop()` after `_input.HandleKey(...)`:
  ```
  KeyAction.UnlockPrompt -> Console.Write("Password: "); pw = ReadLine() ?? "";
                            if (TryUnlock(pw)) Logger.Info("Unlocked."); else Logger.Warn("Wrong password.");
  KeyAction.Lock         -> _auth.Lock(); Logger.Info("Locked.");
  ```
- Save gates: both the `KeyAction.Save` branch AND the auto-save in the `KeyAction.Quit` branch wrap with `if (_auth.IsUnlocked) ConfigStore.Save(...);`. Quit always succeeds; locked quit just doesn't persist.

`Program.cs`: construct `var auth = new AuthGate(config.Security, state);` and pass into `AppController` and `InputHandler`.

Known limitation: `Console.ReadLine` echoes the password. Document it; future hardening can swap to a `ConsoleHelpers.ReadMasked()` using `Console.ReadKey(intercept: true)`.

### 2D — `OverlayRenderer` LOCKED HUD + docs

- `App/OverlayRenderer.cs`: `DrawHud` reads `_state.Unlocked` (already injected via `AppState`) and, when locked, draws a red `"LOCKED"` chip near the top-right using existing `WarnColor`. No emoji — Hershey font has no glyph for U+1F512.
- `Document/UsageGuide.md`:
  - Append `U` (unlock prompt) and `L` (re-lock) rows to the controls table.
  - New "Security" section: first-run flow, what is gated vs always-allowed, and an explicit warning that this is a UX guardrail (the JSON file remains editable).

## Critical files to modify

- `C:\ComputerVision_LED_Console\Models\LedColor.cs` (new)
- `C:\ComputerVision_LED_Console\Models\DetectionResult.cs`
- `C:\ComputerVision_LED_Console\Vision\RoiConfig.cs`
- `C:\ComputerVision_LED_Console\Vision\LedDetector.cs`
- `C:\ComputerVision_LED_Console\Config\DetectionConfig.cs`
- `C:\ComputerVision_LED_Console\Config\MarkerSnapshot.cs`
- `C:\ComputerVision_LED_Console\Config\AppConfig.cs`
- `C:\ComputerVision_LED_Console\Config\SecurityConfig.cs` (new)
- `C:\ComputerVision_LED_Console\App\AuthGate.cs` (new)
- `C:\ComputerVision_LED_Console\App\AppState.cs`
- `C:\ComputerVision_LED_Console\App\InputHandler.cs`
- `C:\ComputerVision_LED_Console\App\AppController.cs`
- `C:\ComputerVision_LED_Console\App\OverlayRenderer.cs`
- `C:\ComputerVision_LED_Console\App\ConfigStore.cs`
- `C:\ComputerVision_LED_Console\Network\TcpBinaryStatusServer.cs`
- `C:\ComputerVision_LED_Console\Program.cs`
- `C:\ComputerVision_LED_Console\Document\UsageGuide.md`

Reused existing code:
- `JsonStringEnumConverter` already configured in `App/ConfigStore.cs` — handles `LedColor` serialization for free.
- `Tests/Camera/FakeCameraSource.cs` (no production code) — reused by the new `InputHandlerLockTests`.
- `LedDetector.IsDuplicate` (in `Vision/LedDetector.cs`) — reused as-is for cross-color dedupe in the per-band AutoDetect refactor.

## Verification

End-to-end smoke after each phase (CLAUDE.md rule: every phase ends with a working app).

1. `dotnet build` — 0 errors, 0 warnings.
2. `dotnet test` — full suite green. Expected counts after Feature 1: +4 vision tests, +1 config test, +1 binary test (existing 4 updated). After Feature 2: +5 AuthGate tests, +1 SecurityConfig test, ~6 InputHandler-lock tests.
3. `dotnet run`:
   - **Tri-color**: place red, yellow, green LEDs in frame; press `R` to rescan; verify each marker's HUD label starts with the right color tag (`R-`, `Y-`, `G-`); confirm `curl http://127.0.0.1:18080/status` shows `"color"` in each LED entry; with binary TCP enabled run `ncat 127.0.0.1 9091 | xxd` and verify status bytes carry the high-nibble color (e.g. `0x22` for Yellow ON).
   - **Lock**: first launch with no password set → console prompts → set "test123" → app starts unlocked → press `L` → HUD shows `LOCKED` → mouse clicks and `S`, `[`, `]`, `C`, `+`, `-`, `,`, `.`, `A` all no-op. `Q` and `R` still work. Press `U` → enter wrong password → still locked. Enter correct password → HUD chip clears, edits work.
   - **Persistence**: edit, save (S), quit, relaunch — markers + colors survive; lock state resets per-session.
