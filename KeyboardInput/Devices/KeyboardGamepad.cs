using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.KeyboardInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.KeyboardInput.Devices;

public class KeyboardGamepad : IGamepad
{
    private static readonly string[] AxisNames = new[] {
        "Horizontal Axis (A/D)",
        "Vertical Axis (W/S)",
        "Zoom Axis (R/F)",
        "Focus Axis (T/G)",
    };

    private static readonly string[] ButtonNames = new[] {
        "Key 1", "Key 2", "Key 3", "Key 4", "Key 5",
        "Key 6", "Key 7", "Key 8", "Key 9", "Key 0",
        "Key Q", "Key E", "Key Z", "Key X", "Key C",
        "Key V", "Key Space", "Key Enter",
    };

    private readonly List<ICommand> _commands;
    private readonly Dictionary<string, IInput> _inputs = new();

    private bool _isActivated;
    private bool _isConnected = true;
    private ViscaDeviceBase? _selectedCamera;
    private ObservableCollection<ViscaDeviceBase>? _cameras;
    private bool _zoomProportionalMode;
    private float _zoomProportionalFactor = 0.5F;

    public string Id { get; }
    public string Name { get; }
    public bool DetectInput { get; set; }

    public bool IsActivated
    {
        get => _isActivated;
        set
        {
            if (_isActivated != value)
            {
                _isActivated = value;
                NotifyPersistentPropertyChanged();
            }
            NotifyPropertyChanged();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            NotifyPropertyChanged();
        }
    }

    public ObservableCollection<ViscaDeviceBase>? Cameras
    {
        get => _cameras;
        set
        {
            _cameras = value;
            NotifyPropertyChanged();
        }
    }

    public ViscaDeviceBase? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (_selectedCamera != value)
            {
                StopCameraMovement(_selectedCamera);
                _selectedCamera = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool ZoomProportionalMode
    {
        get => _zoomProportionalMode;
        set
        {
            _zoomProportionalMode = value;
            NotifyPersistentPropertyChanged();
            NotifyPropertyChanged();
        }
    }

    public float ZoomProportionalFactor
    {
        get => _zoomProportionalFactor;
        set
        {
            _zoomProportionalFactor = Math.Max(0.1F, Math.Min(value, 0.9F));
            NotifyPersistentPropertyChanged();
            NotifyPropertyChanged();
        }
    }

    public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();
    public IEnumerable<IInput> Inputs => _inputs.Values;

    internal KeyboardGamepad(KeyboardGamepadInfo gamepadInfo, ICommandsService commandsService, ObservableCollection<ViscaDeviceBase> cameras)
    {
        Id = gamepadInfo.Id;
        Name = gamepadInfo.Name;
        Cameras = cameras;

        _commands = commandsService.GetCommandsForGamepad(this).ToList();

        // Create axis inputs
        foreach (var name in AxisNames)
        {
            IInput input = new Input(name, name, InputType.Axis, _commands.AsReadOnly());
            input.PersistentPropertyChanged += (s, e) => NotifyPersistentPropertyChanged("");
            _inputs.Add(name, input);
        }

        // Create button inputs
        foreach (var name in ButtonNames)
        {
            IInput input = new Input(name, name, InputType.Button, _commands.AsReadOnly());
            input.PersistentPropertyChanged += (s, e) => NotifyPersistentPropertyChanged("");
            _inputs.Add(name, input);
        }
    }

    public void Acquire() { }
    public void Unacquire() { }
    public void Update() { }

    /// <summary>
    /// Set the value of a named input. Called by the keyboard service when keys are pressed/released.
    /// </summary>
    internal bool SetInputValue(string inputName, float value)
    {
        if (_inputs.TryGetValue(inputName, out var input))
        {
            input.InputValue = Math.Clamp(value, -1f, 1f);
            return true;
        }
        return false;
    }

    internal IInput? GetInput(string inputName)
    {
        _inputs.TryGetValue(inputName, out var input);
        return input;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler? PersistentPropertyChanged;
    private void NotifyPersistentPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PersistentPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void StopCameraMovement(ViscaDeviceBase? camera)
    {
        if (camera == null) return;
        camera.Pan(0, PanDir.Stop);
        camera.Tilt(0, TiltDir.Stop);
        camera.Zoom(0, ZoomDir.Stop);
    }
}
