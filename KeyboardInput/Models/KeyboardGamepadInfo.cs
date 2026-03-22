using PtzJoystickControl.Core.Devices;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.KeyboardInput.Models;

public class KeyboardGamepadInfo : IGamepadInfo
{
    private bool _isConnected = true;
    private bool _isActivated;

    public string Id { get; init; } = "Keyboard#0";
    public string Name { get; init; } = "Keyboard";

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsActivated
    {
        get => _isActivated;
        set
        {
            if (_isActivated != value)
            {
                _isActivated = value;
                NotifyPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;
}
