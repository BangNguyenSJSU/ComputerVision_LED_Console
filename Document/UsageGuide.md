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

1. The console scans camera indices 0–5 and prints the working ones, paired with their Windows DirectShow `FriendlyName` so each entry reads e.g. `[1] Logitech HD Pro Webcam C920 — index 1`.
2. You pick a camera (press Enter for the first one). The negotiated camera name and resolution are echoed by the `[INFO ] Camera negotiated: '...'` line so you can confirm which device opened.
3. If the settings lock is enabled but no password has been set, the console prompts you to set one before the video window opens (press Enter to skip and run unlocked for this session).
4. A video window titled **LED Detection** opens.
5. On the first frame the app auto-detects red, yellow, and green LEDs and places a circle around each.
6. The running app prints `[INFO ] Detected N LED(s).` and `[INFO ] HTTP status server listening on ...` if network publishers are enabled.

---

## In-app controls

The video window must be the focused window for these to register.

| Input | Action |
|---|---|
| **Left-click** (empty area) | Add a manual LED marker at that point |
| **Left-click + drag** (on marker) | Move the marker |
| **Right-click** (on marker) | Delete the marker |
| **Mouse wheel** (over a marker) | Grow / shrink that marker's ROI radius. If no marker is under the cursor, the wheel resizes the currently selected marker. |
| **R** | Clear all markers, reset the marker-id counter, and re-run auto-detect on the current frame |
| **C** | Calibrate the selected marker (two-step: press while LED is ON, then again while OFF) |
| **[** / **]** | Nudge the selected marker's **On threshold** down / up by 5 |
| **;** / **'** | Nudge the selected marker's **Off threshold** down / up by 5 |
| **+** / **-** | Zoom in / out (hardware UVC zoom on supported cams, digital crop+resize otherwise) |
| **,** / **.** | Exposure down / up (darker is generally better for LED detection). Auto-switches camera to manual mode. |
| **A** | Toggle auto-exposure on / off |
| **S** | Save current markers + settings to `config_ComputerVisionLed.txt` |
| **U** | Prompt for password on the console to unlock editing |
| **L** | Re-lock for the rest of the session (no password required) |
| **P** | Password prompt that toggles the lock — enter the password to unlock if locked, or to lock if unlocked |
| **Q** or **Esc** | Quit (also auto-saves) |

### HUD label format

Each marker is drawn as a colored circle with a label of the form:

```
<color><id>-<state> (<brightness>)
```

Worked examples:

| Label | Decoded |
|---|---|
| `R1-ON (29)` | Red marker, id 1, currently ON, mean ROI brightness 29 |
| `Y3-OFF (8)` | Yellow marker, id 3, currently OFF, brightness 8 |
| `G5-ON (40)` | Green marker, id 5, currently ON, brightness 40 |
| `7-ON (12)` | Marker id 7 with `Unknown` color (color letter omitted), ON, brightness 12 |

The selected marker (most recently clicked, dragged, or wheel-resized) gets two extra fields appended showing its active thresholds, e.g. `R1-ON (29) [on>=24 off<=12]`. Use this to confirm what `[`/`]`/`;`/`'` are tuning.

The marker id is the monotonic integer assigned by `LedDetector.AddRoi` when the marker is created. It is stable for the marker's lifetime, persists through save/load, and resets to 1 on every `R` rescan. A red ring means OFF, a green ring means ON; the selected marker's ring is drawn thicker.

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
- `AutoExposure` — start in auto-exposure mode (default **true**). Set `false` to apply `InitialExposure` at startup.
- `InitialExposure` — DSHOW log₂-seconds exposure, applied when `AutoExposure=false` (default **-6.0**, ≈ 1/64 s). Typical Logitech range is `-11..-1`.
- `ExposureMin` / `ExposureMax` / `ExposureStep` — bounds and step for the runtime `,`/`.` hotkeys (defaults `-11`, `-1`, `1.0`).
- `InitialZoomFactor` — zoom applied at startup (default **1.0** = no zoom).
- `MinZoomFactor` / `MaxZoomFactor` / `ZoomStep` — bounds and step for the runtime `+`/`-` hotkeys (defaults `1.0`, `5.0`, `0.25`).

### Camera tuning notes (Logitech UVC)

