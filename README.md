# PTZ Joystick Control

PTZ Joystick Control lets you use any joystick, gamepad, keyboard, MIDI device, or OSC controller to control PTZ cameras. 🕹️🎮

## Features

### Input Devices
  - **Gamepads & Joysticks** — any SDL2-compatible controller (Xbox, PlayStation, generic USB gamepads, etc.).
  - **Keyboard** — map keyboard keys to camera commands.
  - **MIDI** — use MIDI controllers and fader surfaces.
  - **OSC** — receive Open Sound Control messages over UDP or TCP.
  - Multiple input devices can be active simultaneously.
  - Fully customizable button and axis mapping with saveable/loadable mapping profiles.
  - Smooth movement ramping for precise, jitter-free control.

### Camera Connections
  - **VISCA over IP** (TCP or UDP).
  - **VISCA over Serial** (RS-232/RS-485).
  - **VISCA over TCP-Serial** — VISCA protocol tunnelled over a TCP connection.
  - Manage multiple cameras and switch between them instantly.
  - Automatic reconnection on connection loss.

### Camera Controls
  - **Movement** — pan, tilt, and combined pan/tilt with adjustable speed.
  - **Zoom** — tele/wide with speed control; optional proportional zoom mode.
  - **Focus** — near/far adjustment, auto/manual/toggle focus modes, and focus lock.
  - **Exposure** — exposure mode (Auto, Manual, Shutter Priority, Iris Priority, Bright), iris, shutter speed, and gain adjustment.
  - **White Balance** — mode selection (Auto, Indoor, Outdoor, One-Push, Manual), red/blue gain, and one-push WB trigger.
  - **Presets** — save and recall up to 255 camera presets with configurable recall speed.
  - **Other** — backlight compensation, aperture, and camera power on/off.

### Web Interface & REST API
  - Built-in web server (default port **5000**) with a browser-based control panel.
  - Virtual joystick, D-pad, and on-screen buttons accessible from any device on your network.
  - Full REST API for integration with external systems — see [API_DOCS.md](API_DOCS.md) for complete documentation.

### vMix Integration
  - Trigger **Cut** and **Fade** transitions in vMix directly from any mapped button.

### Other
  - Tray icon showing the currently selected camera.
  - Automatic update checking on startup.
  - Cross-platform: Windows, macOS, and Linux (see below).

## Installation
  1. Download the latest release from the [releases page](../../releases).
  2. Double-click the downloaded installer to install the application.
  3. Double-click the PTZ Joystick Control executable to launch the application.

## Usage
  1. Launch the application.
  2. Go to **Cameras** and add your VISCA-compatible camera (IP, Serial, or TCP-Serial).
  3. Go to **Gamepads** (or the relevant input tab) and activate your input device.
  4. Map your inputs to camera commands on the mapping screen.
  5. Use your controller to control the camera. Multiple devices can be active at the same time.
  6. Optionally save your mapping as a **profile** for quick recall later.
  7. Access the **web interface** at `http://localhost:5000` to control cameras from a browser.

## Linux and Raspberry Pi Support
The application also runs on Linux and even a Raspberry Pi, however pre-built binaries for those platforms are not yet provided.
On x86 and x64 Linux machines you can build from source using `dotnet build`, and it should run without further changes.
For Linux-ARM devices such as Raspberry Pi you additionally need to compile libSDL2 yourself and add it to the build output for it to run.

## License
This project is licensed under the GPL-3 License — see the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing
Contributions are always welcome! Please feel free to submit a pull request or open an issue if you encounter any problems with the software.

Thank you for using PTZ Joystick Control!
