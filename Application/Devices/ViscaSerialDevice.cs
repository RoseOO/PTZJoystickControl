using PtzJoystickControl.Core.Devices;
using System.Diagnostics;
using System.IO.Ports;

namespace PtzJoystickControl.Application.Devices;

public class ViscaSerialDevice : ViscaSerialDeviceBase
{
    private SerialPort? _serialPort;
    private readonly object _sendLock = new();
    private readonly byte[] tmpBuffer = new byte[16];
    private ushort tmpBuffIndex = 0;

    public ViscaSerialDevice(string name) : base(name) { }

    public ViscaSerialDevice(string name, string portName, int baudRate) : base(name)
    {
        this.portName = portName;
        this.baudRate = baudRate;
    }

    public override bool Connected => _serialPort?.IsOpen ?? false;

    public override void BeginPort()
    {
        if (string.IsNullOrEmpty(portName)) return;

        try
        {
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
            NotifyPropertyChanged(nameof(Connected));
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Serial BeginPort {name}: {e.Message}");
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }

    public override void EndPort()
    {
        if (_serialPort != null)
        {
            try
            {
                _serialPort.DataReceived -= OnDataReceived;
                if (_serialPort.IsOpen) _serialPort.Close();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Serial EndPort {name}: {e.Message}");
            }
            _serialPort.Dispose();
            _serialPort = null;
            NotifyPropertyChanged(nameof(Connected));
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            int bytesToRead = _serialPort.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            _serialPort.Read(buffer, 0, bytesToRead);
            LastReceiveTime = DateTime.UtcNow;
            Debug.WriteLine($"Serial Received: {BitConverter.ToString(buffer)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Serial OnDataReceived {name}: {ex.Message}");
        }
    }

    private void SendCommand()
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            BeginPort();
            return;
        }

        lock (_sendLock)
        {
            try
            {
                _serialPort.Write(sendBuffer, 0, sendBuffIndex);
                lastSendTime = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Serial SendCommand {name}: {e.Message}");
                EndPort();
            }
        }
    }

    private void BuildAndSend(Action buildAction)
    {
        lock (_sendLock)
        {
            tmpBuffIndex = 0;
            buildAction();
            sendBuffIndex = 0;
            for (int i = 0; i < tmpBuffIndex; i++)
                sendBuffer[sendBuffIndex++] = tmpBuffer[i];
            SendCommand();
        }
    }

    public override void Power(Power power)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x00;
            tmpBuffer[tmpBuffIndex++] = (byte)power;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void Pan(byte panSpeed, PanDir panDir)
    {
        PanTilt(panSpeed, tiltSpeed, panDir, tiltDir);
    }

    public override void Tilt(byte tiltSpeed, TiltDir tiltDir)
    {
        PanTilt(panSpeed, tiltSpeed, panDir, tiltDir);
    }

    public override void PanTilt(byte panSpeed, byte tiltSpeed, PanDir panDir, TiltDir tiltDir)
    {
        this.panDir = panDir;
        this.tiltDir = tiltDir;
        this.panSpeed = panSpeed;
        this.tiltSpeed = tiltSpeed;

        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x06;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = panSpeed;
            tmpBuffer[tmpBuffIndex++] = tiltSpeed;
            tmpBuffer[tmpBuffIndex++] = (byte)panDir;
            tmpBuffer[tmpBuffIndex++] = (byte)tiltDir;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void Zoom(byte zoomSpeed, ZoomDir zoomDir)
    {
        byte zoomCmd = zoomDir == ZoomDir.Stop ? (byte)0x00 : (byte)((byte)zoomDir | (zoomSpeed & 0x0f));
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x07;
            tmpBuffer[tmpBuffIndex++] = zoomCmd;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void Focus(byte focusSpeed, FocusDir focusDir)
    {
        byte focusCmd = focusDir == FocusDir.Stop ? (byte)0x00 : (byte)((byte)focusDir | (focusSpeed & 0x0f));
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x08;
            tmpBuffer[tmpBuffIndex++] = focusCmd;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void FocusMode(FocusMode focusMode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x38;
            tmpBuffer[tmpBuffIndex++] = (byte)focusMode;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void FocusLock(FocusLock focusLock)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x0A;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x68;
            tmpBuffer[tmpBuffIndex++] = (byte)focusLock;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void Preset(Preset preset, byte presetNumber)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x3F;
            tmpBuffer[tmpBuffIndex++] = (byte)preset;
            tmpBuffer[tmpBuffIndex++] = presetNumber;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void PresetRecallSpeed(byte value)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x06;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = value;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void SetExposureMode(ExposureMode mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x39;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustIris(IrisDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x0B;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustShutter(ShutterDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x0A;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x0C;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void SetWhiteBalanceMode(WhiteBalanceMode mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x35;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void SetBacklightCompensation(BacklightCompensation mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x33;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustRedGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x03;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustBlueGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void AdjustAperture(ApertureDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x02;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public override void TriggerWhiteBalance()
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = 0x81;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = 0x04;
            tmpBuffer[tmpBuffIndex++] = 0x10;
            tmpBuffer[tmpBuffIndex++] = 0x05;
            tmpBuffer[tmpBuffIndex++] = 0xFF;
        });
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }
}