- **Manual exposure is recommended for LED detection.** With auto-exposure on, the driver darkens the whole frame when the LED turns ON and brightens it when the LED turns OFF — exactly the opposite of what hysteresis thresholds expect. Press `A` to go manual, then `,` until the background is near black. The LED ON should now read clearly above your `DefaultOnThreshold`.
- **Hardware vs digital zoom.** On Logitech C920, C922, Brio, and similar, `+`/`-` drives the camera's built-in UVC zoom — no frame crop. On models without UVC zoom (C270 and many integrated laptop cams), the app falls back to centered crop+resize automatically. The hotkeys behave identically from the user's perspective. The startup log line `Zoom: hardware UVC zoom supported` vs `Zoom: hardware UVC zoom not available; digital crop+resize will be used` tells you which mode you're in.
- **DSHOW exposure values are log₂-seconds.** `-6` ≈ 1/64 s, `-8` ≈ 1/256 s, `-11` ≈ 1/2048 s. The camera may snap your requested value to its nearest supported step — the `[INFO ]` log prints both the requested value and the camera's reported readback.

### `Config/DetectionConfig.cs`
- `DefaultOnThreshold` / `DefaultOffThreshold` — initial hysteresis values per marker (150 / 120)
- `MinThresholdGap` — enforced minimum between On and Off (default 5)
- `ThresholdStep` — step for keyboard tuning (default 5)
- `CalibrationMargin` — distance from measured brightness used by the `C` key (default 10)
- `MinDetectRadius` / `MaxDetectRadius` — auto-detect radius bounds in pixels
- `DefaultManualRadius` — radius for a manually placed marker (default 20)
- `HueLow`/`HueHigh`, `SaturationLow`/`SaturationHigh`, `ValueLow`/`ValueHigh` — HSV range for yellow (20–35, 100–255, 150–255)
- `DetectRed` + `RedHueLow1`/`RedHueHigh1` + `RedHueLow2`/`RedHueHigh2` — red wraps the hue circle, so two sub-ranges (defaults 0–10 and 170–180) are OR-ed together
- `DetectGreen` + `GreenHueLow`/`GreenHueHigh` — HSV hue range for green (default 40–85). Shares the saturation/value bounds above.

Each marker is tagged with the color band it was detected in (or `Unknown` for manual clicks that miss every band). The color travels with the marker through detection, persistence, JSON, and the binary TCP stream.

### `Config/NetworkConfig.cs`
- `HttpEnabled` — serve `GET /status` on localhost (default **true** during dev, set to `false` to disable)
- `HttpBindAddress` — default `127.0.0.1`
- `HttpPort` — default `18080`
- `TcpEnabled` — push JSON-line stream on TCP (default `false`)
- `TcpBindAddress` — default `127.0.0.1`
- `TcpPort` — default `9090`

Config loading from disk **is** wired. On startup the app looks for `config_ComputerVisionLed.txt` in the same directory as the executable. If present, every field in that file overrides the compiled defaults and any saved LED markers (position, radius, per-LED thresholds) are restored. If the file is missing or unparseable, the app logs a warning and falls back to the compiled defaults.

### Saving

- Press **S** at any time to write the current AppConfig + markers back to `config_ComputerVisionLed.txt`.
- The app also auto-saves when you quit via **Q** / **Esc**.

### File format

The file is JSON (named `.txt` for easy notepad editing). Shape:

```json
{
  "version": 1,
  "app": {
    "camera": { "frameWidth": 640, "frameHeight": 480, "targetFps": 240, "autoExposure": false, "initialExposure": -6.0, ... },
    "detection": { "defaultOnThreshold": 12.0, "defaultOffThreshold": 6.0, "detectRed": true, ... },
    "network": { "httpEnabled": false, "tcpBinaryEnabled": true, ... }
  },
  "markers": [
    { "id": 1, "centerX": 312.0, "centerY": 204.0, "radius": 6, "onThreshold": 12.0, "offThreshold": 6.0 }
  ]
}
```

### Editing by hand

