using System.Net;
using System.Text;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Tests;

public class RadiusProtocolTests
{
    private static byte[] Hex(string h) => Convert.FromHexString(h.Replace(" ", ""));
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("xyzzy5461");

    // RFC 2865 §7.1: NAS at 192.168.1.16 sends an Access-Request for user "nemo", password "arctangent", port 3.
    private const string RfcAccessRequest =
        "01 00 00 38 0f 40 3f 94 73 97 80 57 bd 83 d5 cb 98 f4 22 7a 01 06 6e 65 6d 6f 02 12 0d be 70 8d 93 d4 13 ce 31 96 e4 3f 78 2a 0a ee 04 06 c0 a8 01 10 05 06 00 00 00 03";
    // ... and the server answers with this Access-Accept.
    private const string RfcAccessAccept =
        "02 00 00 26 86 fe 22 0e 76 24 ba 2a 10 05 f6 bf 9b 55 e0 b2 06 06 00 00 00 01 0f 06 00 00 00 00 0e 06 c0 a8 01 03";

    [Fact]
    public void Parses_rfc2865_access_request_and_decrypts_pap_password()
    {
        var p = RadiusPacket.Parse(Hex(RfcAccessRequest));
        Assert.Equal(RadiusCode.AccessRequest, p.Code);
        Assert.Equal("nemo", p.GetString(RadiusAttributeType.UserName));
        Assert.Equal(IPAddress.Parse("192.168.1.16"), p.Get(RadiusAttributeType.NasIpAddress)!.AsAddress());
        Assert.Equal(3u, p.GetInteger(RadiusAttributeType.NasPort));
        Assert.Equal("arctangent", RadiusCrypto.DecryptPap(p.Get(RadiusAttributeType.UserPassword)!.Value, RfcSecret, p.Authenticator));
        Assert.Equal(Hex(RfcAccessRequest), p.Encode());
    }

    [Fact]
    public void Computes_rfc2865_access_accept_response_authenticator()
    {
        var request = RadiusPacket.Parse(Hex(RfcAccessRequest));
        var accept = new RadiusPacket(RadiusCode.AccessAccept, 0)
            .Add(RadiusAttribute.Integer(RadiusAttributeType.ServiceType, 1))
            .Add(RadiusAttribute.Integer(15, 0))                           // Login-Service = Telnet
            .Add(RadiusAttribute.Address(14, IPAddress.Parse("192.168.1.3"))); // Login-IP-Host
        var bytes = RadiusCrypto.EncodeResponse(accept, request, RfcSecret, addMessageAuthenticator: false);
        Assert.Equal(Hex(RfcAccessAccept), bytes);
        Assert.True(RadiusCrypto.VerifyResponse(RadiusPacket.Parse(bytes), request.Authenticator, RfcSecret));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("exactly-16-chars")]
    [InlineData("a much longer password that spans three 16-byte blocks")]
    public void Pap_round_trips(string password)
    {
        var secret = Encoding.ASCII.GetBytes("s3cret-shared");
        var auth = RadiusCrypto.NewRequestAuthenticator();
        var enc = RadiusCrypto.EncryptPap(password, secret, auth);
        Assert.Equal(0, enc.Length % 16);
        Assert.Equal(password, RadiusCrypto.DecryptPap(enc, secret, auth));
        Assert.NotEqual(password, RadiusCrypto.DecryptPap(enc, Encoding.ASCII.GetBytes("wrong"), auth));
    }

    [Fact]
    public void Chap_verifies_with_challenge_attribute_or_authenticator()
    {
        var challenge = RadiusCrypto.NewRequestAuthenticator();
        var resp = RadiusCrypto.ChapResponse(7, "letmein", challenge);
        var chap = new byte[17];
        chap[0] = 7;
        resp.CopyTo(chap, 1);
        Assert.True(RadiusCrypto.VerifyChap(chap, challenge, "letmein"));
        Assert.False(RadiusCrypto.VerifyChap(chap, challenge, "wrong"));
        Assert.False(RadiusCrypto.VerifyChap(chap[..16], challenge, "letmein"));
    }

