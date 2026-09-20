using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Legichain.Models;

namespace Legichain;

/// <summary>One KYC applicant. The SDK manages the returned application credential.</summary>
public sealed class KycSession
{
    private readonly LegichainClient _client;
    private readonly string _token;
    private readonly ConcurrentDictionary<string, byte> _operations = new();
    public string ApplicationId { get; }
    private KycSession(LegichainClient client, string id, string token)
    { _client = client; ApplicationId = id; _token = token; }

    public static async Task<KycSession> StartAsync(LegichainClient client, object application, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || System.Text.Encoding.UTF8.GetByteCount(idempotencyKey) > 256)
            throw new ArgumentException("An explicit 1–256 byte idempotency key is required", nameof(idempotencyKey));
        var created = await client.KycCreateApplicationAsync(application, idempotencyKey, ct).ConfigureAwait(false);
        return new KycSession(client, created["application_id"]!.ToString()!, created["client_token"]!.ToString()!);
    }
    public async Task<OperationAccepted> EvidenceAsync(string step, object body, string idempotencyKey, CancellationToken ct = default)
    {
        var receipt = await _client.EnqueueKycEvidenceAsync(ApplicationId, step, body, idempotencyKey, _token, ct).ConfigureAwait(false);
        _operations[receipt.OperationId] = 0; return receipt;
    }
    public Task<Dictionary<string, object?>> ChallengeAsync(CancellationToken ct = default)
        => _client.KycLivenessChallengeAsync(ApplicationId, _token, 3, 120, ct);
    public Task<Dictionary<string, object?>> StatusAsync(CancellationToken ct = default)
        => _client.KycStatusAsync(ApplicationId, ct: ct);

    public async Task<JsonElement> WaitAsync(string operationId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!_operations.ContainsKey(operationId)) throw new ArgumentException("Operation does not belong to this SDK session");
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var watch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await _client.OperationAsync(operationId, ct).ConfigureAwait(false);
            var state = result.GetProperty("status").GetString();
            if (state == "completed") return result;
            if (state is "failed" or "expired" or "cancelled") throw new InvalidOperationException($"KYC operation {operationId} {state}");
            var remaining = timeout - watch.Elapsed;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("Evidence is still processing; keep its receipt");
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }
    /// <summary>Records submission; the identity outcome is delivered by webhook.</summary>
    public async Task<Dictionary<string, object?>> SubmitAsync(CancellationToken ct = default)
    {
        foreach (var id in _operations.Keys)
        {
            var operation = await _client.OperationAsync(id, ct).ConfigureAwait(false);
            if (operation.GetProperty("status").GetString() != "completed") throw new InvalidOperationException("Evidence is not complete; do not submit yet");
        }
        return await _client.KycSubmitAsync(ApplicationId, _token, ct).ConfigureAwait(false);
    }
}
