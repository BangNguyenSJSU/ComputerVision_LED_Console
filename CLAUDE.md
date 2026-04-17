# ComputerVision_LED_Console — Project Context

Desktop computer-vision tool that watches a webcam feed and publishes LED ON/OFF state for downstream consumers (localhost API, TCP, HTTP).

## Stack
- **Language:** C# (.NET 8)
- **CV library:** OpenCvSharp4 + OpenCvSharp4.runtime.win
- **Runtime shape:** Console app (Version 1). GUI layer is planned, not coupled.

## Architectural contract
Authoritative spec: `Document/DesignPlan.md`. Folder layout already scaffolded:

| Layer | Responsibility | Must NOT depend on |
|---|---|---|
| `Camera/` | Frame acquisition only | Vision, UI, Network |
| `Vision/` | LED detection, ROI math, hysteresis | UI, Network |
| `Models/` | Plain data (`LedStatus`, `DetectionResult`, `SystemStatus`) | Everything else |
| `Config/` | Centralized config values — no constants scattered | Everything else |
| `Services/` | Shared state (`StatusService`, thread-safe) | UI, Network |
| `Network/` | HTTP/TCP publishing of serialized status | OpenCvSharp `Mat` internals |
| `App/` | Wires modules together (`AppController`) | — |
| `Utilities/` | Cross-cutting helpers (Logger, TimeProvider) | — |

Program.cs is currently a one-file demo; the refactor into these layers is in progress.

## Non-negotiable conventions
- **Mat lifecycle:** every `Mat` and `VideoCapture` must be `using` / explicitly disposed. OpenCvSharp leaks native memory fast on long-running capture loops.
- **Hysteresis:** LED state uses a two-threshold model (`OnThreshold`, `OffThreshold`) with a minimum gap enforced to prevent flicker. Do not collapse to a single threshold.
- **Thread safety:** `StatusService` is read by publisher threads (HTTP/TCP) and written by the capture loop — any change to its internals must remain lock-safe.
- **Network layer ignorance:** `Network/*` serializes `SystemStatus` (plain data) only. It must never touch `Mat`, `VideoCapture`, or any OpenCvSharp type.
- **Config centralization:** no magic numbers in `Program.cs` or layer code. Add to the matching `*Config` class under `Config/`.
- **Display back-pressure:** capture runs at full sensor res (1920×1080 @ 60fps, MJPG, buffer=1). Display is downscaled to ≤1280 wide to keep render off the capture hot path. Preserve this separation.

## Working style
- Prefer editing existing files over creating new ones.
- Keep methods short; no massive classes.
- No DI containers. Plain interfaces + constructor wiring in `AppController`.
- No comments explaining *what* code does — only *why* when non-obvious.
- Don't introduce async/await unless a layer genuinely needs it (capture loop is synchronous by design).

## Current state (2026-04-17)
- Git initialized.
- Layer folders scaffolded with interface + stub impls; implementations throw `NotImplementedException`.
- `Program.cs` at project root still contains the working v0 logic and is the source of truth until the refactor lands layer-by-layer.
- Target framework: migrating from `net5.0` to `net8.0`.
