using System.Net;

namespace DnsSharp.Models.Records;

/// <summary>
/// Parsed payload for A record.
/// </summary>
public sealed record ARecordData(IPAddress Address);

/// <summary>
/// Parsed payload for AAAA record.
/// </summary>
public sealed record AaaaRecordData(IPAddress Address);

/// <summary>
/// Parsed payload for NS record.
/// </summary>
public sealed record NsRecordData(string NameServer);

/// <summary>
/// Parsed payload for CNAME record.
/// </summary>
public sealed record CNameRecordData(string CanonicalName);

/// <summary>
/// Parsed payload for PTR record.
/// </summary>
public sealed record PtrRecordData(string Pointer);

/// <summary>
/// Parsed payload for MX record.
/// </summary>
public sealed record MxRecordData(ushort Preference, string Exchange);

/// <summary>
/// Parsed payload for TXT record.
/// </summary>
public sealed record TxtRecordData(IReadOnlyList<string> Values);

/// <summary>
/// Parsed payload for SRV record.
/// </summary>
public sealed record SrvRecordData(ushort Priority, ushort Weight, ushort Port, string Target);

/// <summary>
/// Parsed payload for SOA record.
/// </summary>
public sealed record SoaRecordData(
    string PrimaryNameServer,
    string ResponsibleMailbox,
    uint Serial,
    uint Refresh,
    uint Retry,
    uint Expire,
    uint Minimum);

/// <summary>
/// Parsed payload for CAA record.
/// </summary>
public sealed record CaaRecordData(byte Flags, string Tag, string Value);

/// <summary>
/// Parsed payload for DS record.
/// </summary>
public sealed record DsRecordData(ushort KeyTag, byte Algorithm, byte DigestType, byte[] Digest);

/// <summary>
/// Parsed payload for RRSIG record.
/// </summary>
public sealed record RrSigRecordData(
    RecordType TypeCovered,
    byte Algorithm,
    byte Labels,
    uint OriginalTtl,
    uint SignatureExpiration,
    uint SignatureInception,
    ushort KeyTag,
    string SignerName,
    byte[] Signature);

/// <summary>
/// Parsed payload for DNSKEY record.
/// </summary>
public sealed record DnsKeyRecordData(ushort Flags, byte Protocol, byte Algorithm, byte[] PublicKey);

/// <summary>
/// Parsed payload for OPT pseudo record.
/// </summary>
public sealed record OptRecordData(ushort UdpPayloadSize, byte ExtendedResponseCode, byte Version, ushort Flags, byte[] OptionsData);

/// <summary>
/// Parsed payload for unknown record types.
/// </summary>
public sealed record UnknownRecordData(byte[] Data);
