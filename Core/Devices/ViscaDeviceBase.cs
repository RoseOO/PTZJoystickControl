using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Core.Devices;

public abstract class ViscaDeviceBase : INotifyPropertyChanged
{
    protected string name = null!;
    protected byte address = 0x01;
    protected int sendWaitTime = 30;
    protected byte[] sendBuffer = new byte[128];
    protected byte[] receiveBuffer = new byte[128];
    protected int sendBuffIndex = 0;
    protected int receiveBuffIndex = 0;
    protected DateTime lastSendTime = DateTime.UtcNow;
    protected DateTime lastReceiveTime = DateTime.MinValue;

    protected byte powerCmd;

    protected byte panSpeed = 0x04;
    protected byte tiltSpeed = 0x04;
    protected PanDir panDir = PanDir.Stop;
    protected TiltDir tiltDir = TiltDir.Stop;

    protected byte zoomCmd = 0x00;

    protected byte focusCmd;
    protected byte focusModeCmd;
    protected byte focusLockCmd;

    protected byte presetCmd;
    protected byte presetCmdNumber;
    protected byte presetRecallSpeed = 0x04;

    protected byte exposureModeCmd;
    protected byte irisCmd;
    protected byte shutterCmd;
    protected byte gainCmd;
    protected byte whiteBalanceModeCmd;
    protected byte backlightCmd;
    protected byte redGainCmd;
    protected byte blueGainCmd;
    protected byte apertureCmd;

    private int vmixInputNumber = 0;


    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler? PersistentPropertyChanged;
    protected void NotifyPersistentPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PersistentPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ViscaDeviceBase(string name)
    {
        Name = name;
    }
    
    protected internal Action<ViscaDeviceBase, byte[], int>? InquiryReplyParser { get; set; }
    protected internal bool Acked { get; internal set; }
    protected internal bool Completed { get; internal set; }