- Safe to edit in any text editor — invalid JSON is logged as a warning on next launch and ignored.
- Runtime-only marker state (current ON/OFF, last brightness, calibration phase) is **not** saved. Each marker resumes in `Unknown` state until the first frame re-evaluates it.
- Delete the file to reset everything to compiled defaults.

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
      "color": "Yellow",
      "brightness": 187.4,
      "timestampUtc": "2026-04-17T21:59:23.192Z"
    }
  ]
}
```

Field notes:
- `markerId` is a monotonic integer assigned when the marker is created. It resets when the app restarts.
- `status` is one of `"Unknown"`, `"Off"`, `"On"`. `Unknown` only appears for a brand-new marker that has never been evaluated (or whose ROI fell outside the frame).
- `color` is one of `"Unknown"`, `"Red"`, `"Yellow"`, `"Green"`. AutoDetect tags each marker with the color band it matched; manual clicks sample HSV at the click point and tag the same way (or fall back to `Unknown` if no band matches).
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

## Binary TCP stream

Default endpoint: `127.0.0.1:9091`

A compact byte-oriented alternative to the JSON TCP stream — intended for microcontrollers, custom dashboards, or any consumer that doesn't want to parse JSON. Each LED is encoded in 2 bytes, and each tick is framed with a 1-byte count so the receiver can delimit snapshots.

Server is opt-in via `NetworkConfig.TcpBinaryEnabled`. It runs independently of the JSON TCP stream — both can be enabled at the same time on separate ports.

### Wire format

Per captured frame, one self-delimiting frame is written:

```
+---+----+----+----+----+ ... +----+----+
| N | i1 | s1 | i2 | s2 | ... | iN | sN |
+---+----+----+----+----+ ... +----+----+
  1    1    1    1    1   ...    1    1   bytes
```

- `N` — LED count, unsigned byte (0–255).
- `iK` — `MarkerId` as an unsigned byte. **Markers with `MarkerId > 255` are skipped** from the binary stream (a warn log is emitted once per overflow id). The JSON stream still reports them.
- `sK` — packed status byte. **High nibble = `LedColor`, low nibble = `LedStatus`** (each enum fits in 4 bits):

| Nibble | Value | Meaning |
|---|---|---|
| Low (status) | `0x0` | `Unknown` |
| Low (status) | `0x1` | `Off` |
| Low (status) | `0x2` | `On` |
| High (color) | `0x0` | `Unknown` |
| High (color) | `0x1` | `Red` |
| High (color) | `0x2` | `Yellow` |
| High (color) | `0x3` | `Green` |

Worked examples:

| Status byte | Decoded |
|---|---|
| `0x02` | Unknown-color, ON (back-compat for un-tagged markers) |
| `0x21` | Yellow, OFF |
| `0x22` | Yellow, ON |
| `0x32` | Green, ON |

Decode in code: `status = byte & 0x0F; color = (byte >> 4) & 0x0F;`. A reader written before the color field shipped — and which checks only `byte == 0x01` / `byte == 0x02` — will silently miss color-tagged markers. Either mask the low nibble or update the reader.

When no markers exist, a single `0x00` byte is sent per tick — this acts as a heartbeat and keeps the tick cadence predictable.

Example: 2 LEDs, id=1 yellow ON, id=2 yellow OFF → `02 01 22 02 21` (5 bytes).

### Python — streaming reader

```python
import socket

HOST, PORT = "127.0.0.1", 9091
STATUS = {0: "Unknown", 1: "Off", 2: "On"}
COLOR = {0: "Unknown", 1: "Red", 2: "Yellow", 3: "Green"}

with socket.create_connection((HOST, PORT)) as sock:
    buf = b""
    while True:
        chunk = sock.recv(4096)
        if not chunk:
            break
        buf += chunk
        while buf:
            n = buf[0]
            frame_len = 1 + n * 2
            if len(buf) < frame_len:
                break
            frame = buf[:frame_len]
            payload = frame[1:]
            buf = buf[frame_len:]
            print(f"raw: {frame.hex(' ')}")
            for i in range(n):
                mid, packed = payload[2 * i], payload[2 * i + 1]
                st = packed & 0x0F
                col = (packed >> 4) & 0x0F
                print(f"  LED #{mid}: {COLOR.get(col, '?')} {STATUS.get(st, '?')}")
```

Example output for a tick with 2 LEDs (id=1 yellow ON, id=2 yellow OFF):

```
raw: 02 01 22 02 21
  LED #1: Yellow On
  LED #2: Yellow Off
```

### Python — only act on edges

```python
import socket

HOST, PORT = "127.0.0.1", 9091
STATUS = {0: "Unknown", 1: "Off", 2: "On"}
COLOR = {0: "Unknown", 1: "Red", 2: "Yellow", 3: "Green"}
last = {}

