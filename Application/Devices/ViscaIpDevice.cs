using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Utils;
using PtzJoystickControl.Core.ViscaCommands;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PtzJoystickControl.Application.Devices;

public class ViscaIpDevice : ViscaIPDeviceBase
{
    private readonly object sendRecieveLock = new();
    private readonly HashSet<Func<ViscaIPHeaderType>> commandsToSend = new();
    private readonly List<Func<ViscaIPHeaderType>> commandsToRemoveFromSet = new();

    byte[] tmpBuffer = new byte[16];
    ushort tmpBuffIndex = 0;
    private uint packetId = 0;

    public ViscaIpDevice(string name) : this(name, null) { }

    public ViscaIpDevice(string name, IPEndPoint? viscaEndpint) : base(name, viscaEndpint) { }

    public override void BeginSocket()
    {
        if (socket == null)
        {
            if (protocol == Protocol.Udp)
            {
                Debug.WriteLine($"[{name}] IP Connect: Opening UDP socket for {ViscaIpEndpont}");
                socket = UdpSocket.GetInstance();
                UdpSocket.AddListenerCallback(ViscaIpEndpont, ParseViscaIPReply);
                return;
            }
            Debug.WriteLine($"[{name}] IP Connect: Creating TCP socket for {ViscaIpEndpont}");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 500,
                NoDelay = true,
                SendBufferSize = sendBuffer.Length,
                ReceiveBufferSize = receiveBuffer.Length,
            };
        }