    public string Name
    {
        get { return name; }
        set
        {
            if (name == value)
                return;

            if(string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Name cannot be empty", nameof(value));

                name = value;
                NotifyPropertyChanged();
                NotifyPersistentPropertyChanged();
            
        }
    }

    public byte ViscaAddress
    {
        get => address;
        set => address = 0 < value && value < 128 
            ? value
            : throw new ArgumentOutOfRangeException($"ViscaAddress must be between 1 and 127. {value} was given.");
    }

    public int VmixInputNumber
    {
        get => vmixInputNumber;
        set
        {
            if (vmixInputNumber != value)
            {
                vmixInputNumber = value;
                NotifyPropertyChanged();
                NotifyPersistentPropertyChanged();
            }
        }
    }

    protected DateTime LastSendTime
    {
        get => lastSendTime;
        set {
            var tempConnected = Connected;
            lastSendTime = value;
            if (tempConnected != Connected)
                NotifyPropertyChanged(nameof(Connected));
        }
    }

    protected internal DateTime LastReceiveTime {
        get => lastReceiveTime;
        set
        {
            var tempConnected = Connected;
            lastReceiveTime = value;
            if (tempConnected != Connected)
                NotifyPropertyChanged(nameof(Connected));
        }
    }

    public virtual bool Connected { 
        get {
            var t = lastSendTime - lastReceiveTime;
            return t <= TimeSpan.FromMilliseconds(500) && t >= TimeSpan.FromMilliseconds(-500); 
        } 
    }
    
    public Power? PowerState { get; protected internal set; }

    // Camera feedback properties
    private ushort? _zoomPosition;
    public ushort? ZoomPosition
    {
        get => _zoomPosition;
        protected internal set { _zoomPosition = value; NotifyPropertyChanged(); }
    }

    private short? _panPosition;
    public short? PanPosition
    {
        get => _panPosition;
        protected internal set { _panPosition = value; NotifyPropertyChanged(); }
    }

    private short? _tiltPosition;
    public short? TiltPosition
    {
        get => _tiltPosition;
        protected internal set { _tiltPosition = value; NotifyPropertyChanged(); }
    }

    private ushort? _focusPosition;
    public ushort? FocusPosition
    {
        get => _focusPosition;
        protected internal set { _focusPosition = value; NotifyPropertyChanged(); }
    }

    private FocusMode? _focusModeState;
    public FocusMode? FocusModeState
    {
        get => _focusModeState;
        protected internal set { _focusModeState = value; NotifyPropertyChanged(); }
    }

    private ExposureMode? _exposureModeState;
    public ExposureMode? ExposureModeState
    {
        get => _exposureModeState;
        protected internal set { _exposureModeState = value; NotifyPropertyChanged(); }
    }

    private ushort? _irisPosition;
    public ushort? IrisPosition
    {
        get => _irisPosition;
        protected internal set { _irisPosition = value; NotifyPropertyChanged(); }
    }

    private ushort? _shutterPosition;
    public ushort? ShutterPosition
    {
        get => _shutterPosition;
        protected internal set { _shutterPosition = value; NotifyPropertyChanged(); }
    }

    private ushort? _gainPosition;
    public ushort? GainPosition
    {
        get => _gainPosition;
        protected internal set { _gainPosition = value; NotifyPropertyChanged(); }
    }

    private WhiteBalanceMode? _whiteBalanceModeState;
    public WhiteBalanceMode? WhiteBalanceModeState
    {
        get => _whiteBalanceModeState;
        protected internal set { _whiteBalanceModeState = value; NotifyPropertyChanged(); }
    }

    private ushort? _rGainPosition;
    public ushort? RGainPosition
    {
        get => _rGainPosition;
        protected internal set { _rGainPosition = value; NotifyPropertyChanged(); }
    }

    private ushort? _bGainPosition;
    public ushort? BGainPosition
    {
        get => _bGainPosition;
        protected internal set { _bGainPosition = value; NotifyPropertyChanged(); }
    }

    private ushort? _aperturePosition;
    public ushort? AperturePosition
    {
        get => _aperturePosition;
        protected internal set { _aperturePosition = value; NotifyPropertyChanged(); }
    }

    private BacklightCompensation? _backlightState;
    public BacklightCompensation? BacklightState
    {
        get => _backlightState;
        protected internal set { _backlightState = value; NotifyPropertyChanged(); }
    }

    public abstract void Power(Power byteVal);
    public abstract void Pan(byte panSpeed, PanDir panDir);
    public abstract void Tilt(byte tiltSpeed, TiltDir tiltDir);
    public abstract void PanTilt(byte panSpeed, byte tiltSpeed, PanDir panDir, TiltDir tiltDir);
    public abstract void Zoom(byte zoomSpeed, ZoomDir zoomDir);
    public abstract void Focus(byte focusSpeed, FocusDir focusDir);
    public abstract void FocusMode(FocusMode focusMode);
    public abstract void FocusLock(FocusLock focusMode);
    public abstract void Preset(Preset preset, byte presetNumber);
    public abstract void PresetRecallSpeed(byte value);
    public abstract void SetExposureMode(ExposureMode mode);
    public abstract void AdjustIris(IrisDir direction);
    public abstract void AdjustShutter(ShutterDir direction);
    public abstract void AdjustGain(GainDir direction);
    public abstract void SetWhiteBalanceMode(WhiteBalanceMode mode);
    public abstract void SetBacklightCompensation(BacklightCompensation mode);
    public abstract void AdjustRedGain(GainDir direction);
    public abstract void AdjustBlueGain(GainDir direction);
    public abstract void AdjustAperture(ApertureDir direction);
    public abstract void TriggerWhiteBalance();

    // Inquiry methods
    public abstract void SendInquiry(InquiryType inquiryType);
    public abstract void SendPanTiltInquiry();

    // Polling support
    private Timer? _pollTimer;
    private bool _pollingEnabled;

    public bool PollingEnabled
    {
        get => _pollingEnabled;
        set
        {
            if (_pollingEnabled == value) return;
            _pollingEnabled = value;
            if (value)
                StartPolling();
            else
                StopPolling();
            NotifyPropertyChanged();
        }
    }

    private void StartPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = new Timer(PollCamera, null, 0, 2000);
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private void PollCamera(object? state)
    {
        if (!Connected) return;
        try
        {
            SendInquiry(InquiryType.Zoom);
            SendInquiry(InquiryType.Focus);
            SendInquiry(InquiryType.FocusMode);
            SendInquiry(InquiryType.ExposureMode);
            SendInquiry(InquiryType.Iris);
            SendInquiry(InquiryType.Shutter);
            SendInquiry(InquiryType.Gain);
            SendInquiry(InquiryType.WhiteBalanceMode);
            SendInquiry(InquiryType.RGain);
            SendInquiry(InquiryType.BGain);
            SendInquiry(InquiryType.Aperture);
            SendInquiry(InquiryType.BacklightCompensation);
            SendPanTiltInquiry();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{Name}] PollCamera Error: {e.Message}");
        }
    }
}
