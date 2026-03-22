# PTZ Joystick Control - Web API Documentation

The PTZ Joystick Control web interface exposes a REST API for controlling PTZ cameras. By default, the web server runs on port **5000**.

## Base URL

```
http://<host>:5000
```

## Endpoints

### Camera List

#### `GET /api/cameras`
Returns a list of all configured cameras with their current status.

**Response:**
```json
[
  {
    "index": 0,
    "name": "Camera 1",
    "connected": true,
    "pollingEnabled": true,
    "zoomPosition": 0,
    "panPosition": 0,
    "tiltPosition": 0,
    "focusPosition": null,
    "focusMode": "Auto",
    "exposureMode": "Auto",
    "whiteBalanceMode": "Auto",
    "power": "On"
  }
]
```

---

### Movement Controls

#### `POST /api/cameras/{index}/pan`
Pan the camera left or right.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `speed` | byte | 0-24 | Pan speed |
| `direction` | string | `Left`, `Right`, `Stop` | Pan direction |

```json
{ "speed": 12, "direction": "Left" }
```

#### `POST /api/cameras/{index}/tilt`
Tilt the camera up or down.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `speed` | byte | 0-20 | Tilt speed |
| `direction` | string | `Up`, `Down`, `Stop` | Tilt direction |

```json
{ "speed": 10, "direction": "Up" }
```

#### `POST /api/cameras/{index}/pantilt`
Pan and tilt simultaneously.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `panSpeed` | byte | 0-24 | Pan speed |
| `tiltSpeed` | byte | 0-20 | Tilt speed |
| `panDirection` | string | `Left`, `Right`, `Stop` | Pan direction |
| `tiltDirection` | string | `Up`, `Down`, `Stop` | Tilt direction |

```json
{ "panSpeed": 12, "tiltSpeed": 10, "panDirection": "Right", "tiltDirection": "Up" }
```

#### `POST /api/cameras/{index}/stop`
Stop all movement (pan, tilt, and zoom).

No body required.

#### `POST /api/cameras/{index}/movestop`
Stop pan/tilt movement only.

No body required.

---

### Zoom Controls

#### `POST /api/cameras/{index}/zoom`
Zoom the camera.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `speed` | byte | 0-7 | Zoom speed |
| `direction` | string | `Tele`, `Wide`, `Stop` | Zoom direction (Tele = zoom in, Wide = zoom out) |

```json
{ "speed": 4, "direction": "Tele" }
```

#### `POST /api/cameras/{index}/zoomstop`
Stop zoom movement only.

No body required.

---

### Focus Controls

#### `POST /api/cameras/{index}/focus`
Adjust focus.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `speed` | byte | 0-7 | Focus speed |
| `direction` | string | `Far`, `Near`, `Stop` | Focus direction |

```json
{ "speed": 4, "direction": "Far" }
```

#### `POST /api/cameras/{index}/focusstop`
Stop focus movement.

No body required.

#### `POST /api/cameras/{index}/focusmode`
Set the focus mode.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `mode` | string | `Auto`, `Manual`, `Toggle` | Focus mode |

```json
{ "mode": "Auto" }
```

---

### Preset Controls

#### `POST /api/cameras/{index}/preset`
Recall or set a camera preset.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `action` | string | `Recall`, `Set` | Preset action |
| `number` | byte | 0-255 | Preset number |

```json
{ "action": "Recall", "number": 1 }
```

---

### Exposure Controls

#### `POST /api/cameras/{index}/exposure`
Set the exposure mode.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `mode` | string | `Auto`, `Manual`, `ShutterPriority`, `IrisPriority`, `Bright` | Exposure mode |

```json
{ "mode": "Auto" }
```

#### `POST /api/cameras/{index}/iris`
Adjust iris.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Iris direction |

```json
{ "direction": "Up" }
```

#### `POST /api/cameras/{index}/shutter`
Adjust shutter speed.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Shutter direction (Up = Faster, Down = Slower) |

```json
{ "direction": "Up" }
```

#### `POST /api/cameras/{index}/gain`
Adjust gain.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Gain direction |

```json
{ "direction": "Up" }
```

---

### White Balance Controls

#### `POST /api/cameras/{index}/whitebalance`
Set the white balance mode.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `mode` | string | `Auto`, `Indoor`, `Outdoor`, `OnePush`, `Manual` | White balance mode |

```json
{ "mode": "Auto" }
```

#### `POST /api/cameras/{index}/redgain`
Adjust red gain.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Red gain direction |

```json
{ "direction": "Up" }
```

#### `POST /api/cameras/{index}/bluegain`
Adjust blue gain.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Blue gain direction |

```json
{ "direction": "Up" }
```

#### `POST /api/cameras/{index}/wbtrigger`
Trigger one-push white balance.

No body required.

---

### Other Controls

#### `POST /api/cameras/{index}/backlight`
Set backlight compensation.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `mode` | string | `On`, `Off` | Backlight compensation mode |

```json
{ "mode": "On" }
```

#### `POST /api/cameras/{index}/aperture`
Adjust aperture.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `direction` | string | `Up`, `Down`, `Reset` | Aperture direction |

```json
{ "direction": "Up" }
```

#### `POST /api/cameras/{index}/power`
Control camera power.

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `state` | string | `On`, `Off` | Power state |

```json
{ "state": "On" }
```

---

### Gamepads

#### `GET /api/gamepads`
List all connected input devices (gamepads, keyboards, MIDI, OSC).

**Response:**
```json
[
  {
    "id": "device-id",
    "name": "Xbox Controller",
    "isConnected": true,
    "isActivated": true
  }
]
```

---

### API Documentation

#### `GET /api/docs`
Returns machine-readable JSON documentation of all available API endpoints.

---

## Web Interface

The web interface is available at the root URL (`http://<host>:5000/`). It provides:

- **Camera selection** when multiple cameras are configured
- **Movement controls**: 8-directional D-pad, virtual joystick, speed sliders
- **Zoom controls**: In/Out buttons with speed slider
- **Focus controls**: Far/Near buttons, speed slider, mode selection
- **Preset management**: Recall and set presets 1-9
- **Exposure controls**: Mode selector, Iris/Shutter/Gain adjustments
- **White Balance controls**: Mode selector, Red/Blue gain, WB Trigger
- **Other controls**: Backlight, Aperture, Power
- **Live status**: Auto-refreshing camera feedback with position data

## Error Handling

All endpoints return:
- `200 OK` on success
- `404 Not Found` with message `"Camera not found"` if the camera index is invalid
- `400 Bad Request` if the request body is malformed or contains invalid enum values

## Notes

- Camera indices are zero-based
- Movement buttons (pan, tilt, zoom, focus) use a press-and-hold pattern: send the movement command on press and a stop command on release
- Speed ranges vary by control type: Pan (0-24), Tilt (0-20), Zoom (0-7), Focus (0-7)
- Enum values are case-sensitive