        if (protocol == Protocol.Tcp && !socket.Connected)
        {
            try
            {
                Debug.WriteLine($"[{name}] IP Connect: TCP connecting to {ViscaIpEndpont}...");
                SocketAsyncEventArgs socketAsyncEvArgs = new SocketAsyncEventArgs() { RemoteEndPoint = ViscaIpEndpont };
                socketAsyncEvArgs.Completed += OnConnected;

                if (!socket.ConnectAsync(socketAsyncEvArgs))
                    OnConnected(null, socketAsyncEvArgs);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{name}] IP Connect Error: TCP BeginSocket {ViscaIpEndpont}: {e.Message}");
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
                Debug.WriteLine($"[{name}] IP Connect: TCP connected to {ViscaIpEndpont}");
                socket!.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, new AsyncCallback(OnRecieve), null);
            }
            else
            {
                Debug.WriteLine($"[{name}] IP Connect: TCP connection failed to {ViscaIpEndpont}: {eventArgs.SocketError}");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{name}] IP Connect Error: TCP OnConnected {ViscaIpEndpont}: {eventArgs?.SocketError} {e.Message}");
            EndSocket();
        }
    }

    private void OnRecieve(IAsyncResult res)
    {
        IPEndPoint remoteIPEndPoint = new IPEndPoint(System.Net.IPAddress.Any, 0);
        EndPoint remoteEndPoint = remoteIPEndPoint;

        int length = 0;
        try
        {
            length = socket?.EndReceive(res, out var error) ?? 0;
            Debug.WriteLine($"[{name}] IP Recv [TCP] <- {ViscaIpEndpont}: {BitConverter.ToString(receiveBuffer, 0, length)}");
            ParseViscaIPReply(receiveBuffer, length);
            socket!.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, new AsyncCallback(OnRecieve), null);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{name}] IP Recv Error [TCP] <- {ViscaIpEndpont}: {e.Message}");
            NotifyPropertyChanged(nameof(Connected));
            EndSocket();
        }
    }

    public override void EndSocket()
    {
        Debug.WriteLine($"[{name}] IP Disconnect: Closing socket for {ViscaIpEndpont}");
        if (socket != null)
        {
            EventHandler<SocketAsyncEventArgs> onComplete = (sender, e) =>
            {
                NotifyPropertyChanged(nameof(Connected));
                if (socket != UdpSocket.GetInstance())
                    socket.Dispose();
                socket = null;
            };

            SocketAsyncEventArgs socketAsyncEvArgs = new SocketAsyncEventArgs();
            socketAsyncEvArgs.Completed += onComplete;

            //Manually invoke on complete if DisconnectAsync completed synchronusly.
            if (!socket.Connected || !socket.DisconnectAsync(socketAsyncEvArgs))
                onComplete.Invoke(null, null!);
        }
        UdpSocket.RemoveListenerCallback(ViscaIpEndpont);
    }

    public void ParseViscaIPReply(byte[] buffer, int length)
    {
        ViscaIPCommandParser.ParseReply(this, buffer, length);
    }

    // Inquiry methods
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
        AddCommand(() => AddCameraInquiry(inquiryType));
    }

    public override void SendPanTiltInquiry()
    {
        InquiryReplyParser = ViscaCommandParser.ParsePanTiltPositionInquiryReply;
        AddCommand(AddPanTiltPositionInquiry);
    }

    private ViscaIPHeaderType AddCameraInquiry(InquiryType inquiryType)
    {
        // 81 09 04 xx FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Inquiry;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)inquiryType;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Inquery;
    }

    private ViscaIPHeaderType AddPanTiltPositionInquiry()
    {
        // 81 09 06 12 FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Inquiry;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
        tmpBuffer[tmpBuffIndex++] = (byte)PanTiltInquiryType.Position;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Inquery;
    }

    public override void Power(Power power)
    {
        powerCmd = (byte)power;
        AddCommand(AddPowerCommand);
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
        bool panTiltChanged = false;
        if (this.panDir != panDir || this.tiltDir != tiltDir)
        {
            panTiltChanged = true;
        }
        else if ((this.panDir != PanDir.Stop || this.tiltDir != TiltDir.Stop) && (this.panSpeed != panSpeed || this.tiltSpeed != tiltSpeed))
        {
            panTiltChanged = true;
        }

        this.panDir = panDir;
        this.tiltDir = tiltDir;
        this.panSpeed = panSpeed;
        this.tiltSpeed = tiltSpeed;

        if (panTiltChanged)
        {
            AddCommand(AddPanTiltCommand);
        }
    }

    public override void Zoom(byte zoomSpeed, ZoomDir zoomDir)
    {
        //zoomCmd byte is split in direction and speed. 1 is dir and 2 is speed in 0x12
        bool zoomChanged = false;
        if ((zoomCmd & 0xf0) != (byte)zoomDir)
        { //Direction changed
            zoomChanged = true;
        }
        else if (zoomDir != (byte)ZoomDir.Stop && (zoomCmd & 0x0f) != zoomSpeed)
        { //Speed changed
            zoomChanged = true;
        }

        zoomCmd = zoomDir == (byte)ZoomDir.Stop ? (byte)0x00 : (byte)((byte)zoomDir | zoomSpeed & 0x0f);

        if (zoomChanged)
        {
            AddCommand(AddZoomCommand);
        }
    }

    public override void Focus(byte focusSpeed, FocusDir focusDir)
    {
        bool focusChanged = false;
        if ((focusCmd & 0xf0) != (byte)focusDir)
        {
            focusChanged = true;
        }
        else if (focusDir != (byte)FocusDir.Stop && (focusCmd & 0x0f) != focusSpeed)
        {
            focusChanged = true;
        }

        focusCmd = focusDir == (byte)FocusDir.Stop ? (byte)0x00 : (byte)((byte)focusDir | focusSpeed & 0x0f);

        if (focusChanged)
        {
            AddCommand(AddFocusCommand);
        }
    }

    public override void FocusMode(FocusMode focusMode)
    {
        focusModeCmd = (byte)focusMode;
        AddCommand(AddFocusModeCommand);
    }

    public override void FocusLock(FocusLock focusLock)
    {
        focusLockCmd = (byte)focusLock;
        AddCommand(AddFocusLockCommand);
    }

    public override void Preset(Preset preset, byte presetNumber)
    {
        presetCmd = (byte)preset;
        presetCmdNumber = presetNumber;
        AddCommand(AddPresetCommand);
    }

    public override void PresetRecallSpeed(byte value)
    {
        presetRecallSpeed = value;
        AddCommand(AddPresetRecallSpeedCommand);
    }

    public override void SetExposureMode(ExposureMode mode)
    {
        exposureModeCmd = (byte)mode;
        AddCommand(AddExposureModeCommand);
    }

    public override void AdjustIris(IrisDir direction)
    {
        irisCmd = (byte)direction;
        AddCommand(AddIrisCommand);
    }

    public override void AdjustShutter(ShutterDir direction)
    {
        shutterCmd = (byte)direction;
        AddCommand(AddShutterCommand);
    }

    public override void AdjustGain(GainDir direction)
    {
        gainCmd = (byte)direction;
        AddCommand(AddGainCommand);
    }

    public override void SetWhiteBalanceMode(WhiteBalanceMode mode)
    {
        whiteBalanceModeCmd = (byte)mode;
        AddCommand(AddWhiteBalanceModeCommand);
    }

    public override void SetBacklightCompensation(BacklightCompensation mode)
    {
        backlightCmd = (byte)mode;
        AddCommand(AddBacklightCommand);
    }

    public override void AdjustRedGain(GainDir direction)
    {
        redGainCmd = (byte)direction;
        AddCommand(AddRedGainCommand);
    }

    public override void AdjustBlueGain(GainDir direction)
    {
        blueGainCmd = (byte)direction;
        AddCommand(AddBlueGainCommand);
    }

    public override void AdjustAperture(ApertureDir direction)
    {
        apertureCmd = (byte)direction;
        AddCommand(AddApertureCommand);
    }

    public override void TriggerWhiteBalance()
    {
        AddCommand(AddWhiteBalanceTriggerCommand);
    }

    public ViscaIPHeaderType AddPowerCommand()
    {
        // 81 01 04 00 0X FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Power;
        tmpBuffer[tmpBuffIndex++] = powerCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    public ViscaIPHeaderType AddPowerInquiry()
    {
        // 81 09 04 00 FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Inquiry;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)InquiryType.Power;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        InquiryReplyParser = ViscaCommandParser.ParsePowerInquiryReply;
        return ViscaIPHeaderType.Inquery;
    }

    private ViscaIPHeaderType AddPanTiltCommand()
    {
        // 81 01 06 01 VV WW XX YY FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.PanTilt;
        tmpBuffer[tmpBuffIndex++] = panSpeed;
        tmpBuffer[tmpBuffIndex++] = tiltSpeed;
        tmpBuffer[tmpBuffIndex++] = (byte)panDir;
        tmpBuffer[tmpBuffIndex++] = (byte)tiltDir;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddZoomCommand()
    {
        // 81 01 04 07 xp FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.PanTiltZoomAndLimit;
        tmpBuffer[tmpBuffIndex++] = zoomCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddFocusCommand()
    {
        // 81 01 04 08 2p FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Focus;
        tmpBuffer[tmpBuffIndex++] = focusCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddFocusModeCommand()
    {
        // 81 01 04 08 2p FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.FocusMode;
        tmpBuffer[tmpBuffIndex++] = focusModeCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddFocusLockCommand()
    {
        // 81 0a 04 68 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.FocusLock;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.FocusLock;
        tmpBuffer[tmpBuffIndex++] = focusLockCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddPresetCommand()
    {
        // 81 01 04 3F 0x pp FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Preset;
        tmpBuffer[tmpBuffIndex++] = presetCmd;
        tmpBuffer[tmpBuffIndex++] = presetCmdNumber;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddPresetRecallSpeedCommand()
    {
        // 81 01 06 01 pp FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.PanTilt;
        tmpBuffer[tmpBuffIndex++] = 0x01;
        tmpBuffer[tmpBuffIndex++] = presetRecallSpeed;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddExposureModeCommand()
    {
        // 81 01 04 39 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.ExposureMode;
        tmpBuffer[tmpBuffIndex++] = exposureModeCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddIrisCommand()
    {
        // 81 01 04 0B 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Iris;
        tmpBuffer[tmpBuffIndex++] = irisCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddShutterCommand()
    {
        // 81 01 04 0A 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Shutter;
        tmpBuffer[tmpBuffIndex++] = shutterCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddGainCommand()
    {
        // 81 01 04 0C 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Gain;
        tmpBuffer[tmpBuffIndex++] = gainCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddWhiteBalanceModeCommand()
    {
        // 81 01 04 35 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.WhiteBalanceMode;
        tmpBuffer[tmpBuffIndex++] = whiteBalanceModeCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddBacklightCommand()
    {
        // 81 01 04 33 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.BacklightCompensation;
        tmpBuffer[tmpBuffIndex++] = backlightCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddRedGainCommand()
    {
        // 81 01 04 03 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.RGain;
        tmpBuffer[tmpBuffIndex++] = redGainCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddBlueGainCommand()
    {
        // 81 01 04 04 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.BGain;
        tmpBuffer[tmpBuffIndex++] = blueGainCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddApertureCommand()
    {
        // 81 01 04 02 0x FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.Aperture;
        tmpBuffer[tmpBuffIndex++] = apertureCmd;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    private ViscaIPHeaderType AddWhiteBalanceTriggerCommand()
    {
        // 81 01 04 10 05 FF
        tmpBuffer[tmpBuffIndex++] = 0x81;
        tmpBuffer[tmpBuffIndex++] = (byte)PacketType.Command;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandCategory.Camera;
        tmpBuffer[tmpBuffIndex++] = (byte)CommandType.WhiteBalanceTrigger;
        tmpBuffer[tmpBuffIndex++] = 0x05;
        tmpBuffer[tmpBuffIndex++] = (byte)Terminator.Terminate;
        return ViscaIPHeaderType.Command;
    }

    public void AddHeader(ViscaIPHeaderType commandType, ushort payloadLength)
    {
        sendBuffer[sendBuffIndex++] = (byte)((ushort)commandType >> 8);
        sendBuffer[sendBuffIndex++] = (byte)commandType;
        sendBuffer[sendBuffIndex++] = (byte)(payloadLength >> 8);
        sendBuffer[sendBuffIndex++] = (byte)payloadLength;
        sendBuffer[sendBuffIndex++] = (byte)(packetId >> 24);
        sendBuffer[sendBuffIndex++] = (byte)(packetId >> 16);
        sendBuffer[sendBuffIndex++] = (byte)(packetId >> 8);
        sendBuffer[sendBuffIndex++] = (byte)packetId;
        packetId++;
    }

    private async void AddCommand(Func<ViscaIPHeaderType> commandFunction)
    {
        lock (commandsToSend)
        {
            commandsToSend.Add(commandFunction);
        }

        while (await Task.Run(() => trySendCommandsWithLock()))
        {
            lock (commandsToSend)
            {
                if (commandsToSend.Count == 0)
                {
                    break;
                }
            }
        };
    }

    private bool trySendCommandsWithLock()
    {
        bool hadLock = false;
        try
        {
            if (Monitor.TryEnter(sendRecieveLock))
            {
                if ((DateTime.UtcNow - lastSendTime).Milliseconds >= SendWaitTime)
                {
                    buildCommand();
                    SendCommand();
                    lastSendTime = DateTime.UtcNow;
                }
            }
        }
        finally
        {
            if (Monitor.IsEntered(sendRecieveLock))
            {
                Monitor.Exit(sendRecieveLock);
                hadLock = true;
            }
        }
        return hadLock;
    }

    private void buildCommand()
    {
        sendBuffIndex = 0;
        lock (commandsToSend)
        {
            foreach (var command in commandsToSend)
            {
                tmpBuffIndex = 0;
                var headerType = command();

                ushort cmdLen = tmpBuffIndex;
                if (useHeader)
                    cmdLen += 8;

                if (sendBuffIndex + cmdLen <= sendBuffer.Length)
                {
                    if (useHeader)
                        AddHeader(headerType, tmpBuffIndex);

                    for (int i = 0; i < tmpBuffIndex; i++)
                        sendBuffer[sendBuffIndex++] = tmpBuffer[i];

                    commandsToRemoveFromSet.Add(command);
                }
                else
                    break;

                if (singleCommand) 
                    break;
            }
            foreach (var commandToRemove in commandsToRemoveFromSet)
            {
                commandsToSend.Remove(commandToRemove);
            }
        }
        commandsToRemoveFromSet.Clear();
    }

    public void SendCommand()
    {
        if (socket == null)
        {
            Debug.WriteLine($"[{name}] IP Send: No socket for {ViscaIpEndpont} - initiating connection");
            BeginSocket();
            return;
        }


        if (protocol == Protocol.Tcp && !socket.Connected)
        {
            Debug.WriteLine($"[{name}] IP Send: TCP not connected to {ViscaIpEndpont} - reconnecting");
            BeginSocket();
            return;
        }

        try
        {
            Debug.WriteLine($"[{name}] IP Send [{protocol}] -> {ViscaIpEndpont}: {BitConverter.ToString(sendBuffer, 0, sendBuffIndex)}");
            socket.SendTo(sendBuffer, sendBuffIndex, SocketFlags.None, ViscaIpEndpont);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{name}] IP Send Error [{protocol}] -> {ViscaIpEndpont}: {e.Message}");
            EndSocket();
        }
    }

}
