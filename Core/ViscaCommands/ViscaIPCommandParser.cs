using PtzJoystickControl.Core.Devices;
using System.Diagnostics;

namespace PtzJoystickControl.Core.ViscaCommands;

public static class ViscaIPCommandParser
{
    public static void ParseReply(ViscaIPDeviceBase viscaIPDevice, byte[] buffer, int length, int startIndex = 0)
    {
        int endIndex = startIndex + length;
        if (0 > startIndex || buffer.Length <= endIndex)
            throw new ArgumentOutOfRangeException("index + length");

        if (length < 9) throw new ArgumentException("Packet min length is 9 bytes.");

        int headerType = buffer[startIndex++] << 8 | buffer[startIndex++];
        int commandLength = buffer[startIndex++] << 8 | buffer[startIndex++];
        int packetId = buffer[startIndex++] << 24 
            | buffer[startIndex++] << 16 
            | buffer[startIndex++] << 8 
            | buffer[startIndex++];

        if(commandLength != length - 8)
            throw new ArgumentException($"Packet length mismatch. Header: {commandLength} - Actual: {length - 8}");

        startIndex++; //Skip address byte as it's locked in ViscaIP
        switch (headerType)
        {
            case (ushort)ViscaIPHeaderType.CommandReply:
                Debug.WriteLine($"[{viscaIPDevice.Name}] IP Parse: CommandReply packetId={packetId}");
                ViscaCommandParser.ParseReply(viscaIPDevice, buffer, startIndex);
                break;
            case (ushort)ViscaIPHeaderType.ControlReply:
                Debug.WriteLine($"[{viscaIPDevice.Name}] IP Parse: ControlReply packetId={packetId}");
                break;
            case (ushort)ViscaIPHeaderType.Command:
            case (ushort)ViscaIPHeaderType.Inquery:
            case (ushort)ViscaIPHeaderType.SettingCommand:
            case (ushort)ViscaIPHeaderType.ControlCommand:
                throw new Exception("Not a reply type header");
            default:
                Debug.WriteLine($"[{viscaIPDevice.Name}] IP Parse: Unknown header type 0x{headerType:X4}");
                throw new Exception("Invalid header type");
        }

        viscaIPDevice.remotePacketId = packetId;
    }
}
