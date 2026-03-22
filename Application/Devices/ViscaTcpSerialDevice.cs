using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.ViscaCommands;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PtzJoystickControl.Application.Devices;

public class ViscaTcpSerialDevice : ViscaTcpSerialDeviceBase
{
    private readonly object _sendLock = new();
    private readonly byte[] tmpBuffer = new byte[16];
    private ushort tmpBuffIndex = 0;

    // Buffer for accumulating received bytes until a complete VISCA message (ending in 0xFF) is received
    private readonly byte[] _receiveAccumulator = new byte[256];
    private int _receiveAccumulatorIndex = 0;

    public ViscaTcpSerialDevice(string name) : this(name, null) { }

    public ViscaTcpSerialDevice(string name, IPEndPoint? viscaEndpoint) : base(name)
    {
        if (viscaEndpoint != null)
        {
            ViscaIpEndpont = viscaEndpoint;
        }
    }

    public override bool Connected => socket != null && socket.Connected;

    public override void BeginSocket()
    {
        if (socket == null)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Connect: Creating TCP socket for {ViscaIpEndpont}");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 500,
                NoDelay = true,
                SendBufferSize = sendBuffer.Length,
                ReceiveBufferSize = receiveBuffer.Length,
            };
        }

        if (!socket.Connected)
        {
            try
            {
                Trace.WriteLine($"[{name}] VISCA/TCP Connect: Connecting to {ViscaIpEndpont}...");
                SocketAsyncEventArgs socketAsyncEvArgs = new SocketAsyncEventArgs() { RemoteEndPoint = ViscaIpEndpont };
                socketAsyncEvArgs.Completed += OnConnected;

                if (!socket.ConnectAsync(socketAsyncEvArgs))
                    OnConnected(null, socketAsyncEvArgs);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"[{name}] VISCA/TCP Connect Error: {ViscaIpEndpont}: {e.Message}");
                EndSocket();
            }
        }
    }

    private void OnConnected(object? _, SocketAsyncEventArgs eventArgs)
    {
        NotifyPropertyChanged(nameof(Connected));
        try
        {
            if (eventArgs.SocketError == SocketError.Success)
            {
                Trace.WriteLine($"[{name}] VISCA/TCP Connect: Connected to {ViscaIpEndpont}");
                OnConnectedSuccessfully();
                _receiveAccumulatorIndex = 0;
                socket!.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);

                if (sendAddressSet)
                {
                    SendAddressSetCommand();
                    SendIfClearCommand();
                }
            }
            else
            {
                Trace.WriteLine($"[{name}] VISCA/TCP Connect: Failed to {ViscaIpEndpont}: {eventArgs.SocketError}");
                OnDisconnected();
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Connect Error: OnConnected {ViscaIpEndpont}: {e.Message}");
            EndSocket();
        }
    }

    private void OnReceive(IAsyncResult res)
    {
        int length = 0;
        try
        {
            length = socket?.EndReceive(res, out var error) ?? 0;
            if (length == 0)
            {
                Trace.WriteLine($"[{name}] VISCA/TCP Recv: Connection closed by remote");
                EndSocket();
                return;
            }
            Trace.WriteLine($"[{name}] VISCA/TCP Recv <- {ViscaIpEndpont}: {BitConverter.ToString(receiveBuffer, 0, length)}");
            ProcessReceivedData(receiveBuffer, length);
            socket!.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Recv Error <- {ViscaIpEndpont}: {e.Message}");
            NotifyPropertyChanged(nameof(Connected));
            EndSocket();
        }
    }

    private void ProcessReceivedData(byte[] buffer, int length)
    {
        for (int i = 0; i < length; i++)
        {
            byte b = buffer[i];
            if (_receiveAccumulatorIndex < _receiveAccumulator.Length)
            {
                _receiveAccumulator[_receiveAccumulatorIndex++] = b;
            }

            if (b == (byte)Terminator.Terminate)
            {
                // Complete VISCA message received
                if (_receiveAccumulatorIndex >= 2)
                {
                    ParseViscaReply(_receiveAccumulator, _receiveAccumulatorIndex);
                }
                _receiveAccumulatorIndex = 0;
            }
        }
    }

    private void ParseViscaReply(byte[] buffer, int length)
    {
        try
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Parse: {BitConverter.ToString(buffer, 0, length)}");

            // RS232C VISCA reply format: [address byte] [reply type | socket] [data...] [0xFF]
            // address byte: 0x90 for device 1 reply (0x80 + device address + 8)
            // Skip address byte, parse reply type
            int startIndex = 1; // Skip address byte
            ViscaCommandParser.ParseReply(this, buffer, startIndex);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Parse Error: {e.Message}");
        }
    }

    public override void EndSocket()
    {
        Trace.WriteLine($"[{name}] VISCA/TCP Disconnect: Closing socket for {ViscaIpEndpont}");
        if (socket != null)
        {
            try
            {
                if (socket.Connected)
                    socket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                socket.Close();
                socket.Dispose();
            }
            catch { }
            socket = null;
            NotifyPropertyChanged(nameof(Connected));
        }
        OnDisconnected();
    }

    // Send Address Set command: 88 30 01 FF
    // This assigns VISCA address 1 to the first device on the chain
    private void SendAddressSetCommand()
    {
        lock (_sendLock)
        {
            sendBuffIndex = 0;
            sendBuffer[sendBuffIndex++] = 0x88;
            sendBuffer[sendBuffIndex++] = 0x30;
            sendBuffer[sendBuffIndex++] = 0x01; // Start address assignment from address 1
            sendBuffer[sendBuffIndex++] = (byte)Terminator.Terminate;
            Trace.WriteLine($"[{name}] VISCA/TCP Init: Sending Address Set: {BitConverter.ToString(sendBuffer, 0, sendBuffIndex)}");
            SendRawCommand();
        }
    }

    // Send IF_Clear command: 88 01 00 01 FF
    // This clears the command buffers in the camera
    private void SendIfClearCommand()
    {
        lock (_sendLock)
        {
            sendBuffIndex = 0;
            sendBuffer[sendBuffIndex++] = 0x88; // Broadcast address
            sendBuffer[sendBuffIndex++] = 0x01; // Command
            sendBuffer[sendBuffIndex++] = 0x00; // Interface
            sendBuffer[sendBuffIndex++] = 0x01; // IF_Clear
            sendBuffer[sendBuffIndex++] = (byte)Terminator.Terminate;
            Trace.WriteLine($"[{name}] VISCA/TCP Init: Sending IF_Clear: {BitConverter.ToString(sendBuffer, 0, sendBuffIndex)}");
            SendRawCommand();
        }
    }

    private void SendRawCommand()
    {
        if (socket == null || !socket.Connected)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Send: Not connected to {ViscaIpEndpont}");
            return;
        }

        try
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Send -> {ViscaIpEndpont}: {BitConverter.ToString(sendBuffer, 0, sendBuffIndex)}");
            socket.Send(sendBuffer, sendBuffIndex, SocketFlags.None);
            lastSendTime = DateTime.UtcNow;
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[{name}] VISCA/TCP Send Error -> {ViscaIpEndpont}: {e.Message}");
            EndSocket();
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
            SendRawCommand();
        }
    }

    // RS232C address byte: 0x80 + device address (default address = 1 -> 0x81)
    private byte AddressByte => (byte)(0x80 | (address & 0x07));

    public override void Power(Power power)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Power;
            tmpBuffer[tmpBuffIndex++] = (byte)power;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
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
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.PanTilt;
            tmpBuffer[tmpBuffIndex++] = panSpeed;
            tmpBuffer[tmpBuffIndex++] = tiltSpeed;
            tmpBuffer[tmpBuffIndex++] = (byte)panDir;
            tmpBuffer[tmpBuffIndex++] = (byte)tiltDir;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void Zoom(byte zoomSpeed, ZoomDir zoomDir)
    {
        byte zoomCmd = zoomDir == ZoomDir.Stop ? (byte)0x00 : (byte)((byte)zoomDir | (zoomSpeed & 0x0f));
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.PanTiltZoomAndLimit;
            tmpBuffer[tmpBuffIndex++] = zoomCmd;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void Focus(byte focusSpeed, FocusDir focusDir)
    {
        byte focusCmd = focusDir == FocusDir.Stop ? (byte)0x00 : (byte)((byte)focusDir | (focusSpeed & 0x0f));
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Focus;
            tmpBuffer[tmpBuffIndex++] = focusCmd;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void FocusMode(FocusMode focusMode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.FocusMode;
            tmpBuffer[tmpBuffIndex++] = (byte)focusMode;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void FocusLock(FocusLock focusLock)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.FocusLock;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.FocusLock;
            tmpBuffer[tmpBuffIndex++] = (byte)focusLock;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void Preset(Preset preset, byte presetNumber)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Preset;
            tmpBuffer[tmpBuffIndex++] = (byte)preset;
            tmpBuffer[tmpBuffIndex++] = presetNumber;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void PresetRecallSpeed(byte value)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
            tmpBuffer[tmpBuffIndex++] = 0x01;
            tmpBuffer[tmpBuffIndex++] = value;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void SetExposureMode(ExposureMode mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.ExposureMode;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustIris(IrisDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Iris;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustShutter(ShutterDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Shutter;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Gain;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void SetWhiteBalanceMode(WhiteBalanceMode mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.WhiteBalanceMode;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void SetBacklightCompensation(BacklightCompensation mode)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.BacklightCompensation;
            tmpBuffer[tmpBuffIndex++] = (byte)mode;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustRedGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.RGain;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustBlueGain(GainDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.BGain;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void AdjustAperture(ApertureDir direction)
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Aperture;
            tmpBuffer[tmpBuffIndex++] = (byte)direction;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void TriggerWhiteBalance()
    {
        BuildAndSend(() =>
        {
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandType.WhiteBalanceTrigger;
            tmpBuffer[tmpBuffIndex++] = 0x05;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void SendInquiry(InquiryType inquiryType)
    {
        InquiryReplyParser = inquiryType switch
        {
            InquiryType.Power => ViscaCommandParser.ParsePowerInquiryReply,
            InquiryType.Zoom => ViscaCommandParser.ParseZoomInquiryReply,
            InquiryType.Focus => ViscaCommandParser.ParseFocusInquiryReply,
            InquiryType.FocusMode => ViscaCommandParser.ParseFocusModeInquiryReply,
            InquiryType.ExposureMode => ViscaCommandParser.ParseExposureModeInquiryReply,
            InquiryType.Iris => ViscaCommandParser.ParseIrisInquiryReply,
            InquiryType.Shutter => ViscaCommandParser.ParseShutterInquiryReply,
            InquiryType.Gain => ViscaCommandParser.ParseGainInquiryReply,
            InquiryType.WhiteBalanceMode => ViscaCommandParser.ParseWhiteBalanceModeInquiryReply,
            InquiryType.RGain => ViscaCommandParser.ParseRGainInquiryReply,
            InquiryType.BGain => ViscaCommandParser.ParseBGainInquiryReply,
            InquiryType.Aperture => ViscaCommandParser.ParseApertureInquiryReply,
            InquiryType.BacklightCompensation => ViscaCommandParser.ParseBacklightInquiryReply,
            _ => null,
        };
        BuildAndSend(() =>
        {
            // 8x 09 04 xx FF
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Inquiry;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
            tmpBuffer[tmpBuffIndex++] = (byte)inquiryType;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }

    public override void SendPanTiltInquiry()
    {
        InquiryReplyParser = ViscaCommandParser.ParsePanTiltPositionInquiryReply;
        BuildAndSend(() =>
        {
            // 8x 09 06 12 FF
            tmpBuffer[tmpBuffIndex++] = AddressByte;
            tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Inquiry;
            tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
            tmpBuffer[tmpBuffIndex++] = (byte)PanTiltInquiryType.Position;
            tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        });
    }
}
