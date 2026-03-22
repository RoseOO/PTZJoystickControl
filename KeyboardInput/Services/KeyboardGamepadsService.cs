using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.KeyboardInput.Devices;
using PtzJoystickControl.KeyboardInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PtzJoystickControl.KeyboardInput.Services;

public class KeyboardGamepadsService : IGamepadsService
{
    private readonly IGamepadSettingsStore _gamepadSettingsStore;
    private readonly ICamerasService _camerasService;
    private readonly ICommandsService _commandsService;

    public ObservableCollection<IGamepadInfo> Gamepads { get; } = new();
    public ObservableCollection<IGamepad> ActiveGamepads { get; } = new();

    // Keyboard key-to-input mapping
    private static readonly Dictionary<string, string> KeyToAxisMap = new()
    {
        { "D", "Horizontal Axis (A/D)" },    // Right on horizontal axis
        { "A", "Horizontal Axis (A/D)" },    // Left on horizontal axis
        { "W", "Vertical Axis (W/S)" },      // Up on vertical axis
        { "S", "Vertical Axis (W/S)" },      // Down on vertical axis
        { "R", "Zoom Axis (R/F)" },          // Zoom in
        { "F", "Zoom Axis (R/F)" },          // Zoom out
        { "T", "Focus Axis (T/G)" },         // Focus far
        { "G", "Focus Axis (T/G)" },         // Focus near
    };

    private static readonly Dictionary<string, float> KeyToAxisValue = new()
    {
        { "D", 1f },   // Right
        { "A", -1f },  // Left
        { "W", -1f },  // Up (inverted Y)
        { "S", 1f },   // Down
        { "R", 1f },   // Zoom in
        { "F", -1f },  // Zoom out
        { "T", 1f },   // Focus far
        { "G", -1f },  // Focus near
    };

    // Track which axis-keys are currently held
    private readonly HashSet<string> _heldAxisKeys = new();

    private static readonly Dictionary<string, string> KeyToButtonMap = new()
    {
        { "D1", "Key 1" }, { "D2", "Key 2" }, { "D3", "Key 3" },
        { "D4", "Key 4" }, { "D5", "Key 5" }, { "D6", "Key 6" },
        { "D7", "Key 7" }, { "D8", "Key 8" }, { "D9", "Key 9" },
        { "D0", "Key 0" },
        { "Q", "Key Q" }, { "E", "Key E" },
        { "Z", "Key Z" }, { "X", "Key X" }, { "C", "Key C" },
        { "V", "Key V" }, { "Space", "Key Space" }, { "Return", "Key Enter" },
    };

    public KeyboardGamepadsService(IGamepadSettingsStore gamepadSettingsStore, ICamerasService camerasService, ICommandsService commandsService)
    {
        _gamepadSettingsStore = gamepadSettingsStore;
        _camerasService = camerasService;
        _commandsService = commandsService;

        // Create the keyboard device info
        var keyboardInfo = new KeyboardGamepadInfo();

        // Check for saved settings
        var savedSettings = _gamepadSettingsStore.GetGamepadSettingsById(keyboardInfo.Id);
        if (savedSettings != null)
        {
            keyboardInfo.IsActivated = savedSettings.IsActivated;
        }

        Gamepads.Add(keyboardInfo);
    }

    public void ActivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not KeyboardGamepadInfo keyboardInfo) return;

        if (!ActiveGamepads.Any(g => g.Id == gamepadInfo.Id))
        {
            var gamepad = LoadGamepad(keyboardInfo);
            gamepad.PersistentPropertyChanged += GamepadPersistentPropertyChanged;
            gamepad.Acquire();
            gamepad.IsActivated = true;
            ActiveGamepads.Add(gamepad);
        }
        keyboardInfo.IsActivated = true;
    }

    public void DeactivateGamepad(IGamepadInfo gamepadInfo)
    {
        if (gamepadInfo is not KeyboardGamepadInfo keyboardInfo) return;

        var gamepad = ActiveGamepads.FirstOrDefault(g => g.Id == keyboardInfo.Id) as KeyboardGamepad;
        if (gamepad != null)
        {
            gamepad.IsActivated = false;
            gamepad.Unacquire();
            gamepad.PersistentPropertyChanged -= GamepadPersistentPropertyChanged;
            ActiveGamepads.Remove(gamepad);
        }
        keyboardInfo.IsActivated = false;
    }

    /// <summary>
    /// Called from the UI layer when a key is pressed. Returns true if the key was consumed.
    /// </summary>
    public bool OnKeyDown(string keyName)
    {
        var gamepad = ActiveGamepads.FirstOrDefault() as KeyboardGamepad;
        if (gamepad == null) return false;

        // Axis keys
        if (KeyToAxisMap.TryGetValue(keyName, out var axisName) && KeyToAxisValue.TryGetValue(keyName, out var value))
        {
            _heldAxisKeys.Add(keyName);
            gamepad.SetInputValue(axisName, value);
            return true;
        }

        // Button keys
        if (KeyToButtonMap.TryGetValue(keyName, out var buttonName))
        {
            gamepad.SetInputValue(buttonName, 1f);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Called from the UI layer when a key is released. Returns true if the key was consumed.
    /// </summary>
    public bool OnKeyUp(string keyName)
    {
        var gamepad = ActiveGamepads.FirstOrDefault() as KeyboardGamepad;
        if (gamepad == null) return false;

        // Axis keys - reset to 0 on release, unless the opposite key is still held
        if (KeyToAxisMap.TryGetValue(keyName, out var axisName))
        {
            _heldAxisKeys.Remove(keyName);

            // Check if the opposite direction key is still held
            var oppositeValue = GetOppositeHeldAxisValue(axisName);
            gamepad.SetInputValue(axisName, oppositeValue);
            return true;
        }

        // Button keys
        if (KeyToButtonMap.TryGetValue(keyName, out var buttonName))
        {
            gamepad.SetInputValue(buttonName, 0f);
            return true;
        }

        return false;
    }

    private float GetOppositeHeldAxisValue(string axisName)
    {
        foreach (var heldKey in _heldAxisKeys)
        {
            if (KeyToAxisMap.TryGetValue(heldKey, out var mappedAxis) && mappedAxis == axisName)
            {
                if (KeyToAxisValue.TryGetValue(heldKey, out var value))
                    return value;
            }
        }
        return 0f;
    }

    private KeyboardGamepad LoadGamepad(KeyboardGamepadInfo gamepadInfo)
    {
        var gamepad = new KeyboardGamepad(gamepadInfo, _commandsService, _camerasService.Cameras);

        GamepadSettings? settings = _gamepadSettingsStore.GetGamepadSettingsById(gamepadInfo.Id);
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
                input.DefaultCenter = storedInput.DefaultCenter;
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
