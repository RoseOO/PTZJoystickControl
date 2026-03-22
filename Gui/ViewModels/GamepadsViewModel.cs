using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class GamepadsViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly IGamepadsService _gamepadsService;
    private readonly ICamerasService? _camerasService;
    private IGamepadInfo? _selectedGamepadInfo;
    private IGamepad? _selectedGamepad;
    private IEnumerable<InputViewModel>? _inputViewModels = Enumerable.Empty<InputViewModel>();

    public MappingProfileViewModel? MappingProfileViewModel { get; }

    public GamepadsViewModel(IGamepadsService gamepadsService, IMappingProfileStore? mappingProfileStore = null, ICamerasService? camerasService = null)
    {
        _gamepadsService = gamepadsService;
        _camerasService = camerasService;
        if (mappingProfileStore != null)
            MappingProfileViewModel = new MappingProfileViewModel(mappingProfileStore, this);

        // Auto-activate all devices that were previously activated
        foreach (var gp in _gamepadsService.Gamepads.Where(g => g.IsActivated).ToList())
            _gamepadsService.ActivateGamepad(gp);

        // Select the first activated device for viewing/configuring
        SelectedGamepadInfo = _gamepadsService.Gamepads.FirstOrDefault(g => g.IsActivated)
            ?? _gamepadsService.Gamepads.FirstOrDefault();
    }

    public ObservableCollection<IGamepadInfo> AvailableGamepads => _gamepadsService.Gamepads;

    /// <summary>
    /// The currently selected device for viewing/configuring.
    /// Selecting a device does NOT change its activation state.
    /// Use ToggleSelectedDeviceActive to activate/deactivate.
    /// </summary>
    public IGamepadInfo? SelectedGamepadInfo
    {
        get => _selectedGamepadInfo;
        set
        {
            if (_selectedGamepadInfo != value)
            {
                if (_selectedGamepadInfo != null)
                    _selectedGamepadInfo.PropertyChanged -= OnSelecetdGamepadInfoPropertyChanged;

                _selectedGamepadInfo = value;

                if (_selectedGamepadInfo != null)
                    _selectedGamepadInfo.PropertyChanged += OnSelecetdGamepadInfoPropertyChanged;

                // Look up the active gamepad instance if this device is activated
                SelectedGamepad = _gamepadsService.ActiveGamepads.FirstOrDefault(g => g.Id == _selectedGamepadInfo?.Id);
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsSelectedDeviceActive));
            }
        }
    }

    /// <summary>
    /// Whether the currently selected device is active (activated).
    /// Setting this activates/deactivates the selected device.
    /// Multiple devices can be active simultaneously.
    /// </summary>
    public bool IsSelectedDeviceActive
    {
        get => _selectedGamepadInfo?.IsActivated ?? false;
        set
        {
            if (_selectedGamepadInfo == null) return;
            if (value)
            {
                _gamepadsService.ActivateGamepad(_selectedGamepadInfo);
                SelectedGamepad = _gamepadsService.ActiveGamepads.FirstOrDefault(g => g.Id == _selectedGamepadInfo.Id);
            }
            else
            {
                _gamepadsService.DeactivateGamepad(_selectedGamepadInfo);
                SelectedGamepad = null;
            }
            NotifyPropertyChanged();
        }
    }

    public IGamepad? SelectedGamepad
    {
        get => _selectedGamepad;
        set
        {
            if (_selectedGamepad != value)
            {
                if (_selectedGamepad != null)
                    _selectedGamepad.PropertyChanged -= OnSelecetdGamepadPropertyChanged;

                _selectedGamepad = value;

                if (_selectedGamepad != null)
                    _selectedGamepad.PropertyChanged += OnSelecetdGamepadPropertyChanged;

                InputViewModels = _selectedGamepad?.Inputs.Select(i => new InputViewModel(i));
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(ZoomProportionalMode));
                NotifyPropertyChanged(nameof(ZoomProportionalFactor));
                NotifyPropertyChanged(nameof(DeviceSelectedCamera));
            }
        }
    }

    public bool ZoomProportionalMode
    {
        get => _selectedGamepad?.ZoomProportionalMode ?? false;
        set { if (_selectedGamepad != null) { _selectedGamepad.ZoomProportionalMode = value; NotifyPropertyChanged(); } }
    }

    public float ZoomProportionalFactor
    {
        get => _selectedGamepad?.ZoomProportionalFactor ?? 0.5f;
        set { if (_selectedGamepad != null) { _selectedGamepad.ZoomProportionalFactor = value; NotifyPropertyChanged(); } }
    }

    /// <summary>
    /// All available cameras for per-device camera assignment.
    /// </summary>
    public ObservableCollection<ViscaDeviceBase>? AvailableCameras => _camerasService?.Cameras;

    /// <summary>
    /// The camera assigned to the selected device.
    /// Allows per-device camera targeting. Null means "use selected camera from main camera list".
    /// </summary>
    public ViscaDeviceBase? DeviceSelectedCamera
    {
        get => _selectedGamepad?.SelectedCamera;
        set
        {
            if (_selectedGamepad != null)
            {
                _selectedGamepad.SelectedCamera = value;
                NotifyPropertyChanged();
            }
        }
    }

    private void OnSelecetdGamepadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGamepad.IsConnected) && !SelectedGamepad!.IsConnected)
        {
            // Stop all camera movements when gamepad disconnects
            StopCameraMovement(SelectedGamepad.SelectedCamera);
            // Keep the gamepad selected so configuration remains visible even when disconnected
        }

        NotifyPropertyChanged(e.PropertyName ?? "");
    }

    public void OnSelecetdGamepadInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGamepadInfo.IsActivated))
        {
            NotifyPropertyChanged(nameof(IsSelectedDeviceActive));
            if (SelectedGamepad == null && SelectedGamepadInfo!.IsActivated && SelectedGamepadInfo!.IsConnected)
                SelectedGamepad = _gamepadsService.ActiveGamepads.FirstOrDefault(g => g.Id == _selectedGamepadInfo?.Id);
        }

        NotifyPropertyChanged(e.PropertyName ?? "");
    }

    public IEnumerable<InputViewModel>? InputViewModels
    {
        get => _inputViewModels;
        set
        {
            _inputViewModels = value;
            NotifyPropertyChanged();
        }
    }


    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
