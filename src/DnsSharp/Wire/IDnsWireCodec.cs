using DnsSharp.Models;

namespace DnsSharp.Wire;

/// <summary>
/// Encodes and decodes DNS wire-format messages.
/// </summary>
public interface IDnsWireCodec
{
    /// <summary>
    /// Encodes a DNS message into wire format.
    /// </summary>
    byte[] Encode(DnsMessage message);

    /// <summary>
    /// Decodes DNS wire bytes into a message.
    /// </summary>
    DnsMessage Decode(ReadOnlySpan<byte> bytes);
}
