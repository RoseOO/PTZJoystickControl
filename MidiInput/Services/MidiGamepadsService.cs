using Commons.Music.Midi;
using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.MidiInput.Devices;
using PtzJoystickControl.MidiInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace PtzJoystickControl.MidiInput.Services;

public class MidiGamepadsService : IGamepadsService
{
    private readonly IGamepadSettingsStore _gamepadSettingsStore;
    private readonly ICamerasService _camerasService;
    private readonly ICommandsService _commandsService;
    private readonly Dictionary<string, IMidiInput> _openPorts = new();

    public ObservableCollection<IGamepadInfo> Gamepads { get; } = new();
    public ObservableCollection<IGamepad> ActiveGamepads { get; } = new();

    public MidiGamepadsService(IGamepadSettingsStore gamepadSettingsStore, ICamerasService camerasService, ICommandsService commandsService)
    {
        _gamepadSettingsStore = gamepadSettingsStore;
        _camerasService = camerasService;
        _commandsService = commandsService;

        // Load saved MIDI device settings
        var savedSettings = _gamepadSettingsStore.GetAllGamepadSettings()
            .Where(s => s.Id.StartsWith("MIDI:"));
        foreach (var s in savedSettings)
        {
            Gamepads.Add(new MidiGamepadInfo
            {
                Id = s.Id,
                Name = s.Name,
                IsConnected = false,
                IsActivated = s.IsActivated,
            });
        }

        // Scan for MIDI devices
        Task.Run(ScanAndMonitorDevices);
    }

