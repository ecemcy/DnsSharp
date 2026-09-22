namespace DnsSharp.Models;

/// <summary>
/// Represents a DNS message.
/// </summary>
public sealed class DnsMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public ushort Id { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this message is a response.
    /// </summary>
    public bool IsResponse { get; init; }

    /// <summary>
    /// Gets or sets the operation code.
    /// </summary>
    public byte OpCode { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this response is authoritative.
    /// </summary>
    public bool AuthoritativeAnswer { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this response is truncated.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether recursion was desired.
    /// </summary>
    public bool RecursionDesired { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether recursion is available.
    /// </summary>
    public bool RecursionAvailable { get; init; }

    /// <summary>
    /// Gets or sets the response code.
    /// </summary>
    public ResponseCode ResponseCode { get; set; }

    /// <summary>
    /// Gets questions in the message.
    /// </summary>
    public List<DnsQuestion> Questions { get; } = new();

    /// <summary>
    /// Gets answers in the message.
    /// </summary>
    public List<DnsRecord> Answers { get; } = new();

    /// <summary>
    /// Gets authority records in the message.
    /// </summary>
    public List<DnsRecord> Authorities { get; } = new();

    /// <summary>
    /// Gets additional records in the message.
    /// </summary>
    public List<DnsRecord> Additionals { get; } = new();
}
