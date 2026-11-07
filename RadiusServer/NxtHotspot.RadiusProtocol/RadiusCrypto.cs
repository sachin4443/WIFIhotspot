using System.Security.Cryptography;
using System.Text;

namespace NxtHotspot.RadiusProtocol;

/// <summary>Authenticator, password and Message-Authenticator calculations (RFC 2865, 2866, 2869, 5176).</summary>
public static class RadiusCrypto
{
    private static readonly byte[] ZeroAuthenticator = new byte[16];

    // ---------------- Authenticators ----------------

    /// <summary>Random Request Authenticator for Access-Request (RFC 2865 §3).</summary>
    public static byte[] NewRequestAuthenticator() => RandomNumberGenerator.GetBytes(16);

    /// <summary>MD5(Code + Identifier + Length + RequestAuthenticator + Attributes + Secret).</summary>
    public static byte[] ResponseAuthenticator(byte[] encodedResponseWithRequestAuth, byte[] secret)
    {
        var input = new byte[encodedResponseWithRequestAuth.Length + secret.Length];
        encodedResponseWithRequestAuth.CopyTo(input, 0);
        secret.CopyTo(input, encodedResponseWithRequestAuth.Length);
        return MD5.HashData(input);
    }

    /// <summary>
    /// Encodes a response to <paramref name="request"/>: adds Message-Authenticator when the request had one (or when forced),
    /// then computes the Response Authenticator. The response's attributes must already be in place.
    /// </summary>
    public static byte[] EncodeResponse(RadiusPacket response, RadiusPacket request, byte[] secret, bool addMessageAuthenticator)
    {
        response.Identifier = request.Identifier;
        response.Authenticator = (byte[])request.Authenticator.Clone();
        response.Attributes.RemoveAll(a => a.Type == RadiusAttributeType.MessageAuthenticator);
        if (addMessageAuthenticator)
        {
            response.Attributes.Add(new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, new byte[16]));
            var tmp = response.Encode();
            var mac = HMACMD5.HashData(secret, tmp);
            response.Attributes[^1] = new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, mac);
        }
        var withRequestAuth = response.Encode();
        response.Authenticator = ResponseAuthenticator(withRequestAuth, secret);
        return response.Encode();
    }

    /// <summary>Accounting-Request / Disconnect-Request / CoA-Request authenticator: MD5 over the packet with a zero authenticator, plus secret.</summary>
    public static byte[] ZeroBasedAuthenticator(RadiusPacket packet, byte[] secret)
    {
        var saved = packet.Authenticator;
        packet.Authenticator = ZeroAuthenticator;
        var encoded = packet.Encode();
        packet.Authenticator = saved;
        return ResponseAuthenticator(encoded, secret);
    }

    /// <summary>Signs an Accounting/Disconnect/CoA request in place and returns its bytes. Adds Message-Authenticator if asked.</summary>
    public static byte[] EncodeZeroBasedRequest(RadiusPacket packet, byte[] secret, bool addMessageAuthenticator)
    {
        packet.Attributes.RemoveAll(a => a.Type == RadiusAttributeType.MessageAuthenticator);
        packet.Authenticator = new byte[16];
        if (addMessageAuthenticator)
        {
            packet.Attributes.Add(new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, new byte[16]));
            var mac = HMACMD5.HashData(secret, packet.Encode());
            packet.Attributes[^1] = new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, mac);
        }
        packet.Authenticator = ZeroBasedAuthenticator(packet, secret);
        return packet.Encode();
    }

    /// <summary>Verifies the authenticator of an Accounting-Request (RFC 2866 §3) or Disconnect/CoA request (RFC 5176 §3.5).</summary>
    public static bool VerifyZeroBasedRequest(RadiusPacket packet, byte[] secret) =>
        CryptographicOperations.FixedTimeEquals(ZeroBasedAuthenticator(packet, secret), packet.Authenticator);

    /// <summary>Verifies a response (Access-Accept/Reject, Accounting-Response, Disconnect-ACK/NAK…) against the request authenticator.</summary>
    public static bool VerifyResponse(RadiusPacket response, byte[] requestAuthenticator, byte[] secret)
    {
        var received = response.Authenticator;
        response.Authenticator = requestAuthenticator;
        var expected = ResponseAuthenticator(response.Encode(), secret);
        response.Authenticator = received;
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    // ---------------- Message-Authenticator (RFC 2869 §5.14 / RFC 3579 §3.2) ----------------

    /// <summary>
    /// Validates Message-Authenticator on a request. Returns null when the attribute is absent.
    /// For Access-Request the request authenticator is used as-is; for zero-based requests it is zeroed.
    /// </summary>
    public static bool? VerifyMessageAuthenticator(RadiusPacket packet, byte[] secret)
    {
        var index = packet.Attributes.FindIndex(a => a.Type == RadiusAttributeType.MessageAuthenticator);
        if (index < 0) return null;
        var received = packet.Attributes[index].Value;
        if (received.Length != 16) return false;
        var savedAttr = packet.Attributes[index];
        var savedAuth = packet.Authenticator;
        try
        {
            packet.Attributes[index] = new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, new byte[16]);
            if (packet.Code is RadiusCode.AccountingRequest or RadiusCode.DisconnectRequest or RadiusCode.CoaRequest)
                packet.Authenticator = new byte[16];
            var expected = HMACMD5.HashData(secret, packet.Encode());
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        finally
        {
            packet.Attributes[index] = savedAttr;
            packet.Authenticator = savedAuth;
        }
    }

    // ---------------- PAP (RFC 2865 §5.2) ----------------

    public static byte[] EncryptPap(string password, byte[] secret, byte[] requestAuthenticator)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        if (plain.Length > 128) throw new RadiusPacketException("PAP passwords are limited to 128 bytes.");
        var padded = new byte[Math.Max(16, (plain.Length + 15) / 16 * 16)];
        plain.CopyTo(padded, 0);
        var result = new byte[padded.Length];
        var previous = requestAuthenticator;
        for (var i = 0; i < padded.Length; i += 16)
        {
            var b = MD5.HashData(Concat(secret, previous));
            for (var j = 0; j < 16; j++) result[i + j] = (byte)(padded[i + j] ^ b[j]);
            previous = result.AsSpan(i, 16).ToArray();
        }
        return result;
    }

    public static string DecryptPap(byte[] encrypted, byte[] secret, byte[] requestAuthenticator)
    {
        if (encrypted.Length < 16 || encrypted.Length > 128 || encrypted.Length % 16 != 0)
            throw new RadiusPacketException("User-Password must be 16–128 bytes in multiples of 16.");
        var plain = new byte[encrypted.Length];
        var previous = requestAuthenticator;
        for (var i = 0; i < encrypted.Length; i += 16)
        {
            var b = MD5.HashData(Concat(secret, previous));
            for (var j = 0; j < 16; j++) plain[i + j] = (byte)(encrypted[i + j] ^ b[j]);
            previous = encrypted.AsSpan(i, 16).ToArray();
        }
        var end = Array.IndexOf(plain, (byte)0);
        return Encoding.UTF8.GetString(plain, 0, end < 0 ? plain.Length : end);
    }

    // ---------------- CHAP (RFC 2865 §5.3) ----------------

    /// <summary>CHAP-Password = CHAP Ident (1 byte) + MD5(Ident + password + challenge). Challenge = CHAP-Challenge or the Request Authenticator.</summary>
    public static bool VerifyChap(byte[] chapPassword, byte[] challenge, string password)
    {
        if (chapPassword.Length != 17) return false;
        var expected = ChapResponse(chapPassword[0], password, challenge);
        return CryptographicOperations.FixedTimeEquals(expected, chapPassword.AsSpan(1, 16));
    }

    public static byte[] ChapResponse(byte ident, string password, byte[] challenge) =>
        MD5.HashData(Concat(new[] { ident }, Encoding.UTF8.GetBytes(password), challenge));

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var pos = 0;
        foreach (var p in parts) { p.CopyTo(result, pos); pos += p.Length; }
        return result;
    }
}
