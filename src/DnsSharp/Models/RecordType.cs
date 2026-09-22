namespace DnsSharp.Models;

/// <summary>
/// DNS record type values.
/// </summary>
public enum RecordType : ushort
{
    A = 1,
    NS = 2,
    CNAME = 5,
    SOA = 6,
    PTR = 12,
    MX = 15,
    TXT = 16,
    AAAA = 28,
    SRV = 33,
    DS = 43,
    RRSIG = 46,
    NSEC = 47,
    DNSKEY = 48,
    NSEC3 = 50,
    NSEC3PARAM = 51,
    CAA = 257,
    OPT = 41,
    ANY = 255
}
