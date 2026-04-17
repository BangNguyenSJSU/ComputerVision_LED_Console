# ComputerVision_LED_Console — Usage Guide

A desktop tool that watches a webcam feed, detects yellow LED ON/OFF state with per-LED hysteresis, and publishes the latest state over localhost HTTP and/or TCP.

---

## Requirements

- **Windows 10 or 11**
- **.NET 8 SDK** (`dotnet --info` must report an 8.x SDK)
- A **USB webcam** reachable through the DirectShow backend (most UVC cameras)
- Optional: Visual Studio 2022/2026 if you want IDE tooling

---

## Build and run

From the repo root:

```bash
dotnet restore
dotnet build
dotnet run
```

On launch:

1. The console scans camera indices 0–5 and prints the working ones.
2. You pick a camera (press Enter for the first one).
3. A video window titled **LED Detection** opens.
4. On the first frame the app auto-detects yellow LEDs and places a circle around each.
5. The running app prints `[INFO ] Detected N yellow LED(s).` and `[INFO ] HTTP status server listening on ...` if network publishers are enabled.

---

## In-app controls

The video window must be the focused window for these to register.

| Input | Action |
|---|---|
| **Left-click** (empty area) | Add a manual LED marker at that point |
| **Left-click + drag** (on marker) | Move the marker |
| **Right-click** (on marker) | Delete the marker |
| **R** | Clear all markers and re-run auto-detect on the current frame |
| **C** | Calibrate the selected marker (two-step: press while LED is ON, then again while OFF) |
| **[** / **]** | Nudge the selected marker's **On threshold** down / up by 5 |
| **;** / **'** | Nudge the selected marker's **Off threshold** down / up by 5 |
| **Q** or **Esc** | Quit |

The label on each marker shows state, current brightness, and (for the selected marker) the active thresholds.

---

## Configuration

All tunables live under `Config/`. Defaults are set in each class.

### `Config/CameraConfig.cs`
- `DeviceIndex` — preselected camera (unused today; probe prompts the user)
- `FrameWidth`, `FrameHeight` — requested capture resolution (default 1920×1080)
- `TargetFps` — default 60
- `BufferSize` — capture queue depth (default 1; keep low)
- `FourCC` — default `"MJPG"`; change only if your camera can't speak MJPEG
- `MaxProbeIndex` — highest index checked during probe (default 5)
- `DisplayMaxWidth` — downscale display if frame is wider than this (default 1280)

### `Config/DetectionConfig.cs`
- `DefaultOnThreshold` / `DefaultOffThreshold` — initial hysteresis values per marker (150 / 120)
- `MinThresholdGap` — enforced minimum between On and Off (default 5)
- `ThresholdStep` — step for keyboard tuning (default 5)
- `CalibrationMargin` — distance from measured brightness used by the `C` key (default 10)
- `MinDetectRadius` / `MaxDetectRadius` — auto-detect radius bounds in pixels
- `DefaultManualRadius` — radius for a manually placed marker (default 20)
- `HueLow`/`HueHigh`, `SaturationLow`/`SaturationHigh`, `ValueLow`/`ValueHigh` — HSV range for yellow (20–35, 100–255, 150–255)

### `Config/NetworkConfig.cs`
- `HttpEnabled` — serve `GET /status` on localhost (default **true** during dev, set to `false` to disable)
- `HttpBindAddress` — default `127.0.0.1`
- `HttpPort` — default `18080`
- `TcpEnabled` — push JSON-line stream on TCP (default `false`)
- `TcpBindAddress` — default `127.0.0.1`
- `TcpPort` — default `9090`

Config loading from disk is **not** wired in v1 — edit the defaults and rebuild.

---

## Status JSON schema

Both HTTP and TCP publish the same `SystemStatus` payload, serialized with camelCase property names. Enum values stay PascalCase.

```json
{
  "timestampUtc": "2026-04-17T21:59:23.193Z",
  "cameraIndex": 1,
  "frameWidth": 1920,
  "frameHeight": 1080,
  "framesPerSecond": 60.00024,
  "leds": [
    {
      "markerId": 1,
      "status": "On",
      "brightness": 187.4,
      "timestampUtc": "2026-04-17T21:59:23.192Z"
    }
  ]
}
```

Field notes:
- `markerId` is a monotonic integer assigned when the marker is created. It resets when the app restarts.
- `status` is one of `"Unknown"`, `"Off"`, `"On"`. `Unknown` only appears for a brand-new marker that has never been evaluated (or whose ROI fell outside the frame).
- `brightness` is the mean grayscale value of the ROI (0–255).
- Both timestamps are ISO-8601 UTC. The outer `timestampUtc` is the moment `StatusService.Update` was called; each per-LED `timestampUtc` is the moment that detection was computed (within the same tick).

---

## HTTP endpoint

Default URL: `http://127.0.0.1:18080/status`

| Request | Response |
|---|---|
| `GET /status` | `200 application/json` — full `SystemStatus` |
| `GET /anything-else` | `404 Not Found` |
| Any non-GET | `405 Method Not Allowed` |

Server is opt-in via `NetworkConfig.HttpEnabled`. If the port is already in use the app logs `[ERROR]` and continues without HTTP.

### Curl
```bash
curl -sS http://127.0.0.1:18080/status | jq .
```

### Python — simple poll

