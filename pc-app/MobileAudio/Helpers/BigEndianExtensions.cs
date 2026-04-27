using System.Buffers.Binary;

namespace MobileAudio.Helpers;

/// <summary>
/// Extension methods for big-endian binary operations.
/// </summary>
public static class BigEndianExtensions
{
    /// <summary>
    /// Writes <paramref name="value"/> as a big-endian 32-bit unsigned integer
    /// into <paramref name="destination"/> starting at <paramref name="offset"/>.
    /// </summary>
    public static void WriteUInt32BigEndian(this byte[] destination, uint value, int offset = 0)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination.AsSpan(offset, 4), value);
    }
}

