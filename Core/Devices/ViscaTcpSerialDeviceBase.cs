using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Core.Devices;

public abstract class ViscaTcpSerialDeviceBase : ViscaIPDeviceBase
{
    protected bool sendAddressSet = true;

    public ViscaTcpSerialDeviceBase(string name) : base(name)
    {
        protocol = Protocol.Tcp;
        useHeader = false;
        singleCommand = true;
    }

    public ViscaTcpSerialDeviceBase(string name, Protocol protocol) : base(name)
    {
        this.protocol = protocol;
        useHeader = false;
        singleCommand = true;
    }

    public bool SendAddressSet
    {
        get => sendAddressSet;
        set
        {
            if (sendAddressSet != value)
            {
                sendAddressSet = value;
                NotifyPropertyChanged();
                NotifyPersistentPropertyChanged();
            }
        }
    }
}
