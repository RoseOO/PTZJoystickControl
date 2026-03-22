using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PtzJoystickControl.OscInput.Services;

/// <summary>
/// Lightweight OSC message parser and UDP listener.
/// Handles basic OSC messages with float, int, and string arguments.
/// </summary>
internal static class OscParser
{
    public record OscMessage(string Address, object[] Arguments);

    /// <summary>
    /// Parse an OSC message from raw UDP bytes.
    /// </summary>
    public static OscMessage? Parse(byte[] data, int length)
    {
        try
        {
            int offset = 0;

            // Read address (null-terminated, padded to 4 bytes)
            string address = ReadString(data, ref offset, length);
            if (!address.StartsWith("/")) return null;

            // Read type tag string
            if (offset >= length) return new OscMessage(address, Array.Empty<object>());
            string typeTag = ReadString(data, ref offset, length);
            if (!typeTag.StartsWith(",")) return new OscMessage(address, Array.Empty<object>());

            // Parse arguments based on type tags
            var args = new List<object>();
            for (int i = 1; i < typeTag.Length && offset < length; i++)
            {
                switch (typeTag[i])
                {
                    case 'f': // float32
                        if (offset + 4 <= length)
                        {
                            int intBits = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
                            args.Add(BitConverter.Int32BitsToSingle(intBits));
                            offset += 4;
                        }
                        break;

                    case 'i': // int32
                        if (offset + 4 <= length)
                        {
                            args.Add(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4)));
                            offset += 4;
                        }
                        break;

                    case 's': // string
                        args.Add(ReadString(data, ref offset, length));
                        break;

                    case 'T': // True
                        args.Add(true);
                        break;

                    case 'F': // False
                        args.Add(false);
                        break;
                }
            }

            return new OscMessage(address, args.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(byte[] data, ref int offset, int maxLength)
    {
        int start = offset;
        while (offset < maxLength && data[offset] != 0)
            offset++;

        string result = Encoding.ASCII.GetString(data, start, offset - start);

        // Skip null terminator and pad to 4-byte boundary
        offset++;
        while (offset % 4 != 0 && offset < maxLength)
            offset++;

        return result;
    }
}