```python
import time
import requests

URL = "http://127.0.0.1:18080/status"

while True:
    try:
        resp = requests.get(URL, timeout=1.0)
        resp.raise_for_status()
        data = resp.json()
    except requests.RequestException as e:
        print(f"request failed: {e}")
        time.sleep(1.0)
        continue

    for led in data["leds"]:
        print(
            f"LED #{led['markerId']}  "
            f"{led['status']:<3}  "
            f"brightness={led['brightness']:.0f}"
        )
    time.sleep(0.5)
```

### Python — print only on state change

```python
import time
import requests

URL = "http://127.0.0.1:18080/status"
last_state = {}

while True:
    try:
        leds = requests.get(URL, timeout=1.0).json()["leds"]
    except Exception as e:
        print("poll error:", e)
        time.sleep(1.0)
        continue

    for led in leds:
        mid = led["markerId"]
        prev = last_state.get(mid)
        if prev != led["status"]:
            last_state[mid] = led["status"]
            print(f"[{led['timestampUtc']}] LED #{mid}: {prev} -> {led['status']}")
    time.sleep(0.1)
```

HTTP is polling-based — easy to integrate, but you pay the request latency per poll.

---

## TCP stream

Default endpoint: `127.0.0.1:9090`

The TCP publisher pushes **one line of JSON per captured frame** (UTF-8, terminated by `\n`). Each line is a complete `SystemStatus` — same shape as the HTTP payload. Intended consumer pattern: connect, read lines forever, parse each line as JSON.

Server is opt-in via `NetworkConfig.TcpEnabled`. Connecting clients are tracked and dropped on write error; clients can reconnect at any time. The server does not read anything from the client — it's push-only.

At 60 fps this is ~60 JSON lines per second per client. Typical line size is a few hundred bytes per LED, well under typical TCP buffers.

### Python — streaming reader

```python
import json
import socket

HOST = "127.0.0.1"
PORT = 9090

def stream_status(host: str, port: int):
    with socket.create_connection((host, port)) as sock:
        buffer = b""
        while True:
            chunk = sock.recv(4096)
            if not chunk:
                return  # server closed
            buffer += chunk
            while b"\n" in buffer:
                line, buffer = buffer.split(b"\n", 1)
                if not line:
                    continue
                yield json.loads(line)

for status in stream_status(HOST, PORT):
    for led in status["leds"]:
        print(
            f"{status['timestampUtc']}  "
            f"LED #{led['markerId']}  "
            f"{led['status']:<3}  "
            f"brightness={led['brightness']:.0f}"
        )
```

### Python — only act on edges

```python
import json
import socket

HOST, PORT = "127.0.0.1", 9090
last = {}

with socket.create_connection((HOST, PORT)) as sock:
    buffer = b""
    while True:
        chunk = sock.recv(4096)
        if not chunk:
            break
        buffer += chunk
        while b"\n" in buffer:
            line, buffer = buffer.split(b"\n", 1)
            if not line:
                continue
            status = json.loads(line)
            for led in status["leds"]:
                mid = led["markerId"]
                if last.get(mid) != led["status"]:
                    last[mid] = led["status"]
                    print(f"LED #{mid} -> {led['status']}  (brightness {led['brightness']:.0f})")
```

### Python — async

```python
import asyncio
import json

HOST, PORT = "127.0.0.1", 9090

async def consume():
    reader, writer = await asyncio.open_connection(HOST, PORT)
    try:
        while True:
            line = await reader.readline()
            if not line:
                break
            status = json.loads(line)
            # do something with status...
            print(status["leds"])
    finally:
        writer.close()
        await writer.wait_closed()

asyncio.run(consume())
```

---

## Troubleshooting

**"No cameras detected."**
Your webcam isn't exposed through DirectShow or is in use by another app (Zoom, Teams, browser tab). Close the other app and retry.

**HTTP won't bind / `HttpListenerException`**
Port `18080` is in use by another local process. The app logs an error and continues without HTTP. Either change `NetworkConfig.HttpPort` or free the port — `netstat -ano | findstr :18080` tells you which PID is holding it.

**`curl /status` shows `"leds": []`**
No markers exist yet. Either:
- Point the camera at a yellow LED, focus the video window, and press `R`.
- Left-click in the video window to add a manual marker.

**Auto-detect finds zero LEDs even with an LED in frame**
The HSV defaults are tuned for warm-yellow LEDs at indoor lighting. Try widening `HueLow`/`HueHigh` in `DetectionConfig` or lowering `ValueLow` if the LED is dim. Keyboard `[` / `]` / `;` / `'` are faster for live tuning once a marker is placed.

**LED flickers between ON/OFF in the JSON**
Raise `DetectionConfig.DefaultOnThreshold` or lower `DefaultOffThreshold` to widen the hysteresis gap. The minimum allowed gap (default 5) is enforced automatically.

**TCP client disconnected silently**
Each status tick writes to every connected client. A dead socket is detected on write failure and removed. Your reader should reconnect if it sees a closed connection.

**App exits immediately after "Using camera index..."**
OpenCvSharp native runtime is missing. Confirm the `OpenCvSharp4.runtime.win` NuGet package restored. A full `dotnet clean && dotnet restore && dotnet build` usually fixes it.

---

## Architecture reference

See `Document/DesignPlan.md` for the authoritative layer contract and `Document/DesignPlanPhase.md` for the phase-by-phase refactor history that got us here.

Layer map in one line: `Program → App → Camera / Vision / Services → Config / Models / Utilities`, with `Network` subscribing to `Services` only (never touches `OpenCvSharp`).
