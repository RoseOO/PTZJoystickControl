using Avalonia.Layout;
using Avalonia.Media;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.Gui.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private CameraOverlayWindow? _overlayWindow;
    private readonly CameraOverlayViewModel _overlayViewModel;
    private LogWindow? _logWindow;
    private readonly LogWindowViewModel _logWindowViewModel;

    private bool _showInputPane = true;
    private bool _showCamerasPane = true;
    private bool _showControlsPane = true;
    private string _currentCameraName = "None";
    private Color _cameraStatusColor = Colors.Gray;
    private string _vmixStatusText = "Disconnected";
    private ViscaDeviceBase? _currentCamera;
    private PropertyChangedEventHandler? _cameraPropertyHandler;

    public GamepadsViewModel GamepadsViewModel { get; }
    public CamerasViewModel CamerasViewModel { get; }
    public CameraControlViewModel CameraControlViewModel { get; }
    public VmixViewModel VmixViewModel { get; }

    public MainWindowViewModel(
        GamepadsViewModel gamepadsViewModel,
        CamerasViewModel camerasViewModel,
        CameraControlViewModel cameraControlViewModel,
        VmixViewModel vmixViewModel,
        MainWindow window)
    {
        GamepadsViewModel = gamepadsViewModel;
        CamerasViewModel = camerasViewModel;
        CameraControlViewModel = cameraControlViewModel;
        VmixViewModel = vmixViewModel;
        AcrylicEnabled = window?.ActualTransparencyLevel == Avalonia.Controls.WindowTransparencyLevel.AcrylicBlur
            || window?.ActualTransparencyLevel == Avalonia.Controls.WindowTransparencyLevel.Blur;

        _overlayViewModel = new CameraOverlayViewModel
        {
            Cameras = camerasViewModel.Cameras,
        };

        camerasViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CamerasViewModel.SelectedCamera))
            {
                CameraControlViewModel.Camera = camerasViewModel.SelectedCamera;
                _overlayViewModel.SelectedCamera = camerasViewModel.SelectedCamera;
                _overlayViewModel.Refresh();
                UpdateCameraStatus(camerasViewModel.SelectedCamera);
            }
        };

        // Subscribe to camera collection changes to update overlay
        camerasViewModel.Cameras.CollectionChanged += (s, e) =>
        {
            _overlayViewModel.Refresh();
        };

        // Subscribe to vMix status changes
        vmixViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VmixViewModel.IsConnected))
            {
                VmixStatusText = vmixViewModel.IsConnected ? "Connected" : "Disconnected";
            }
        };

        _logWindowViewModel = new LogWindowViewModel();
    }

    public bool AcrylicEnabled { get; }

    public HorizontalAlignment TitleHorizontalAlignment { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? HorizontalAlignment.Left 
        : HorizontalAlignment.Center;

    // Collapsible pane states
    public bool ShowInputPane
    {
        get => _showInputPane;
        set { _showInputPane = value; NotifyPropertyChanged(); }
    }

    public bool ShowCamerasPane
    {
        get => _showCamerasPane;
        set { _showCamerasPane = value; NotifyPropertyChanged(); }
    }

    public bool ShowControlsPane
    {
        get => _showControlsPane;
        set { _showControlsPane = value; NotifyPropertyChanged(); }
    }

    // Status bar properties
    public string CurrentCameraName
    {
        get => _currentCameraName;
        set { _currentCameraName = value; NotifyPropertyChanged(); }
    }

    public Color CameraStatusColor
    {
        get => _cameraStatusColor;
        set { _cameraStatusColor = value; NotifyPropertyChanged(); }
    }

    public string VmixStatusText
    {
        get => _vmixStatusText;
        set { _vmixStatusText = value; NotifyPropertyChanged(); }
    }

    private void UpdateCameraStatus(ViscaDeviceBase? camera)
    {
        // Unsubscribe from previous camera
        if (_currentCamera != null && _cameraPropertyHandler != null)
        {
            _currentCamera.PropertyChanged -= _cameraPropertyHandler;
        }

        _currentCamera = camera;

        if (camera == null)
        {
            CurrentCameraName = "None";
            CameraStatusColor = Colors.Gray;
            _cameraPropertyHandler = null;
            return;
        }

        CurrentCameraName = camera.Name ?? "Unnamed";
        CameraStatusColor = camera.Connected ? Color.Parse("#4CAF50") : Color.Parse("#F44336");

        // Subscribe to camera property changes for live status updates
        _cameraPropertyHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(ViscaDeviceBase.Connected))
                CameraStatusColor = camera.Connected ? Color.Parse("#4CAF50") : Color.Parse("#F44336");
            if (e.PropertyName == nameof(ViscaDeviceBase.Name))
                CurrentCameraName = camera.Name ?? "Unnamed";
        };
        camera.PropertyChanged += _cameraPropertyHandler;
    }

    public void ToggleOverlay()
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }
        else
        {
            _overlayWindow = new CameraOverlayWindow
            {
                DataContext = _overlayViewModel,
            };
            _overlayWindow.Closed += (s, e) => _overlayWindow = null;
            _overlayWindow.Show();
        }
    }

    public void ToggleDebugLog()
    {
        if (_logWindow != null)
        {
            _logWindow.Close();
            _logWindow = null;
        }
        else
        {
            _logWindow = new LogWindow
            {
                DataContext = _logWindowViewModel,
            };
            _logWindow.Closed += (s, e) => _logWindow = null;
            _logWindow.Show();
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
