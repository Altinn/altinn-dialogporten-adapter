using UUIDNext;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Common;

internal static class GuidExtensions
{
    /// <summary>
    /// Creates a deterministic UUID v7 by first creating a UUID v5 based
    /// on the parent UUID (namespace) and name, then converting that to
    /// a UUID v7 by copying the UUID v7 parts from the parent UUID.
    /// </summary>
    /// <remarks>
    /// Assumes that parentV7Id is on UUID v7 format.
    /// </remarks>
    /// <param name="parentV7Id"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static Guid CreateDeterministicSubUuidV7(this Guid parentV7Id, string name)
        => Uuid.NewNameBased(parentV7Id, name).CopyUuidV7PartsFrom(parentV7Id);

    public static Guid ToVersion7(this Guid guid, DateTimeOffset timestamp)
    {
        // Create a buffer for the UUID (16 bytes)
        Span<byte> uuidBytes = stackalloc byte[16];
        // Copy data from the input GUID into the buffer
        guid.TryWriteBytes(uuidBytes, bigEndian: true, out _);
        // Get the timestamp in milliseconds since Unix epoch
        var unixTimestampMillis = timestamp.ToUnixTimeMilliseconds();

        // Write the timestamp (48 bits) into the UUID buffer 
        uuidBytes[0] = (byte)((unixTimestampMillis >> 40) & 0xFF);
        uuidBytes[1] = (byte)((unixTimestampMillis >> 32) & 0xFF);
        uuidBytes[2] = (byte)((unixTimestampMillis >> 24) & 0xFF);
        uuidBytes[3] = (byte)((unixTimestampMillis >> 16) & 0xFF);
        uuidBytes[4] = (byte)((unixTimestampMillis >> 8) & 0xFF);
        uuidBytes[5] = (byte)(unixTimestampMillis & 0xFF);

        // Set the version to 7 (4 high bits of the 7th byte)
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | 0x70);

        // Set the variant to RFC 4122 (2 most significant bits of the 9th byte to 10)
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80);

        // Construct and return the UUID
        return new Guid(uuidBytes, bigEndian: true);
    }

    private static Guid CopyUuidV7PartsFrom(this Guid target, Guid source)
    {
        // Create buffers for the source and target GUIDs (16 bytes each)
        Span<byte> sourceBytes = stackalloc byte[16];
        Span<byte> targetBytes = stackalloc byte[16];

        // Copy data from the source and target GUIDs into the buffers
        source.TryWriteBytes(sourceBytes, bigEndian: true, out _);
        target.TryWriteBytes(targetBytes, bigEndian: true, out _);

        // Copy the first 48 bits (6 bytes) from the source to the target (timestamp)
        sourceBytes[..6].CopyTo(targetBytes[..6]);
        
        // Copy only the four most significant bits of the 7th byte (version) from the source to the target
        targetBytes[6] = (byte)((targetBytes[6] & 0x0F) | (sourceBytes[6] & 0xF0));

        // Copy only the two most significant bits of the 9th byte (variant) from the source to the target
        targetBytes[8] = (byte)((targetBytes[8] & 0x3F) | (sourceBytes[8] & 0xC0));

        // Construct and return the new target GUID
        return new Guid(targetBytes, bigEndian: true);
    }
}
