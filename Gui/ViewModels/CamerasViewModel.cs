using Avalonia.Threading;
using Avalonia.Utilities;
using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class CamerasViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly ICamerasService _camerasService;
    private readonly GamepadsViewModel _gamepadsViewModel;
    private readonly IVmixService? _vmixService;
    private ViscaDeviceBase? _manuallySelectedCamera;

    public CamerasViewModel(ICamerasService camerasService, GamepadsViewModel gamepadsViewModel, IVmixService? vmixService = null)
    {
        _camerasService = camerasService;
        _gamepadsViewModel = gamepadsViewModel;
        _vmixService = vmixService;
        Cameras = _camerasService.Cameras;

        WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, CamerasViewModel>(camerasService.Cameras, nameof(camerasService.Cameras.CollectionChanged), OnCamerasServicePropertyCahnged);
        WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, CamerasViewModel>(gamepadsViewModel, nameof(gamepadsViewModel.PropertyChanged), OnGamepadsViewModelPropertyCahnged);

        if (_vmixService != null)
        {
            _vmixService.PreviewInputChanged += OnVmixPreviewInputChanged;
        }
    }

    private void OnGamepadsViewModelPropertyCahnged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGamepad.SelectedCamera) || e.PropertyName == nameof(GamepadsViewModel.SelectedGamepad))
        {
            // When gamepad selection changes, notify that SelectedCamera might have changed
            NotifyPropertyChanged(nameof(SelectedCamera));

            // Auto-preview selected camera in VMix if enabled
            if (_vmixService != null && _vmixService.AutoPreview && SelectedCamera != null && SelectedCamera.VmixInputNumber > 0)
            {
                _ = _vmixService.SendPreviewInputAsync(SelectedCamera.VmixInputNumber);
            }
        }
    }

    private void OnCamerasServicePropertyCahnged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyPropertyChanged(nameof(Cameras));
    }

    private void OnVmixPreviewInputChanged(int vmixInputNumber)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var camera = Cameras.FirstOrDefault(c => c.VmixInputNumber == vmixInputNumber);
            if (camera != null && camera != SelectedCamera)
            {
                SelectedCamera = camera;
            }
        });
    }

    public ObservableCollection<ViscaDeviceBase> Cameras { get; }

    public ViscaDeviceBase? SelectedCamera
    {
        get => _gamepadsViewModel.SelectedGamepad?.SelectedCamera ?? _manuallySelectedCamera;
        set
        {
            if (_gamepadsViewModel.SelectedGamepad != null)
            {
                _gamepadsViewModel.SelectedGamepad.SelectedCamera = value;
            }
            else
            {
                if (_manuallySelectedCamera != value)
                {
                    _manuallySelectedCamera = value;
                    NotifyPropertyChanged();

                    // Auto-preview selected camera in VMix if enabled
                    if (_vmixService != null && _vmixService.AutoPreview && value != null && value.VmixInputNumber > 0)
                    {
                        _ = _vmixService.SendPreviewInputAsync(value.VmixInputNumber);
                    }
                }
            }
        }
    }

    public void AddCamera() =>
        _camerasService.AddCamera(new ViscaIpDevice($"Camera {Cameras.Count() + 1}"));

    public void AddSerialCamera() =>
        _camerasService.AddCamera(new ViscaSerialDevice($"Serial {Cameras.Count() + 1}"));

    public void AddTcpSerialCamera() =>
        _camerasService.AddCamera(new ViscaTcpSerialDevice($"VISCA/TCP {Cameras.Count() + 1}"));

    public void RemoveCamera(object camera)
    {
        _camerasService.RemoveCamera((ViscaDeviceBase)camera);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private class CollectionChangedEventArgs
    {
    }
}
