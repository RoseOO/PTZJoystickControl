using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.SdlGamepads.Models;
using SDL2;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.SdlGamepads.Devices;

public class SdlGamepad : IGamepad
{
    private static readonly string[] AxisNames = new string[] {
        "Axis X",
        "Axis Y",
        "Axis Z",
        "Axis 1",
        "Axis 2",
        "Axis 3",
        "Axis 4",
        "Axis 5",
    };
    private const string buttonNameFormatString = "Button {0}";
    private const string hatButtonNameFormatString = "DPad {0} {1}";
    private static readonly string[] HatDirectionNames = new string[] { "Up", "Right", "Down", "Left" };
    private static readonly byte[] HatDirectionMasks = new byte[] { SDL.SDL_HAT_UP, SDL.SDL_HAT_RIGHT, SDL.SDL_HAT_DOWN, SDL.SDL_HAT_LEFT };
    private List<ICommand> commands;
    private bool isActivated;
    private bool isConnected;

    public string Id { get; }
    public string Name { get; }
    internal IntPtr sdlJoystickPointer;
    internal int DeviceIndex { get; set; }
    internal int InstanceId{ get; set; }
    public bool DetectInput { get; set; } = false;

    private readonly Dictionary<string, IInput> inputs = new();
    private ViscaDeviceBase? selectedCamera;
    private ObservableCollection<ViscaDeviceBase>? cameras;
    private bool zoomProportionalMode = false;
    private float zoomProportionalFactor = 0.5F; // Default 50% reduction at full zoom

    public bool IsActivated { 
        get => isActivated;
        set {
            if (isActivated != value)
            {
                isActivated = value;
                NotifyPersistentPropertyChanged();
            }
            NotifyPropertyChanged();
        }
    }

    public bool IsConnected { 
        get => isConnected;
        set
        {
            isConnected = value;
            NotifyPropertyChanged();
        }
    }

    public ObservableCollection<ViscaDeviceBase>? Cameras
    {
        get => cameras;
        set
        {
            cameras = value;
            NotifyPropertyChanged();
        }
    }


    public ViscaDeviceBase? SelectedCamera
    {
        get => selectedCamera;
        set
        {
            if (selectedCamera != value)
            {
                // Stop all movements on the previously selected camera
                StopCameraMovement(selectedCamera);

                selectedCamera = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool ZoomProportionalMode
    {
        get => zoomProportionalMode;
        set
        {
            zoomProportionalMode = value;
            NotifyPersistentPropertyChanged();
            NotifyPropertyChanged();
        }
    }

    public float ZoomProportionalFactor
    {
        get => zoomProportionalFactor;
        set
        {
            zoomProportionalFactor = Math.Max(0.1F, Math.Min(value, 0.9F)); // Clamp between 10% and 90%
            NotifyPersistentPropertyChanged();
            NotifyPropertyChanged();
        }
    }

    public IReadOnlyList<ICommand> Commands => commands.AsReadOnly();

    public IEnumerable<IInput> Inputs => inputs.Values;

    internal SdlGamepad(SdlGamepadInfo gamepadInfo, ICommandsService commandsService, ObservableCollection<ViscaDeviceBase> cameras)
    {
        Name = gamepadInfo.Name;
        Cameras = cameras;

        commands = commandsService.GetCommandsForGamepad(this).ToList();

        Id = gamepadInfo.Id;
        DeviceIndex = gamepadInfo.DeviceIndex;
        InstanceId = gamepadInfo.InstanceId;
        sdlJoystickPointer = SDL.SDL_JoystickOpen(DeviceIndex);
        
        if (sdlJoystickPointer == IntPtr.Zero)
            throw new Exception("Failed to open joystick");

        //Get Axis inputs
        var numAxis = SDL.SDL_JoystickNumAxes(sdlJoystickPointer);
        for (int i = 0; i < numAxis; i++)
        {
            IInput newInput = new Input(AxisNames[i], AxisNames[i], InputType.Axis, commands.AsReadOnly());
            newInput.PersistentPropertyChanged += (sender, e) => NotifyPersistentPropertyChanged("");
            inputs.Add(AxisNames[i], newInput);
        }

        //Get Button inputs
        var numButtons = SDL.SDL_JoystickNumButtons(sdlJoystickPointer);
        for (int i = 0; i < numButtons; i++)
        {
            var name = string.Format(buttonNameFormatString, i + 1);
            var id = string.Format(buttonNameFormatString, i);
            IInput newInput = new Input(name, id, InputType.Button, commands.AsReadOnly());
            newInput.PersistentPropertyChanged += (sender, e) => NotifyPersistentPropertyChanged("");
            inputs.Add(id, newInput);
        }

        //Get Hat/DPad inputs
        var numHats = SDL.SDL_JoystickNumHats(sdlJoystickPointer);
        for (int i = 0; i < numHats; i++)
        {
            for (int d = 0; d < HatDirectionNames.Length; d++)
            {
                var name = string.Format(hatButtonNameFormatString, i + 1, HatDirectionNames[d]);
                var id = string.Format(hatButtonNameFormatString, i, HatDirectionNames[d]);
                IInput newInput = new Input(name, id, InputType.Button, commands.AsReadOnly());
                newInput.PersistentPropertyChanged += (sender, e) => NotifyPersistentPropertyChanged("");
                inputs.Add(id, newInput);
            }
        }
    }

    public void Acquire()
    {
    }

    public void Unacquire()
    {
        SDL.SDL_JoystickClose(sdlJoystickPointer);
    }

    public void Update()
    {
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

    internal void OnButtonEvent(SDL.SDL_JoyButtonEvent jbutton)
    {
        if (inputs.TryGetValue(string.Format(buttonNameFormatString, jbutton.button), out var input))
            input.InputValue = jbutton.state;
    }

    internal void OnHatEvent(SDL.SDL_JoyHatEvent jhat)
    {
        for (int d = 0; d < HatDirectionNames.Length; d++)
        {
            var id = string.Format(hatButtonNameFormatString, jhat.hat, HatDirectionNames[d]);
            if (inputs.TryGetValue(id, out var input))
                input.InputValue = (jhat.hatValue & HatDirectionMasks[d]) != 0 ? 1 : 0;
        }
    }

    internal void OnAxisEvent(SDL.SDL_JoyAxisEvent jaxis)
    {
        if (inputs.TryGetValue(AxisNames[jaxis.axis], out var input))
            input.InputValue = Util.Map(jaxis.axisValue, short.MinValue, short.MaxValue, -1, 1);
    }

    private void StopCameraMovement(ViscaDeviceBase? camera)
    {
        if (camera == null)
            return;

        // Send stop commands for pan, tilt, and zoom
        camera.Pan(0, PanDir.Stop);
        camera.Tilt(0, TiltDir.Stop);
        camera.Zoom(0, ZoomDir.Stop);
    }
}
