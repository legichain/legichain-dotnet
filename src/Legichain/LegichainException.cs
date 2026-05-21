using System;
using Legichain.Models;

namespace Legichain;

/// <summary>
/// Raised when the API returns a non-2xx response. The full RFC 7807
/// problem body is exposed via <see cref="Problem"/>; the stable error
/// code is on <see cref="Code"/> — branch on that, not on <see cref="Detail"/>.
/// </summary>
public class LegichainException : Exception
{
    /// <summary>Stable error code (e.g. <c>BIL_001_INSUFFICIENT_CREDITS</c>).</summary>
    public string Code { get; }

    /// <summary>HTTP status returned by the API.</summary>
    public int Status { get; }

    /// <summary>Human-readable explanation from the server.</summary>
    public string? Detail { get; }

    /// <summary>Full RFC 7807 problem body.</summary>
    public ProblemDetails? Problem { get; }

    public LegichainException(
        string code, int status, string? detail = null,
        ProblemDetails? problem = null, Exception? innerException = null)
        : base($"[{code}] HTTP {status}: {detail ?? "(no detail)"}", innerException)
    {
        Code = code;
        Status = status;
        Detail = detail;
        Problem = problem;
    }
}

/// <summary>Raised when the transport itself fails (timeout, DNS, TLS).</summary>
public sealed class LegichainNetworkException : LegichainException
{
    public LegichainNetworkException(string message, Exception innerException)
        : base("NETWORK_ERROR", 0, message, null, innerException)
    {
    }
}
