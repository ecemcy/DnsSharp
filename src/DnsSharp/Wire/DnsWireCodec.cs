using System.Buffers.Binary;
using System.Net;
using System.Text;
using DnsSharp.Models;
using DnsSharp.Models.Records;

namespace DnsSharp.Wire;

/// <summary>
/// Defensive RFC-aware DNS wire codec with compressed-name handling.
/// </summary>
public sealed class DnsWireCodec : IDnsWireCodec
{
    /// <inheritdoc />
    public byte[] Encode(DnsMessage message)
    {
        var buffer = new List<byte>(512);
        var flags = BuildFlags(message);

        WriteUInt16(buffer, message.Id);
        WriteUInt16(buffer, flags);
        WriteUInt16(buffer, checked((ushort)message.Questions.Count));
        WriteUInt16(buffer, checked((ushort)message.Answers.Count));
        WriteUInt16(buffer, checked((ushort)message.Authorities.Count));
        WriteUInt16(buffer, checked((ushort)message.Additionals.Count));

        foreach (var question in message.Questions)
        {
            WriteDomainName(buffer, question.Name);
            WriteUInt16(buffer, (ushort)question.Type);
            WriteUInt16(buffer, (ushort)question.Class);
        }

        foreach (var record in message.Answers.Concat(message.Authorities).Concat(message.Additionals))
        {
            WriteRecord(buffer, record);
        }

        return buffer.ToArray();
    }

