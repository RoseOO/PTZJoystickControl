using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.OscInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.OscInput.Devices;

public class OscGamepad : IGamepad
{
    private readonly List<ICommand> _commands;
    private readonly Dictionary<string, IInput> _inputs = new();

    private bool _isActivated;
    private bool _isConnected;
    private ViscaDeviceBase? _selectedCamera;
    private ObservableCollection<ViscaDeviceBase>? _cameras;
    private bool _zoomProportionalMode;
    private float _zoomProportionalFactor = 0.5F;

    // OSC axes: /pan, /tilt, /zoom, /focus + 32 user-defined faders
    private static readonly string[] AxisNames = new[] {
        "/pan", "/tilt", "/zoom", "/focus",
        "/fader1", "/fader2", "/fader3", "/fader4",
        "/fader5", "/fader6", "/fader7", "/fader8",
        "/fader9", "/fader10", "/fader11", "/fader12",
        "/fader13", "/fader14", "/fader15", "/fader16",
        "/fader17", "/fader18", "/fader19", "/fader20",
        "/fader21", "/fader22", "/fader23", "/fader24",
        "/fader25", "/fader26", "/fader27", "/fader28",
        "/fader29", "/fader30", "/fader31", "/fader32",
    };

    // OSC buttons: /button/1 through /button/64
    private const int NumButtons = 64;

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
        set { _isConnected = value; NotifyPropertyChanged(); }
    }

    public ObservableCollection<ViscaDeviceBase>? Cameras
    {
        get => _cameras;
        set { _cameras = value; NotifyPropertyChanged(); }
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

    internal OscGamepad(OscGamepadInfo gamepadInfo, ICommandsService commandsService, ObservableCollection<ViscaDeviceBase> cameras)
    {
        Id = gamepadInfo.Id;
        Name = gamepadInfo.Name;
        Cameras = cameras;
        _isConnected = true;

        _commands = commandsService.GetCommandsForGamepad(this).ToList();

        foreach (var name in AxisNames)
        {
            IInput input = new Input(name, name, InputType.Axis, _commands.AsReadOnly());
            input.PersistentPropertyChanged += (s, e) => NotifyPersistentPropertyChanged("");
            _inputs.Add(name, input);
        }

        for (int i = 0; i < NumButtons; i++)
        {
            var name = $"/button/{i + 1}";
            IInput input = new Input(name, name, InputType.Button, _commands.AsReadOnly());
            input.PersistentPropertyChanged += (s, e) => NotifyPersistentPropertyChanged("");
            _inputs.Add(name, input);
        }
    }

    public void Acquire() { }
    public void Unacquire() { }
    public void Update() { }

    /// <summary>
    /// Set a named OSC input value. Address should match an axis or button name.
    /// </summary>
    internal bool SetInputValue(string address, float value)
    {
        if (_inputs.TryGetValue(address, out var input))
        {
            input.InputValue = Math.Clamp(value, -1f, 1f);
            return true;
        }
        return false;
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
