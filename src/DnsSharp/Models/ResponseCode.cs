namespace DnsSharp.Models;

/// <summary>
/// DNS response codes.
/// </summary>
public enum ResponseCode : byte
{
    NoError = 0,
    FormatError = 1,
    ServerFailure = 2,
    NxDomain = 3,
    NotImplemented = 4,
    Refused = 5,
    YxDomain = 6,
    YxRrSet = 7,
    NxRrSet = 8,
    NotAuth = 9,
    NotZone = 10,
    BadVersOrSig = 16
}
