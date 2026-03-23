using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PtzJoystickControl.Application.Services;
using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.Gui.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PtzJoystickControl.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private CameraOverlayWindow? _overlayWindow;
    private readonly CameraOverlayViewModel _overlayViewModel;
    private LogWindow? _logWindow;
    private readonly LogWindowViewModel _logWindowViewModel;
    private readonly Window _window;
    private readonly ICameraSettingsStore _cameraSettingsStore;
    private readonly IGamepadSettingsStore _gamepadSettingsStore;
    private readonly IVmixService _vmixService;

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
        MainWindow window,
        ICameraSettingsStore cameraSettingsStore,
        IGamepadSettingsStore gamepadSettingsStore,
        IVmixService vmixService)
    {
        GamepadsViewModel = gamepadsViewModel;
        CamerasViewModel = camerasViewModel;
        CameraControlViewModel = cameraControlViewModel;
        VmixViewModel = vmixViewModel;
        _window = window;
        _cameraSettingsStore = cameraSettingsStore;
        _gamepadSettingsStore = gamepadSettingsStore;
        _vmixService = vmixService;
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

    public async void ExportConfig()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                DefaultExtension = "json",
                InitialFileName = "PTZJoyControl_Config.json",
            };
            dialog.Filters!.Add(new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } });

            var path = await dialog.ShowAsync(_window);
            if (string.IsNullOrEmpty(path)) return;

            var cameras = _cameraSettingsStore.GetAllCameras()
                .Select(c => new ViscaDeviceSettings(c)).ToList();

            var vmixSettings = new VmixSettings();
            if (_vmixService is VmixService svc)
            {
                vmixSettings.Host = svc.Host;
                vmixSettings.Port = svc.Port;
                vmixSettings.AutoPreview = svc.AutoPreview;
                vmixSettings.AutoCameraSelect = svc.AutoCameraSelect;
                vmixSettings.Enabled = svc.IsConnected;
            }

            var export = new FullConfigExport
            {
                Cameras = cameras,
                VmixSettings = vmixSettings,
                GamepadSettings = _gamepadSettingsStore.GetAllGamepadSettings(),
                ExportedAt = DateTime.UtcNow.ToString("o"),
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export error: {ex.Message}");
        }
    }

    public async void ImportConfig()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = false,
            };
            dialog.Filters!.Add(new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } });

            var result = await dialog.ShowAsync(_window);
            if (result == null || result.Length == 0) return;

            var json = File.ReadAllText(result[0]);
            var config = JsonSerializer.Deserialize<FullConfigExport>(json);
            if (config == null) return;

            // Write config files directly
            string configDir;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".PTZJoystickControl/");
            else
                configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTZJoystickControl/");

            Directory.CreateDirectory(configDir);

            if (config.Cameras != null)
            {
                var camerasJson = JsonSerializer.Serialize(config.Cameras);
                File.WriteAllText(Path.Combine(configDir, "Cameras.json"), camerasJson);
            }

            if (config.VmixSettings != null)
            {
                var vmixJson = JsonSerializer.Serialize(config.VmixSettings);
                File.WriteAllText(Path.Combine(configDir, "VmixSettings.json"), vmixJson);
            }

            if (config.GamepadSettings != null)
            {
                var gamepadsJson = JsonSerializer.Serialize(config.GamepadSettings);
                File.WriteAllText(Path.Combine(configDir, "Gamepads.json"), gamepadsJson);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Import error: {ex.Message}");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
