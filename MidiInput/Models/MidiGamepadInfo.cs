using PtzJoystickControl.Core.Devices;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.MidiInput.Models;

public class MidiGamepadInfo : IGamepadInfo
{
    private bool _isConnected;
    private bool _isActivated;

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PortId { get; init; } = "";

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
