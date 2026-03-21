using Avalonia.Utilities;
using PtzJoystickControl.Application.Devices;
using PtzJoystickControl.Core.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class SerialCameraViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly ViscaSerialDeviceBase _camera;

    public static IEnumerable<int> BaudRates { get; } = new[] { 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

    public SerialCameraViewModel(ViscaDeviceBase camera)
    {
        _camera = (ViscaSerialDeviceBase)camera;
        WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, SerialCameraViewModel>(camera, nameof(camera.PropertyChanged), OnCameraPropertyChanged);
    }

    private void OnCameraPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyPropertyChanged(e?.PropertyName ?? "");
    }

    public ViscaDeviceBase Camera { get => _camera; }

    public string Name
    {
        get => _camera.Name;
        set => _camera.Name = value;
    }

    public string PortName
    {
        get => _camera.PortName;
        set => _camera.PortName = value;
    }

    public int BaudRate
    {
        get => _camera.BaudRate;
        set => _camera.BaudRate = value;
    }

    public bool Connected { get => _camera.Connected; }

    public IEnumerable<string> AvailablePorts => ViscaSerialDevice.GetAvailablePorts();

    public void RefreshPorts()
    {
        NotifyPropertyChanged(nameof(AvailablePorts));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;
}
