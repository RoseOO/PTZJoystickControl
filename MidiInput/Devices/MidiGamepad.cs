using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.MidiInput.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.MidiInput.Devices;

public class MidiGamepad : IGamepad
{
    private readonly List<ICommand> _commands;
    private readonly Dictionary<string, IInput> _inputs = new();

    private bool _isActivated;
    private bool _isConnected;
    private ViscaDeviceBase? _selectedCamera;
    private ObservableCollection<ViscaDeviceBase>? _cameras;
    private bool _zoomProportionalMode;
    private float _zoomProportionalFactor = 0.5F;

    public string Id { get; }
    public string Name { get; }
    public bool DetectInput { get; set; }

    // MIDI CC 0-127 midpoint for centering (127 / 2)
    private const float MidiCcMidpoint = 63.5f;

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

    internal MidiGamepad(MidiGamepadInfo gamepadInfo, ICommandsService commandsService, ObservableCollection<ViscaDeviceBase> cameras)
    {
        Id = gamepadInfo.Id;
        Name = gamepadInfo.Name;
        Cameras = cameras;
        _isConnected = gamepadInfo.IsConnected;

        _commands = commandsService.GetCommandsForGamepad(this).ToList();
        // No predefined inputs - they are created dynamically via MIDI learn
    }

    public void Acquire() { }
    public void Unacquire() { }
    public void Update() { }

    /// <summary>
    /// Get an existing input or create a new one dynamically (MIDI learn).
    /// </summary>
    internal IInput GetOrCreateInput(string name, InputType inputType)
    {
        if (!_inputs.TryGetValue(name, out var input))
        {
            input = new Input(name, name, inputType, _commands.AsReadOnly());
            input.PersistentPropertyChanged += (s, e) => NotifyPersistentPropertyChanged("");
            _inputs[name] = input;
            NotifyPropertyChanged(nameof(Inputs));
        }
        return input;
    }

    /// <summary>
    /// Process a MIDI CC (Control Change) message.
    /// </summary>
    internal void OnControlChange(int ccNumber, int value)
    {
        var name = $"CC {ccNumber + 1}";
        var input = GetOrCreateInput(name, InputType.Axis);
        float normalized = (value / MidiCcMidpoint) - 1f;
        input.InputValue = Math.Clamp(normalized, -1f, 1f);
    }

    /// <summary>
    /// Process a MIDI Note On message.
    /// </summary>
    internal void OnNoteOn(int noteNumber)
    {
        var name = $"Note {noteNumber}";
        var input = GetOrCreateInput(name, InputType.Button);
        input.InputValue = 1f;
    }

    /// <summary>
    /// Process a MIDI Note Off message.
    /// </summary>
    internal void OnNoteOff(int noteNumber)
    {
        var name = $"Note {noteNumber}";
        if (_inputs.TryGetValue(name, out var input))
            input.InputValue = 0f;
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