    private async Task ScanAndMonitorDevices()
    {
        while (true)
        {
            try
            {
                ScanMidiDevices();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MIDI] Scan error: {ex.Message}");
            }

            await Task.Delay(5000); // Rescan every 5 seconds
        }
    }

    private void ScanMidiDevices()
    {
        try
        {
            var access = MidiAccessManager.Default;
            var inputs = access.Inputs.ToList();

            // Mark all current devices as potentially disconnected
            var currentIds = new HashSet<string>();

            foreach (var port in inputs)
            {
                var id = $"MIDI:{port.Id}";
                currentIds.Add(id);

                var existing = Gamepads.FirstOrDefault(g => g.Id == id);
                if (existing == null)
                {
                    var info = new MidiGamepadInfo
                    {
                        Id = id,
                        Name = $"MIDI: {port.Name}",
                        PortId = port.Id,
                        IsConnected = true,
                    };

                    // Check for saved settings
                    var saved = _gamepadSettingsStore.GetGamepadSettingsById(id);
                    if (saved != null)
                        info.IsActivated = saved.IsActivated;

                    Gamepads.Add(info);

                    if (info.IsActivated)
                        ActivateGamepad(info);
                }
                else
                {
                    existing.IsConnected = true;
                }
            }

            // Mark devices no longer present as disconnected
            foreach (var gamepadInfo in Gamepads.Cast<MidiGamepadInfo>().ToList())
            {
                if (!currentIds.Contains(gamepadInfo.Id))
                    gamepadInfo.IsConnected = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MIDI] Device scan error: {ex.Message}");
        }
    }

    public void ActivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not MidiGamepadInfo midiInfo) return;

        if (!ActiveGamepads.Any(g => g.Id == gamepadInfo.Id))
        {
            if (!gamepadInfo.IsConnected) { midiInfo.IsActivated = true; return; }

            var gamepad = LoadGamepad(midiInfo);
            gamepad.PersistentPropertyChanged += GamepadPersistentPropertyChanged;
            gamepad.Acquire();
            gamepad.IsActivated = true;
            ActiveGamepads.Add(gamepad);

            // Open the MIDI port and start listening
            OpenMidiPort(midiInfo, gamepad);
        }
        midiInfo.IsActivated = true;
    }

    public void DeactivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not MidiGamepadInfo midiInfo) return;

        var gamepad = ActiveGamepads.FirstOrDefault(g => g.Id == midiInfo.Id) as MidiGamepad;
        if (gamepad != null)
        {
            gamepad.IsActivated = false;
            gamepad.Unacquire();
            gamepad.PersistentPropertyChanged -= GamepadPersistentPropertyChanged;
            ActiveGamepads.Remove(gamepad);

            CloseMidiPort(midiInfo.PortId);
        }
        midiInfo.IsActivated = false;
    }

    private void OpenMidiPort(MidiGamepadInfo midiInfo, MidiGamepad gamepad)
    {
        try
        {
            if (_openPorts.ContainsKey(midiInfo.PortId)) return;

            var access = MidiAccessManager.Default;
            var portInfo = access.Inputs.FirstOrDefault(p => p.Id == midiInfo.PortId);
            if (portInfo == null) return;

            var port = access.OpenInputAsync(portInfo.Id).GetAwaiter().GetResult();
            port.MessageReceived += (sender, e) => OnMidiMessage(gamepad, e);
            _openPorts[midiInfo.PortId] = port;

            Debug.WriteLine($"[MIDI] Opened port: {midiInfo.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MIDI] Error opening port {midiInfo.Name}: {ex.Message}");
        }
    }

    private void CloseMidiPort(string portId)
    {
        if (_openPorts.TryGetValue(portId, out var port))
        {
            try
            {
                port.CloseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MIDI] Error closing port: {ex.Message}");
            }
            _openPorts.Remove(portId);
        }
    }

    private void OnMidiMessage(MidiGamepad gamepad, MidiReceivedEventArgs e)
    {
        try
        {
            // Parse MIDI message
            if (e.Length < 1) return;

            byte status = e.Data[e.Start];
            int channel = status & 0x0F;
            int messageType = status & 0xF0;

            switch (messageType)
            {
                case 0xB0: // Control Change
                    if (e.Length >= 3)
                    {
                        int ccNumber = e.Data[e.Start + 1];
                        int ccValue = e.Data[e.Start + 2];
                        gamepad.OnControlChange(ccNumber, ccValue);
                    }
                    break;

                case 0x90: // Note On
                    if (e.Length >= 3)
                    {
                        int noteNumber = e.Data[e.Start + 1];
                        int velocity = e.Data[e.Start + 2];
                        if (velocity > 0)
                            gamepad.OnNoteOn(noteNumber);
                        else
                            gamepad.OnNoteOff(noteNumber); // Note On with velocity 0 = Note Off
                    }
                    break;

                case 0x80: // Note Off
                    if (e.Length >= 2)
                    {
                        int noteNumber = e.Data[e.Start + 1];
                        gamepad.OnNoteOff(noteNumber);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MIDI] Message processing error: {ex.Message}");
        }
    }

    private MidiGamepad LoadGamepad(MidiGamepadInfo midiInfo)
    {
        var gamepad = new MidiGamepad(midiInfo, _commandsService, _camerasService.Cameras);

        GamepadSettings? settings = _gamepadSettingsStore.GetGamepadSettingsById(midiInfo.Id);
        if (settings == null) return gamepad;

        gamepad.ZoomProportionalMode = settings.ZoomProportionalMode;
        gamepad.ZoomProportionalFactor = settings.ZoomProportionalFactor;

        var commandsDict = gamepad.Commands.ToDictionary(val => val.GetType().ToString());
        foreach (IInput input in gamepad.Inputs)
        {
            InputSettings? storedInput = settings.Inputs?.FirstOrDefault(i => i.Id == input.Id);
            if (storedInput != null)
            {
                if (storedInput.CommandType != null && commandsDict.TryGetValue(storedInput.CommandType, out var command))
                    input.SelectedCommand = command;
                input.CommandDirection = storedInput.CommandDirection;
                input.CommandValue = storedInput.CommandValue;
                input.Inverted = storedInput.Inverted;
                input.Saturation = storedInput.DeadZoneHigh;
                input.DeadZone = storedInput.DeadZoneLow;
                input.EnableRamping = storedInput.EnableRamping;
                input.RampTime = storedInput.RampTime;
                if (input.SecondInput != null && storedInput.SecondInputSettings != null)
                {
                    if (storedInput.SecondInputSettings.CommandType != null && commandsDict.TryGetValue(storedInput.SecondInputSettings.CommandType, out var secondCommand))
                        input.SecondInput.SelectedCommand = secondCommand;
                    input.SecondInput.CommandDirection = storedInput.SecondInputSettings.CommandDirection;
                    input.SecondInput.CommandValue = storedInput.SecondInputSettings.CommandValue;
                }
            }
        }

        return gamepad;
    }

    private void GamepadPersistentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IGamepad gamepad)
            _gamepadSettingsStore.SaveGamepadSettings(new GamepadSettings(gamepad));
    }
}
