using PtzJoystickControl.Core.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PtzJoystickControl.Application.Services;

namespace PtzJoystickControl.Gui.ViewModels;

public class VmixViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly IVmixService _vmixService;

    public VmixViewModel(IVmixService vmixService)
    {
        _vmixService = vmixService;
    }

    public string Host
    {
        get => _vmixService.Host;
        set { _vmixService.Host = value; NotifyPropertyChanged(); }
    }

    public int Port
    {
        get => _vmixService.Port;
        set { _vmixService.Port = value; NotifyPropertyChanged(); }
    }

    public bool AutoPreview
    {
        get => _vmixService.AutoPreview;
        set { _vmixService.AutoPreview = value; NotifyPropertyChanged(); }
    }

    public bool AutoCameraSelect
    {
        get => _vmixService.AutoCameraSelect;
        set { _vmixService.AutoCameraSelect = value; NotifyPropertyChanged(); }
    }

    public bool IsConnected => _vmixService.IsConnected;

    public async void Connect()
    {
        await _vmixService.ConnectAsync();
        NotifyPropertyChanged(nameof(IsConnected));
        if (_vmixService is VmixService svc)
            svc.SaveSettings();
    }

    public void Disconnect()
    {
        _vmixService.Disconnect();
        NotifyPropertyChanged(nameof(IsConnected));
        if (_vmixService is VmixService svc)
            svc.SaveSettings();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
