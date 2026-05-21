using System;
using System.Security.Cryptography;
using System.Text;
using Legichain;
using Xunit;

namespace Legichain.Tests;

public class WebhooksTests
{
    private const string Secret = "whsec_test_legichain";

    [Fact]
    public void Verifies_valid_signature()
    {
        var body = Encoding.UTF8.GetBytes("{\"event\":\"screening.completed\"}");
        var ts   = 1716240000L;
        var sig  = Sign(Secret, ts, body);
        var header = $"t={ts},v1={sig}";

        Assert.True(Webhooks.VerifySignature(body, header, Secret, 300, ts));
    }

    [Fact]
    public void Rejects_expired_signature()
    {
        var body = Encoding.UTF8.GetBytes("x");
        var ts   = 1716240000L;
        var sig  = Sign(Secret, ts, body);
        var header = $"t={ts},v1={sig}";

        // 10 minutes later, tolerance 5 minutes → reject
        Assert.False(Webhooks.VerifySignature(body, header, Secret, 300, ts + 600));
    }

    [Fact]
    public void Rejects_tampered_body()
    {
        var body = Encoding.UTF8.GetBytes("{\"amount\":100}");
        var ts   = 1716240000L;
        var sig  = Sign(Secret, ts, body);
        var header = $"t={ts},v1={sig}";

        var tampered = Encoding.UTF8.GetBytes("{\"amount\":99999}");
        Assert.False(Webhooks.VerifySignature(tampered, header, Secret, 300, ts));
    }

    [Fact]
    public void Rejects_malformed_header()
    {
        var body = Encoding.UTF8.GetBytes("x");
        Assert.False(Webhooks.VerifySignature(body, "garbage", Secret, 300, 1716240000L));
        Assert.False(Webhooks.VerifySignature(body, "t=abc,v1=def", Secret, 300, 1716240000L));
        Assert.False(Webhooks.VerifySignature(body, "", Secret, 300, 1716240000L));
    }

    private static string Sign(string secret, long ts, byte[] body)
    {
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var prefix = Encoding.UTF8.GetBytes(ts + ".");
        var buf = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, buf, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, buf, prefix.Length, body.Length);
        var hash = mac.ComputeHash(buf);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
