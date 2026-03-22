using PtzJoystickControl.Core.Devices;
using System.Diagnostics;

namespace PtzJoystickControl.Core.ViscaCommands;

public static class ViscaCommandParser
{
    public static int ParseReplyViscaAddress(byte[] buffer, out int startIndex)
    {
        throw new NotImplementedException();
    }

    public static void ParseReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        
        if (buffer.Length - startIndex < 2) throw new ArgumentException("Reply min length is 2 bytes.");

        //byte socket = (byte)(buffer[startIndex] & 0x0F); // not used
        byte replyType = (byte)(buffer[startIndex++] & 0xF0);

        switch (replyType)
        {
            case (byte)ReplyType.Ack:
                if (buffer[startIndex++] != (byte)Terminator.Terminate)
                    throw new Exception("Invalid response. Ack not terminated.");
                viscaDevice.Acked = true;
                viscaDevice.LastReceiveTime = DateTime.UtcNow;
                Debug.WriteLine($"[{viscaDevice.Name}] VISCA Reply: ACK");
                break;
            case (byte)ReplyType.Complete:
                if (buffer[startIndex] != (byte)Terminator.Terminate)
                {
                    var replyParser = viscaDevice.InquiryReplyParser;
                    replyParser?.Invoke(viscaDevice, buffer, startIndex);
                }
                viscaDevice.Acked = true;
                viscaDevice.Completed = true;
                viscaDevice.LastReceiveTime = DateTime.UtcNow;
                Debug.WriteLine($"[{viscaDevice.Name}] VISCA Reply: Complete");
                break;
            case (byte)ReplyType.Error:
                Debug.WriteLine($"[{viscaDevice.Name}] VISCA Reply: Error - {BitConverter.ToString(buffer, startIndex - 1)}");
                break;
            default:
                Debug.WriteLine($"[{viscaDevice.Name}] VISCA Reply: Unknown type 0x{replyType:X2}");
                break;
        }

    }

    public static void ParsePowerInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 2) throw new ArgumentException("Expected min 2 bytes");
        var value = (Power)buffer[startIndex++];
        if(buffer[startIndex++] != (byte)Terminator.Terminate) {
            viscaDevice.PowerState = null;
            throw new Exception("Invalid response. PowerInquiry not terminated.");
        }
        viscaDevice.PowerState = Enum.IsDefined(value) ? value : null;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Power = {viscaDevice.PowerState}");
    }

    public static void ParseZoomInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        // Response: y0 50 0p 0q 0r 0s FF  -> zoom position = pqrs
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] ZoomInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.ZoomPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Zoom = 0x{val:X4} ({val})");
    }

    public static void ParseFocusInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] FocusInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.FocusPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Focus = 0x{val:X4} ({val})");
    }

    public static void ParseFocusModeInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 2) { Debug.WriteLine($"[{viscaDevice.Name}] FocusModeInquiry: too short"); return; }
        var value = (FocusMode)buffer[startIndex];
        viscaDevice.FocusModeState = Enum.IsDefined(value) ? value : null;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: FocusMode = {viscaDevice.FocusModeState}");
    }

    public static void ParseExposureModeInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 2) { Debug.WriteLine($"[{viscaDevice.Name}] ExposureModeInquiry: too short"); return; }
        var value = (ExposureMode)buffer[startIndex];
        viscaDevice.ExposureModeState = Enum.IsDefined(value) ? value : null;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: ExposureMode = {viscaDevice.ExposureModeState}");
    }

    public static void ParseIrisInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] IrisInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.IrisPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Iris = 0x{val:X4} ({val})");
    }

    public static void ParseShutterInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] ShutterInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.ShutterPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Shutter = 0x{val:X4} ({val})");
    }

    public static void ParseGainInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] GainInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.GainPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Gain = 0x{val:X4} ({val})");
    }

    public static void ParseWhiteBalanceModeInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 2) { Debug.WriteLine($"[{viscaDevice.Name}] WBModeInquiry: too short"); return; }
        var value = (WhiteBalanceMode)buffer[startIndex];
        viscaDevice.WhiteBalanceModeState = Enum.IsDefined(value) ? value : null;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: WBMode = {viscaDevice.WhiteBalanceModeState}");
    }

    public static void ParseRGainInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] RGainInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.RGainPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: RGain = 0x{val:X4} ({val})");
    }

    public static void ParseBGainInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] BGainInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.BGainPosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: BGain = 0x{val:X4} ({val})");
    }

    public static void ParseApertureInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 5) { Debug.WriteLine($"[{viscaDevice.Name}] ApertureInquiry: too short"); return; }
        ushort val = (ushort)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        viscaDevice.AperturePosition = val;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Aperture = 0x{val:X4} ({val})");
    }

    public static void ParseBacklightInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        if (buffer.Length - startIndex < 2) { Debug.WriteLine($"[{viscaDevice.Name}] BacklightInquiry: too short"); return; }
        var value = (BacklightCompensation)buffer[startIndex];
        viscaDevice.BacklightState = Enum.IsDefined(value) ? value : null;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Backlight = {viscaDevice.BacklightState}");
    }

    public static void ParsePanTiltPositionInquiryReply(ViscaDeviceBase viscaDevice, byte[] buffer, int startIndex)
    {
        // Response: y0 50 0w 0w 0w 0w 0z 0z 0z 0z FF -> pan=wwww tilt=zzzz
        if (buffer.Length - startIndex < 9) { Debug.WriteLine($"[{viscaDevice.Name}] PanTiltInquiry: too short"); return; }
        short panVal = (short)(
            ((buffer[startIndex] & 0x0F) << 12) |
            ((buffer[startIndex + 1] & 0x0F) << 8) |
            ((buffer[startIndex + 2] & 0x0F) << 4) |
            (buffer[startIndex + 3] & 0x0F));
        short tiltVal = (short)(
            ((buffer[startIndex + 4] & 0x0F) << 12) |
            ((buffer[startIndex + 5] & 0x0F) << 8) |
            ((buffer[startIndex + 6] & 0x0F) << 4) |
            (buffer[startIndex + 7] & 0x0F));
        viscaDevice.PanPosition = panVal;
        viscaDevice.TiltPosition = tiltVal;
        Debug.WriteLine($"[{viscaDevice.Name}] Inquiry Reply: Pan = 0x{panVal:X4} ({panVal}), Tilt = 0x{tiltVal:X4} ({tiltVal})");
    }
}