    [Fact]
    public void Message_authenticator_is_added_and_verified_and_detects_tampering()
    {
        var secret = Encoding.ASCII.GetBytes("message-auth-secret");
        var req = new RadiusPacket(RadiusCode.AccessRequest, 9, RadiusCrypto.NewRequestAuthenticator())
            .Add(RadiusAttribute.String(RadiusAttributeType.UserName, "alice"))
            .Add(new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, new byte[16]));
        var mac = System.Security.Cryptography.HMACMD5.HashData(secret, req.Encode());
        req.Attributes[^1] = new RadiusAttribute(RadiusAttributeType.MessageAuthenticator, mac);
        var parsed = RadiusPacket.Parse(req.Encode());
        Assert.True(RadiusCrypto.VerifyMessageAuthenticator(parsed, secret));
        Assert.False(RadiusCrypto.VerifyMessageAuthenticator(parsed, Encoding.ASCII.GetBytes("other")));

        var tampered = req.Encode();
        tampered[^20] ^= 0xFF; // flip a byte of User-Name
        Assert.False(RadiusCrypto.VerifyMessageAuthenticator(RadiusPacket.Parse(tampered), secret));
        Assert.Null(RadiusCrypto.VerifyMessageAuthenticator(new RadiusPacket(RadiusCode.AccessRequest, 1), secret));

        // Responses signed with Message-Authenticator still verify with the response authenticator.
        var accept = RadiusCrypto.EncodeResponse(new RadiusPacket(RadiusCode.AccessAccept, 0), parsed, secret, addMessageAuthenticator: true);
        var acceptPacket = RadiusPacket.Parse(accept);
        Assert.True(acceptPacket.Has(RadiusAttributeType.MessageAuthenticator));
        Assert.True(RadiusCrypto.VerifyResponse(acceptPacket, parsed.Authenticator, secret));
    }

    [Fact]
    public void Accounting_and_disconnect_requests_sign_and_verify()
    {
        var secret = Encoding.ASCII.GetBytes("acct-secret-123");
        var acct = new RadiusPacket(RadiusCode.AccountingRequest, 3)
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctStatusType, AcctStatusType.Start))
            .Add(RadiusAttribute.String(RadiusAttributeType.AcctSessionId, "80000001"));
        var bytes = RadiusCrypto.EncodeZeroBasedRequest(acct, secret, addMessageAuthenticator: false);
        var parsed = RadiusPacket.Parse(bytes);
        Assert.True(RadiusCrypto.VerifyZeroBasedRequest(parsed, secret));
        Assert.False(RadiusCrypto.VerifyZeroBasedRequest(parsed, Encoding.ASCII.GetBytes("nope")));

        var dm = new RadiusPacket(RadiusCode.DisconnectRequest, 4).Add(RadiusAttribute.String(RadiusAttributeType.AcctSessionId, "80000001"));
        var dmBytes = RadiusCrypto.EncodeZeroBasedRequest(dm, secret, addMessageAuthenticator: true);
        var dmParsed = RadiusPacket.Parse(dmBytes);
        Assert.True(RadiusCrypto.VerifyZeroBasedRequest(dmParsed, secret));
        Assert.True(RadiusCrypto.VerifyMessageAuthenticator(dmParsed, secret));
    }

    [Fact]
    public void Vendor_attributes_round_trip()
    {
        var a = RadiusAttribute.VendorString(MikrotikAttribute.VendorId, MikrotikAttribute.RateLimit, "5120k/10240k");
        var v = a.AsVendor()!.Value;
        Assert.Equal(14988u, v.VendorId);
        Assert.Equal(MikrotikAttribute.RateLimit, v.VendorType);
        Assert.Equal("5120k/10240k", Encoding.UTF8.GetString(v.Value));
    }

    [Theory]
    [InlineData("01 00 00 14")]                                                        // shorter than header
    [InlineData("01 00 00 13 00000000000000000000000000000000")]                       // length < 20
    [InlineData("01 00 00 30 00000000000000000000000000000000")]                       // length > datagram
    [InlineData("01 00 00 16 00000000000000000000000000000000 01 01")]                 // attribute length 1
    [InlineData("01 00 00 17 00000000000000000000000000000000 01 05 61")]              // attribute overruns
    public void Rejects_malformed_packets(string hex)
    {
        Assert.Throws<RadiusPacketException>(() => RadiusPacket.Parse(Hex(hex)));
    }

    [Fact]
    public void Rejects_invalid_pap_length()
    {
        Assert.Throws<RadiusPacketException>(() => RadiusCrypto.DecryptPap(new byte[15], RfcSecret, new byte[16]));
        Assert.Throws<RadiusPacketException>(() => RadiusCrypto.DecryptPap(new byte[144], RfcSecret, new byte[16]));
    }
}
