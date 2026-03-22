using Avalonia.Data;
using Avalonia.Utilities;
using PtzJoystickControl.Core.Devices;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class TcpSerialCameraViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly ViscaTcpSerialDeviceBase _camera;

    public TcpSerialCameraViewModel(ViscaDeviceBase camera)
    {
        _camera = (ViscaTcpSerialDeviceBase)camera;
        WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, TcpSerialCameraViewModel>(camera, nameof(camera.PropertyChanged), OnCameraPropertyChanged);
    }

    private void OnCameraPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyPropertyChanged(e?.PropertyName ?? "");
    }

    public ViscaDeviceBase Camera { get => _camera; }

    public string Name
    {
        get => _camera.Name;
        set
        {
            try
            {
                _camera.Name = value;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new DataValidationException(e.Message);
            }
        }
    }

    public string IPAddress
    {
        get => _camera.IPAddress;
        set
        {
            try
            {
                _camera.IPAddress = value;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new DataValidationException("Invalid IP");
            }
        }
    }

    public int Port
    {
        get => _camera.Port;
        set
        {
            try
            {
                _camera.Port = value;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new DataValidationException("1-65535");
            }
        }
    }

    public bool SendAddressSet
    {
        get => _camera.SendAddressSet;
        set => _camera.SendAddressSet = value;
    }

    public bool Connected { get => _camera.Connected; }

    public int VmixInputNumber
    {
        get => _camera.VmixInputNumber;
        set => _camera.VmixInputNumber = value;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return Name;
    }
}
