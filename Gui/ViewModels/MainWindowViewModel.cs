using Avalonia.Layout;
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
            }
        };
    }

    public bool AcrylicEnabled { get; }

    public HorizontalAlignment TitleHorizontalAlignment { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? HorizontalAlignment.Left 
        : HorizontalAlignment.Center;

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

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
