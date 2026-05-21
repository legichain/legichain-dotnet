using System;
using System.Security.Cryptography;
using System.Text;

namespace Legichain;

/// <summary>HMAC-SHA256 verifier for inbound Legichain webhook deliveries.</summary>
/// <remarks>
/// <para>The server signs every delivery with
/// <c>HMAC-SHA256(secret, "{t}.{body}")</c> and emits the
/// <c>Legichain-Signature: t=&lt;unix&gt;,v1=&lt;hex&gt;</c> header.</para>
/// <para>Verification parses the header, rejects messages older than
/// the tolerance (default 5 min), recomputes the HMAC and compares it
/// in constant time.</para>
/// </remarks>
public static class Webhooks
{
    /// <summary>Default replay window — matches the server tolerance.</summary>
    public const long DefaultToleranceSeconds = 5 * 60L;

    public static bool VerifySignature(
        byte[] body, string signatureHeader, string secret,
        long toleranceSeconds = DefaultToleranceSeconds,
        long? nowEpochSeconds = null)
    {
        if (body == null || string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
            return false;
        if (toleranceSeconds <= 0) toleranceSeconds = DefaultToleranceSeconds;

        string? tStr = null, v1 = null;
        foreach (var raw in signatureHeader.Split(','))
        {
            var p = raw.Trim();
            if (p.StartsWith("t=",  StringComparison.Ordinal))      tStr = p.Substring(2);
            else if (p.StartsWith("v1=", StringComparison.Ordinal)) v1   = p.Substring(3);
        }
        if (tStr is null || v1 is null) return false;
        if (!long.TryParse(tStr, out var ts)) return false;

        var now = nowEpochSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > toleranceSeconds) return false;

        byte[] expected;
        using (var mac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            var prefix = Encoding.UTF8.GetBytes(tStr + ".");
            var buffer = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, buffer, 0, prefix.Length);
            Buffer.BlockCopy(body,   0, buffer, prefix.Length, body.Length);
            expected = mac.ComputeHash(buffer);
        }

        var received = HexDecode(v1);
        if (received is null || received.Length != expected.Length) return false;

        return FixedTimeEquals(expected, received);
    }

    // CryptographicOperations.FixedTimeEquals exists on net8 but not netstandard2.0.
    // Manual implementation works on both with no perf hit at HMAC-SHA256 sizes.
    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static byte[]? HexDecode(string s)
    {
        if ((s.Length & 1) != 0) return null;
        var len = s.Length / 2;
        var bytes = new byte[len];
        for (int i = 0; i < len; i++)
        {
            int hi = FromHex(s[i * 2]);
            int lo = FromHex(s[i * 2 + 1]);
            if (hi < 0 || lo < 0) return null;
            bytes[i] = (byte)((hi << 4) | lo);
        }
        return bytes;
    }

    private static int FromHex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
