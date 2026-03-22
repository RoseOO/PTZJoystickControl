using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.OscInput.Devices;
using PtzJoystickControl.OscInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PtzJoystickControl.OscInput.Services;

public class OscGamepadsService : IGamepadsService
{
    private readonly IGamepadSettingsStore _gamepadSettingsStore;
    private readonly ICamerasService _camerasService;
    private readonly ICommandsService _commandsService;

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;

    public const int DefaultPort = 9000;

    public ObservableCollection<IGamepadInfo> Gamepads { get; } = new();
    public ObservableCollection<IGamepad> ActiveGamepads { get; } = new();

    public OscGamepadsService(IGamepadSettingsStore gamepadSettingsStore, ICamerasService camerasService, ICommandsService commandsService)
    {
        _gamepadSettingsStore = gamepadSettingsStore;
        _camerasService = camerasService;
        _commandsService = commandsService;

        // Create the OSC device info
        var oscInfo = new OscGamepadInfo
        {
            Id = $"OSC:{DefaultPort}",
            Name = $"OSC (UDP:{DefaultPort})",
            Port = DefaultPort,
            IsConnected = true,
        };

        var saved = _gamepadSettingsStore.GetGamepadSettingsById(oscInfo.Id);
        if (saved != null)
            oscInfo.IsActivated = saved.IsActivated;

        Gamepads.Add(oscInfo);
    }

    public void ActivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not OscGamepadInfo oscInfo) return;

        if (!ActiveGamepads.Any(g => g.Id == gamepadInfo.Id))
        {
            var gamepad = LoadGamepad(oscInfo);
            gamepad.PersistentPropertyChanged += GamepadPersistentPropertyChanged;
            gamepad.Acquire();
            gamepad.IsActivated = true;
            ActiveGamepads.Add(gamepad);

            StartListening(oscInfo.Port, gamepad);
        }
        oscInfo.IsActivated = true;
    }

    public void DeactivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not OscGamepadInfo oscInfo) return;

        var gamepad = ActiveGamepads.FirstOrDefault(g => g.Id == oscInfo.Id) as OscGamepad;
        if (gamepad != null)
        {
            gamepad.IsActivated = false;
            gamepad.Unacquire();
            gamepad.PersistentPropertyChanged -= GamepadPersistentPropertyChanged;
            ActiveGamepads.Remove(gamepad);

            StopListening();
        }
        oscInfo.IsActivated = false;
    }

    private void StartListening(int port, OscGamepad gamepad)
    {
        StopListening();

        try
        {
            _udpClient = new UdpClient(port);
            _cts = new CancellationTokenSource();
            Task.Run(() => ReceiveLoop(gamepad, _cts.Token));
            Debug.WriteLine($"[OSC] Listening on UDP port {port}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OSC] Error starting listener on port {port}: {ex.Message}");
        }
    }

    private void StopListening()
    {
        _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        _cts = null;
    }

    private async Task ReceiveLoop(OscGamepad gamepad, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient!.ReceiveAsync();
                var message = OscParser.Parse(result.Buffer, result.Buffer.Length);
                if (message != null)
                    ProcessOscMessage(gamepad, message);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OSC] Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessOscMessage(OscGamepad gamepad, OscParser.OscMessage message)
    {
        // Try to set the input value using the OSC address as the input name
        float value = 0f;
        if (message.Arguments.Length > 0)
        {
            value = message.Arguments[0] switch
            {
                float f => f,
                int i => i / 127f, // Normalize int values similar to MIDI
                bool b => b ? 1f : 0f,
                _ => 0f,
            };
        }

        // Try direct address mapping (e.g., /pan, /tilt, /button/1)
        if (gamepad.SetInputValue(message.Address, value))
            return;

        // Try remapped patterns: /ptz/pan -> /pan, /control/fader1 -> /fader1
        var lastSegment = message.Address.Contains('/') && message.Address.Length > 1
            ? "/" + message.Address.Split('/').Last()
            : message.Address;

        gamepad.SetInputValue(lastSegment, value);
    }

    private OscGamepad LoadGamepad(OscGamepadInfo oscInfo)
    {
        var gamepad = new OscGamepad(oscInfo, _commandsService, _camerasService.Cameras);

        GamepadSettings? settings = _gamepadSettingsStore.GetGamepadSettingsById(oscInfo.Id);
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
