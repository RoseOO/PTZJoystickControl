namespace PtzJoystickControl.Core.Devices;

public abstract class ViscaSerialDeviceBase : ViscaDeviceBase
{
    protected string portName = "";
    protected int baudRate = 9600;

    public ViscaSerialDeviceBase(string name) : base(name) { }

    public string PortName
    {
        get => portName;
        set
        {
            if (portName != value)
            {
                portName = value;
                OnPortSettingChanged();
            }
        }
    }

    public int BaudRate
    {
        get => baudRate;
        set
        {
            if (baudRate != value)
            {
                baudRate = value;
                OnPortSettingChanged();
            }
        }
    }

    private void OnPortSettingChanged()
    {
        EndPort();
        BeginPort();
        NotifyPropertyChanged(nameof(PortName));
        NotifyPersistentPropertyChanged(nameof(PortName));
    }

    public abstract void BeginPort();
    public abstract void EndPort();
}
