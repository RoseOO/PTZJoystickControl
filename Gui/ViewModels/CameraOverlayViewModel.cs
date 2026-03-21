using PtzJoystickControl.Core.Devices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class CameraOverlayViewModel : ViewModelBase, INotifyPropertyChanged
{
    private ObservableCollection<ViscaDeviceBase> _cameras = new();
    private ViscaDeviceBase? _selectedCamera;

    public ObservableCollection<ViscaDeviceBase> Cameras
    {
        get => _cameras;
        set { _cameras = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(CameraInfos)); }
    }

    public ViscaDeviceBase? SelectedCamera
    {
        get => _selectedCamera;
        set { _selectedCamera = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(CameraInfos)); }
    }

    public IEnumerable<CameraInfoItem> CameraInfos => _cameras.Select((cam, i) => new CameraInfoItem
    {
        Number = i + 1,
        Name = cam.Name,
        IsConnected = cam.Connected,
        IsSelected = cam == _selectedCamera,
    });

    public void Refresh()
    {
        NotifyPropertyChanged(nameof(CameraInfos));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class CameraInfoItem
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public bool IsConnected { get; set; }
    public bool IsSelected { get; set; }
    public string StatusText => IsConnected ? "Connected" : "Disconnected";
    public string StatusColor => IsConnected ? "LightGreen" : "#FFFF9090";
    public string StatusBackground => IsSelected ? "#FF2266DD" : "#FF555555";
}
