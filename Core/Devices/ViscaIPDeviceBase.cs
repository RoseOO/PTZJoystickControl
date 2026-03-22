using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PtzJoystickControl.Core.Devices;

public abstract class ViscaIPDeviceBase : ViscaDeviceBase
{
    protected Socket? socket;
    protected Protocol protocol = Protocol.Udp;
    protected bool useHeader = true;
    protected bool singleCommand = true; //TODO: test if allowed or not...
    protected IPEndPoint ViscaIpEndpont;

    private Timer? _reconnectTimer;
    private bool _autoReconnect = true;
    private const int DefaultReconnectIntervalMs = 5000;
    private int _reconnectIntervalMs = DefaultReconnectIntervalMs;
    private volatile bool _reconnecting;

    public ViscaIPDeviceBase(string name) : this(name, null) { }

    public ViscaIPDeviceBase(string name, IPEndPoint? viscaEndpint) : base(name)
    {
        Name = name;
        ViscaIpEndpont = viscaEndpint ?? new IPEndPoint(System.Net.IPAddress.Any, 5678);
    }

    public int remotePacketId { get; set; }

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set
        {
            if (_autoReconnect != value)
            {
                _autoReconnect = value;
                if (value)
                    StartReconnectTimer();
                else
                    StopReconnectTimer();
                NotifyPropertyChanged();
                NotifyPersistentPropertyChanged();
            }
        }
    }

    public bool UseHeader
    {
        get => useHeader;
        set
        {
            useHeader = value;
            NotifyPropertyChanged();
            NotifyPersistentPropertyChanged();
        }
    }

    public bool SingleCommand
    {
        get => singleCommand;
        set
        {
            singleCommand = value;
            NotifyPropertyChanged();
            NotifyPersistentPropertyChanged();
        }
    }

    public Protocol Protocol
    {
        get => protocol;
        set
        {
            if (protocol != value)
            {
                protocol = value;
                OnSocketPropChange();
            }
        }
    }

    public int SendWaitTime
    {
        get => sendWaitTime;
        set => sendWaitTime = value;
    }

    public string IPAddress
    {
        get => ViscaIpEndpont.Address.ToString();
        set
        {
            if (System.Net.IPAddress.TryParse(value, out var ipAddress))
            {

                if (!ViscaIpEndpont.Address.Equals(ipAddress))
                {
                    ViscaIpEndpont.Address = ipAddress;
                    OnSocketPropChange();
                }
            }
            else
                throw new ArgumentException("Cannot parse IPAddress", nameof(value));
        }
    }

    public int Port
    {
        get => ViscaIpEndpont.Port;
        set
        {
            if (ViscaIpEndpont.Port != value)
            {
                ViscaIpEndpont.Port = value;
                OnSocketPropChange();
            }
        }
    }

    private void OnSocketPropChange([CallerMemberName] string propertyName = "")
    {
        try
        {
            EndSocket();
            BeginSocket();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{Name}] OnSocketPropChange Error: {e.Message}");
        }
        NotifyPropertyChanged(propertyName);
        NotifyPersistentPropertyChanged(propertyName);
    }

    public override bool Connected { get { return socket != null && (protocol == Protocol.Udp || socket.Connected || base.Connected); } }

    public abstract void BeginSocket();

    public abstract void EndSocket();

    protected void StartReconnectTimer()
    {
        if (_reconnectTimer != null) return;
        _reconnectTimer = new Timer(AttemptReconnect, null, _reconnectIntervalMs, _reconnectIntervalMs);
    }

    protected void StopReconnectTimer()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }

    private void AttemptReconnect(object? state)
    {
        if (!_autoReconnect || Connected || _reconnecting)
            return;

        // Only reconnect TCP connections (UDP is connectionless)
        if (protocol != Protocol.Tcp)
            return;

        // Skip if IP address is not configured
        if (ViscaIpEndpont.Address.Equals(System.Net.IPAddress.Any))
            return;

        _reconnecting = true;
        try
        {
            Trace.WriteLine($"[{Name}] Auto-Reconnect: Attempting to reconnect to {ViscaIpEndpont}...");
            EndSocket();
            BeginSocket();
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[{Name}] Auto-Reconnect Error: {e.Message}");
        }
        finally
        {
            _reconnecting = false;
        }
    }

    protected void OnDisconnected()
    {
        if (_autoReconnect && protocol == Protocol.Tcp)
            StartReconnectTimer();
    }

    protected void OnConnectedSuccessfully()
    {
        StopReconnectTimer();
    }
}
