using System.Buffers.Binary;

namespace NxtHotspot.RadiusProtocol;

/// <summary>RADIUS packet (RFC 2865 §3). Parsing is strict: every length is checked before use.</summary>
public sealed class RadiusPacket
{
    public const int HeaderLength = 20;
    public const int MinLength = 20;
    public const int MaxLength = 4096;

    public RadiusCode Code { get; set; }
    public byte Identifier { get; set; }
    /// <summary>16-byte Request or Response Authenticator.</summary>
    public byte[] Authenticator { get; set; } = new byte[16];
    public List<RadiusAttribute> Attributes { get; } = new();

    public RadiusPacket() { }

    public RadiusPacket(RadiusCode code, byte identifier, byte[]? authenticator = null)
    {
        Code = code;
        Identifier = identifier;
        Authenticator = authenticator ?? new byte[16];
    }

    public static RadiusPacket Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinLength) throw new RadiusPacketException("Packet shorter than the RADIUS header.");
        var length = BinaryPrimitives.ReadUInt16BigEndian(data[2..4]);
        if (length < MinLength || length > MaxLength) throw new RadiusPacketException($"Invalid packet length {length}.");
        if (length > data.Length) throw new RadiusPacketException("Packet length field exceeds the datagram size.");
        // Octets beyond Length are padding and are ignored (RFC 2865 §3).
        var packet = new RadiusPacket((RadiusCode)data[0], data[1], data[4..20].ToArray());
        var pos = HeaderLength;
        while (pos < length)
        {
            if (pos + 2 > length) throw new RadiusPacketException("Truncated attribute header.");
            var type = data[pos];
            var attrLen = data[pos + 1];
            if (attrLen < 2) throw new RadiusPacketException($"Attribute {type} has invalid length {attrLen}.");
            if (pos + attrLen > length) throw new RadiusPacketException($"Attribute {type} overruns the packet.");
            packet.Attributes.Add(new RadiusAttribute(type, data.Slice(pos + 2, attrLen - 2).ToArray()));
            pos += attrLen;
        }
        return packet;
    }

    /// <summary>Wire bytes with the Length field filled in. The authenticator is written as currently set.</summary>
    public byte[] Encode()
    {
        var length = HeaderLength + Attributes.Sum(a => a.Value.Length + 2);
        if (length > MaxLength) throw new RadiusPacketException("Packet exceeds 4096 bytes.");
        var buffer = new byte[length];
        buffer[0] = (byte)Code;
        buffer[1] = Identifier;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)length);
        Authenticator.AsSpan(0, 16).CopyTo(buffer.AsSpan(4));
        var pos = HeaderLength;
        foreach (var a in Attributes)
        {
            buffer[pos] = a.Type;
            buffer[pos + 1] = (byte)(a.Value.Length + 2);
            a.Value.CopyTo(buffer, pos + 2);
            pos += a.Value.Length + 2;
        }
        return buffer;
    }

    public RadiusAttribute? Get(byte type) => Attributes.FirstOrDefault(a => a.Type == type);
    public string? GetString(byte type) => Get(type)?.AsString();
    public uint? GetInteger(byte type) => Get(type) is { Value.Length: 4 } a ? a.AsInteger() : null;
    public bool Has(byte type) => Attributes.Any(a => a.Type == type);

    public RadiusPacket Add(RadiusAttribute attribute)
    {
        Attributes.Add(attribute);
        return this;
    }
}
