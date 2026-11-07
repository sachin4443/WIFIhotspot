using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace NxtHotspot.RadiusProtocol;

/// <summary>One attribute (type + raw value). Value length is 0..253 bytes.</summary>
public sealed class RadiusAttribute
{
    public const int MaxValueLength = 253;

    public byte Type { get; }
    public byte[] Value { get; }

    public RadiusAttribute(byte type, byte[] value)
    {
        if (value.Length > MaxValueLength) throw new RadiusPacketException($"Attribute {type} value exceeds {MaxValueLength} bytes.");
        Type = type;
        Value = value;
    }

    public static RadiusAttribute String(byte type, string value) => new(type, Encoding.UTF8.GetBytes(value));

    public static RadiusAttribute Integer(byte type, uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, value);
        return new RadiusAttribute(type, b);
    }

    public static RadiusAttribute Address(byte type, IPAddress ip)
    {
        var bytes = ip.MapToIPv4().GetAddressBytes();
        return new RadiusAttribute(type, bytes);
    }

    /// <summary>Vendor-Specific (26) wrapping one vendor attribute (RFC 2865 §5.26).</summary>
    public static RadiusAttribute Vendor(uint vendorId, byte vendorType, byte[] value)
    {
        if (value.Length > MaxValueLength - 6) throw new RadiusPacketException("Vendor attribute value too long.");
        var b = new byte[6 + value.Length];
        BinaryPrimitives.WriteUInt32BigEndian(b, vendorId);
        b[4] = vendorType;
        b[5] = (byte)(value.Length + 2);
        value.CopyTo(b, 6);
        return new RadiusAttribute(RadiusAttributeType.VendorSpecific, b);
    }

    public static RadiusAttribute VendorString(uint vendorId, byte vendorType, string value) => Vendor(vendorId, vendorType, Encoding.UTF8.GetBytes(value));

    public static RadiusAttribute VendorInteger(uint vendorId, byte vendorType, uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, value);
        return Vendor(vendorId, vendorType, b);
    }

    public string AsString() => Encoding.UTF8.GetString(Value);

    public uint AsInteger() =>
        Value.Length == 4 ? BinaryPrimitives.ReadUInt32BigEndian(Value) : throw new RadiusPacketException($"Attribute {Type} is not a 4-byte integer.");

    public IPAddress AsAddress() =>
        Value.Length == 4 ? new IPAddress(Value) : throw new RadiusPacketException($"Attribute {Type} is not an IPv4 address.");

    /// <summary>Returns (vendorId, vendorType, value) for a Vendor-Specific attribute holding one sub-attribute.</summary>
    public (uint VendorId, byte VendorType, byte[] Value)? AsVendor()
    {
        if (Type != RadiusAttributeType.VendorSpecific || Value.Length < 6) return null;
        var len = Value[5];
        if (len < 2 || 4 + len > Value.Length) return null;
        return (BinaryPrimitives.ReadUInt32BigEndian(Value), Value[4], Value.AsSpan(6, len - 2).ToArray());
    }
}