with socket.create_connection((HOST, PORT)) as sock:
    buf = b""
    while True:
        chunk = sock.recv(4096)
        if not chunk:
            break
        buf += chunk
        while buf:
            n = buf[0]
            frame_len = 1 + n * 2
            if len(buf) < frame_len:
                break
            payload = buf[1:frame_len]
            buf = buf[frame_len:]
            for i in range(n):
                mid, packed = payload[2 * i], payload[2 * i + 1]
                st = packed & 0x0F
                col = (packed >> 4) & 0x0F
                if last.get(mid) != packed:
                    last[mid] = packed
                    print(f"LED #{mid} -> {COLOR.get(col, '?')} {STATUS.get(st, '?')}")
```

### Quick sanity check with ncat

```bash
ncat 127.0.0.1 9091 | xxd
```

You should see ~60 short rows per second, each starting with a count byte.

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

**Binary TCP port `9091` won't bind**
Something else on the host owns the port. The app logs `[ERROR]` and continues without the binary stream. Change `NetworkConfig.TcpBinaryPort` or free the port — `netstat -ano | findstr :9091` identifies the PID.

**`+` / `-` doesn't visibly zoom**
Your camera reported no UVC zoom support, so the app should have automatically switched to digital crop+resize. Check the startup `[INFO ]` line — if it says `digital crop+resize will be used`, pressing `+` should still produce a visible zoom. If neither mode works, confirm the window is focused; hotkeys only register when the video window has focus.

**LED still flickers between ON/OFF after tuning thresholds**
Check whether auto-exposure is on — it's the most common culprit. Press `A` to go manual, then `,` a few times to darken the frame. The LED ON region should sit well above `DefaultOnThreshold` and the background well below `DefaultOffThreshold`. Threshold tuning fights a losing battle if the driver keeps rescaling the overall brightness.

**App exits immediately after "Using camera index..."**
OpenCvSharp native runtime is missing. Confirm the `OpenCvSharp4.runtime.win` NuGet package restored. A full `dotnet clean && dotnet restore && dotnet build` usually fixes it.

---

## Security — settings lock

The app ships with a session-scoped settings lock. When locked, the video feed and HTTP/TCP publishing keep running but **mouse edits, threshold/calibration keys, camera controls, and save are blocked**. `Q`/`Esc` (quit) and `R` (rescan) always work.

### First launch

If `Security.LockEnabled` is true (default) and no password is configured yet, the console prompts:

```
Settings lock is enabled but no password is set.
Enter a password to enable the lock (or press Enter to leave it disabled for this session): ___
```

- Type a password and press Enter — the app hashes it (PBKDF2-SHA256, 100 000 iterations, 16-byte salt) and writes the hash + salt into `config_ComputerVisionLed.txt`. Every subsequent launch starts locked until you press `U` and type the password.
- Press Enter with no input — the lock stays disabled for this session, and the warning is logged.

### Unlocking and re-locking

- Press **U** while the video window is focused — the console prompts `Password: `. Type it and press Enter. Correct password unlocks editing for the rest of the session; wrong password keeps the lock and logs a warning.
- Press **L** to re-lock at any time without quitting.
- The HUD shows a red **LOCKED** chip in the top-right corner whenever the lock is engaged.

### What is gated

| Action | Locked | Unlocked |
|---|---|---|
| Mouse add/move/delete markers | blocked | allowed |
| `[` `]` `;` `'` `C` per-marker tuning | blocked | allowed |
| `+` `-` `,` `.` `A` camera controls | blocked | allowed |
| `S` save / auto-save on quit | blocked | allowed |
| `Q` `Esc` quit | always | always |
| `R` rescan | always | always |
| `U` unlock prompt / `L` re-lock / `P` password toggle | always | always |

### Caveats

- This is a **UX guardrail, not real authentication.** `config_ComputerVisionLed.txt` remains a plain-text JSON file under the executable's directory — anyone who can edit that file can clear the password fields, change the hash, or set `lockEnabled` to `false`. The lock prevents accidental misuse, not a determined attacker.
- The password prompt masks input with `*` per keystroke. Backspace edits the buffer, Enter submits, and Esc cancels. If stdin is redirected (piped input or test harness), the prompt falls back to plain `Console.ReadLine` so automated drivers still work.
- Resetting a forgotten password: blank out `passwordHash` and `passwordSalt` in `config_ComputerVisionLed.txt`. The next launch behaves like a fresh first-run and prompts you to set a new password.

---

## Architecture reference

See `Document/DesignPlan.md` for the authoritative layer contract and `Document/DesignPlanPhase.md` for the phase-by-phase refactor history that got us here.

Layer map in one line: `Program → App → Camera / Vision / Services → Config / Models / Utilities`, with `Network` subscribing to `Services` only (never touches `OpenCvSharp`).