    /// <inheritdoc />
    public DnsMessage Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            throw new InvalidDataException("DNS message is shorter than header size.");
        }

        var index = 0;
        var id = ReadUInt16(bytes, ref index);
        var flags = ReadUInt16(bytes, ref index);
        var qdCount = ReadUInt16(bytes, ref index);
        var anCount = ReadUInt16(bytes, ref index);
        var nsCount = ReadUInt16(bytes, ref index);
        var arCount = ReadUInt16(bytes, ref index);

        var message = new DnsMessage
        {
            Id = id,
            IsResponse = (flags & 0x8000) != 0,
            OpCode = (byte)((flags >> 11) & 0x0F),
            AuthoritativeAnswer = (flags & 0x0400) != 0,
            Truncated = (flags & 0x0200) != 0,
            RecursionDesired = (flags & 0x0100) != 0,
            RecursionAvailable = (flags & 0x0080) != 0,
            ResponseCode = (ResponseCode)(flags & 0x000F)
        };

        for (var i = 0; i < qdCount && index < bytes.Length; i++)
        {
            var qName = ReadDomainName(bytes, ref index);
            if (!TryReadQuestion(bytes, ref index, out var question))
            {
                break;
            }

            message.Questions.Add(new DnsQuestion { Name = qName, Type = question.Type, Class = question.Class });
        }

        ReadRecordSection(bytes, ref index, anCount, message.Answers);
        ReadRecordSection(bytes, ref index, nsCount, message.Authorities);
        ReadRecordSection(bytes, ref index, arCount, message.Additionals);
        ApplyExtendedResponseCode(message);

        return message;
    }

    private static void ApplyExtendedResponseCode(DnsMessage message)
    {
        var opt = message.Additionals
            .Select(x => x.Data as OptRecordData)
            .FirstOrDefault(x => x is not null);

        if (opt is null || opt.ExtendedResponseCode == 0)
        {
            return;
        }

        var baseCode = (byte)message.ResponseCode & 0x0F;
        var extendedCode = (opt.ExtendedResponseCode << 4) | baseCode;
        message.ResponseCode = (ResponseCode)extendedCode;
    }

    private static void ReadRecordSection(ReadOnlySpan<byte> bytes, ref int index, int count, ICollection<DnsRecord> target)
    {
        for (var i = 0; i < count && index < bytes.Length; i++)
        {
            var rr = ReadRecord(bytes, ref index);
            if (rr is null)
            {
                break;
            }

            target.Add(rr);
        }
    }

    private static DnsRecord? ReadRecord(ReadOnlySpan<byte> bytes, ref int index)
    {
        try
        {
            var name = ReadDomainName(bytes, ref index);
            if (index + 10 > bytes.Length)
            {
                return null;
            }

            var type = (RecordType)ReadUInt16(bytes, ref index);
            var klass = (DnsClass)ReadUInt16(bytes, ref index);
            var ttl = ReadUInt32(bytes, ref index);
            var rdLength = ReadUInt16(bytes, ref index);

            if (index + rdLength > bytes.Length)
            {
                rdLength = (ushort)Math.Max(0, bytes.Length - index);
            }

            var rdataOffset = index;
            var rdata = bytes.Slice(index, rdLength).ToArray();
            index += rdLength;

            var data = ParseRecordData(type, klass, rdata, bytes, rdataOffset);
            return new DnsRecord
            {
                Name = name,
                Type = type,
                Class = klass,
                Ttl = ttl,
                RawData = rdata,
                Data = data
            };
        }
        catch
        {
            return null;
        }
    }

    private static object ParseRecordData(RecordType type, DnsClass klass, byte[] rdata, ReadOnlySpan<byte> message, int rdataOffset)
    {
        try
        {
            return type switch
            {
                RecordType.A when rdata.Length == 4 => new ARecordData(new IPAddress(rdata)),
                RecordType.AAAA when rdata.Length == 16 => new AaaaRecordData(new IPAddress(rdata)),
                RecordType.NS => new NsRecordData(ReadDomainNameAt(message, rdataOffset)),
                RecordType.CNAME => new CNameRecordData(ReadDomainNameAt(message, rdataOffset)),
                RecordType.PTR => new PtrRecordData(ReadDomainNameAt(message, rdataOffset)),
                RecordType.MX => ParseMx(rdata, message, rdataOffset),
                RecordType.TXT => ParseTxt(rdata),
                RecordType.SRV => ParseSrv(rdata, message, rdataOffset),
                RecordType.SOA => ParseSoa(message, rdataOffset, rdata.Length),
                RecordType.CAA => ParseCaa(rdata),
                RecordType.DS => ParseDs(rdata),
                RecordType.RRSIG => ParseRrsig(message, rdataOffset, rdata.Length),
                RecordType.DNSKEY => ParseDnsKey(rdata),
                RecordType.OPT => ParseOpt(rdata, klass, message, rdataOffset),
                _ => new UnknownRecordData(rdata)
            };
        }
        catch
        {
            return new UnknownRecordData(rdata);
        }
    }

    private static object ParseOpt(byte[] rdata, DnsClass klass, ReadOnlySpan<byte> message, int offset)
    {
        if (offset < 6 || offset > message.Length)
        {
            return new UnknownRecordData(rdata);
        }

        // OPT reuses CLASS for UDP payload size and TTL for ext-rcode/version/flags.
        var ttlOffset = offset - 6;
        var ttl = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(ttlOffset + 2, 4));
        return new OptRecordData(
            (ushort)klass,
            (byte)(ttl >> 24),
            (byte)(ttl >> 16),
            (ushort)(ttl & 0xFFFF),
            rdata);
    }

    private static object ParseDnsKey(byte[] rdata)
    {
        if (rdata.Length < 4)
        {
            return new UnknownRecordData(rdata);
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
        var protocol = rdata[2];
        var algorithm = rdata[3];
        var key = rdata.AsSpan(4).ToArray();
        return new DnsKeyRecordData(flags, protocol, algorithm, key);
    }

    private static object ParseRrsig(ReadOnlySpan<byte> message, int offset, int length)
    {
        if (offset < 0 || offset + length > message.Length || length < 18)
        {
            return new UnknownRecordData(message.Slice(offset, Math.Max(0, Math.Min(length, message.Length - offset))).ToArray());
        }

        var span = message.Slice(offset, length);
        var localIndex = 0;
        var typeCovered = (RecordType)ReadUInt16(span, ref localIndex);
        var algorithm = ReadByte(span, ref localIndex);
        var labels = ReadByte(span, ref localIndex);
        var originalTtl = ReadUInt32(span, ref localIndex);
        var expiration = ReadUInt32(span, ref localIndex);
        var inception = ReadUInt32(span, ref localIndex);
        var keyTag = ReadUInt16(span, ref localIndex);

        var absoluteIndex = offset + localIndex;
        var signerName = ReadDomainName(message, ref absoluteIndex);
        var signatureStart = absoluteIndex;
        var signatureLength = Math.Max(0, (offset + length) - signatureStart);
        var signature = message.Slice(signatureStart, signatureLength).ToArray();

        return new RrSigRecordData(typeCovered, algorithm, labels, originalTtl, expiration, inception, keyTag, signerName, signature);
    }

    private static object ParseDs(byte[] rdata)
    {
        if (rdata.Length < 4)
        {
            return new UnknownRecordData(rdata);
        }

        var keyTag = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
        return new DsRecordData(keyTag, rdata[2], rdata[3], rdata.AsSpan(4).ToArray());
    }

    private static object ParseCaa(byte[] rdata)
    {
        if (rdata.Length < 2)
        {
            return new UnknownRecordData(rdata);
        }

        var flags = rdata[0];
        var tagLength = rdata[1];
        if (rdata.Length < tagLength + 2)
        {
            return new UnknownRecordData(rdata);
        }

        var tag = Encoding.ASCII.GetString(rdata, 2, tagLength);
        var value = Encoding.UTF8.GetString(rdata, 2 + tagLength, rdata.Length - 2 - tagLength);
        return new CaaRecordData(flags, tag, value);
    }

    private static object ParseSoa(ReadOnlySpan<byte> message, int offset, int length)
    {
        var end = offset + length;
        var index = offset;
        var mname = ReadDomainName(message, ref index);
        var rname = ReadDomainName(message, ref index);

        if (index + 20 > end || index + 20 > message.Length)
        {
            return new UnknownRecordData(message.Slice(offset, Math.Max(0, Math.Min(length, message.Length - offset))).ToArray());
        }

        var serial = ReadUInt32(message, ref index);
        var refresh = ReadUInt32(message, ref index);
        var retry = ReadUInt32(message, ref index);
        var expire = ReadUInt32(message, ref index);
        var minimum = ReadUInt32(message, ref index);
        return new SoaRecordData(mname, rname, serial, refresh, retry, expire, minimum);
    }

    private static object ParseSrv(byte[] rdata, ReadOnlySpan<byte> message, int rdataOffset)
    {
        if (rdata.Length < 6)
        {
            return new UnknownRecordData(rdata);
        }

        var priority = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
        var weight = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(2, 2));
        var port = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(4, 2));

        var targetIndex = rdataOffset + 6;
        if (targetIndex > message.Length)
        {
            return new UnknownRecordData(rdata);
        }

        var target = ReadDomainName(message, ref targetIndex);
        return new SrvRecordData(priority, weight, port, target);
    }

    private static object ParseTxt(byte[] rdata)
    {
        var values = new List<string>();
        var idx = 0;

        while (idx < rdata.Length)
        {
            var len = rdata[idx++];
            if (idx + len > rdata.Length)
            {
                len = (byte)Math.Max(0, rdata.Length - idx);
            }

            values.Add(Encoding.UTF8.GetString(rdata, idx, len));
            idx += len;
        }

        return new TxtRecordData(values);
    }

    private static object ParseMx(byte[] rdata, ReadOnlySpan<byte> message, int rdataOffset)
    {
        if (rdata.Length < 2)
        {
            return new UnknownRecordData(rdata);
        }

        var preference = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
        var exchangeIndex = rdataOffset + 2;
        if (exchangeIndex > message.Length)
        {
            return new UnknownRecordData(rdata);
        }

        var exchange = ReadDomainName(message, ref exchangeIndex);
        return new MxRecordData(preference, exchange);
    }

    private static (RecordType Type, DnsClass Class) ParseQuestion(ReadOnlySpan<byte> bytes, ref int index)
    {
        var type = (RecordType)ReadUInt16(bytes, ref index);
        var klass = (DnsClass)ReadUInt16(bytes, ref index);
        return (type, klass);
    }

    private static bool TryReadQuestion(ReadOnlySpan<byte> bytes, ref int index, out (RecordType Type, DnsClass Class) question)
    {
        question = default;
        if (index + 4 > bytes.Length)
        {
            return false;
        }

        question = ParseQuestion(bytes, ref index);
        return true;
    }

    private static string ReadDomainNameAt(ReadOnlySpan<byte> bytes, int index)
    {
        return ReadDomainName(bytes, ref index);
    }

    private static string ReadDomainName(ReadOnlySpan<byte> bytes, ref int index)
    {
        // DNS labels may be compressed using 0b11xxxxxx pointers (RFC 1035 section 4.1.4).
        var labels = new List<string>();
        var jumps = 0;
        var jumped = false;
        var currentIndex = index;

        while (currentIndex < bytes.Length)
        {
            var length = bytes[currentIndex];

            if (length == 0)
            {
                currentIndex++;
                if (!jumped)
                {
                    index = currentIndex;
                }

                return labels.Count == 0 ? "." : string.Join('.', labels);
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (currentIndex + 1 >= bytes.Length)
                {
                    throw new InvalidDataException("Compressed DNS name pointer is truncated.");
                }

                var pointer = ((length & 0x3F) << 8) | bytes[currentIndex + 1];
                if (pointer >= bytes.Length)
                {
                    throw new InvalidDataException("Compressed DNS name pointer is out of range.");
                }

                if (!jumped)
                {
                    index = currentIndex + 2;
                }

                currentIndex = pointer;
                jumped = true;
                jumps++;
                if (jumps > 20)
                {
                    throw new InvalidDataException("Too many DNS compression pointers; possible loop.");
                }

                continue;
            }

            if ((length & 0xC0) != 0)
            {
                throw new InvalidDataException("Invalid DNS label length marker.");
            }

            currentIndex++;
            if (currentIndex + length > bytes.Length)
            {
                throw new InvalidDataException("DNS label exceeds packet length.");
            }

            var label = Encoding.ASCII.GetString(bytes.Slice(currentIndex, length));
            labels.Add(label);
            currentIndex += length;

            if (!jumped)
            {
                index = currentIndex;
            }
        }

        throw new InvalidDataException("Unterminated DNS name.");
    }

    private static void WriteRecord(List<byte> buffer, DnsRecord record)
    {
        WriteDomainName(buffer, record.Name);
        WriteUInt16(buffer, (ushort)record.Type);
        WriteUInt16(buffer, (ushort)record.Class);
        WriteUInt32(buffer, record.Ttl);

        var data = record.RawData ?? Array.Empty<byte>();
        WriteUInt16(buffer, checked((ushort)data.Length));
        buffer.AddRange(data);
    }

    private static void WriteDomainName(List<byte> buffer, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == ".")
        {
            buffer.Add(0);
            return;
        }

        foreach (var label in name.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length is 0 or > 63)
            {
                throw new InvalidOperationException($"Invalid DNS label length: {label}");
            }

            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        buffer.Add(0);
    }

    private static ushort BuildFlags(DnsMessage message)
    {
        ushort flags = 0;
        if (message.IsResponse)
        {
            flags |= 0x8000;
        }

        flags |= (ushort)((message.OpCode & 0x0F) << 11);

        if (message.AuthoritativeAnswer)
        {
            flags |= 0x0400;
        }

        if (message.Truncated)
        {
            flags |= 0x0200;
        }

        if (message.RecursionDesired)
        {
            flags |= 0x0100;
        }

        if (message.RecursionAvailable)
        {
            flags |= 0x0080;
        }

        flags |= (ushort)((byte)message.ResponseCode & 0x0F);
        return flags;
    }

    private static void WriteUInt16(List<byte> buffer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static void WriteUInt32(List<byte> buffer, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int index)
    {
        EnsureLength(bytes, index, 2);
        var value = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index, 2));
        index += 2;
        return value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int index)
    {
        EnsureLength(bytes, index, 4);
        var value = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(index, 4));
        index += 4;
        return value;
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int index)
    {
        EnsureLength(bytes, index, 1);
        return bytes[index++];
    }

    private static void EnsureLength(ReadOnlySpan<byte> bytes, int index, int count)
    {
        if (index < 0 || index + count > bytes.Length)
        {
            throw new InvalidDataException("DNS message ended unexpectedly.");
        }
    }
}
