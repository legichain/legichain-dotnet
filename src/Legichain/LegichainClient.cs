using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Legichain.Models;

namespace Legichain;

/// <summary>Official client for the Legichain AML/KYC/Travel-Rule API.</summary>
/// <example>
/// <code>
/// using var lc = new LegichainClient(Environment.GetEnvironmentVariable("LEGICHAIN_API_KEY")!);
/// var r = await lc.ScreenPersonAsync(new PersonQuery("Vladimir Putin", "RU", "1952-10-07"));
/// switch (r.Summary.Recommendation)
/// {
///     case Recommendation.Block:  DenyOnboarding();  break;
///     case Recommendation.Review: QueueForCompliance(r.ScreeningId); break;
///     case Recommendation.Clear:  Approve();         break;
/// }
/// </code>
/// </example>
public sealed class LegichainClient : IDisposable
{
    private const string DefaultBaseUrl = "https://api.legichain.com";
    private const string Version        = "0.1.0";

    private readonly HttpClient            _http;
    private readonly bool                  _ownsHttp;
    private readonly string                _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public LegichainClient(
        string apiKey,
        string baseUrl = DefaultBaseUrl,
        HttpClient? httpClient = null,
        TimeSpan? requestTimeout = null,
        IReadOnlyDictionary<string, string>? defaultHeaders = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey is required", nameof(apiKey));

        _baseUrl  = baseUrl.TrimEnd('/');
        _ownsHttp = httpClient is null;
        _http     = httpClient ?? new HttpClient();
        if (requestTimeout is { } t) _http.Timeout = t;
        else if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(30);

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"legichain-dotnet/{Version}");
        if (defaultHeaders is not null)
            foreach (var kv in defaultHeaders)
                _http.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
    }

    // ── Screening ────────────────────────────────────────────────
    public Task<ScreeningResponse> ScreenPersonAsync(
        PersonQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/person", query, idempotencyKey, ct);

    public Task<ScreeningResponse> ScreenCompanyAsync(
        CompanyQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/company", query, idempotencyKey, ct);

    public Task<ScreeningResponse> ScreenCryptoAsync(
        CryptoQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/crypto", query, idempotencyKey, ct);

    public Task<IReadOnlyList<ScreeningResponse>> ScreenBatchAsync(
        IEnumerable<object> items, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<IReadOnlyList<ScreeningResponse>>(
               "/v1/screen/batch", new { items = items.ToArray() }, idempotencyKey, ct);

    public Task<BatchAsyncResponse> ScreenBatchAsyncJobAsync(
        IEnumerable<object> items, string? webhookUrl = null,
        string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<BatchAsyncResponse>(
               "/v1/screen/batch/async",
               new { items = items.ToArray(), webhook_url = webhookUrl },
               idempotencyKey, ct);

    public Task<JobStatus> GetJobAsync(string jobId, CancellationToken ct = default)
        => GetJsonAsync<JobStatus>($"/v1/screen/jobs/{Uri.EscapeDataString(jobId)}", ct);

    // ── PDF reports ──────────────────────────────────────────────
    public Task<byte[]> ReportWalletAsync(CryptoQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/wallet", query, ct);

    public Task<byte[]> ReportPersonAsync(PersonQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/person", query, ct);

    public Task<byte[]> ReportCompanyAsync(CompanyQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/company", query, ct);

    // ── Misc ─────────────────────────────────────────────────────
    public Task<StatusPayload> GetStatusAsync(CancellationToken ct = default)
        => GetJsonAsync<StatusPayload>("/v1/status", ct);

    // ── Internals ────────────────────────────────────────────────
    private async Task<T> PostJsonAsync<T>(
        string path, object body, string? idempotencyKey, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(idempotencyKey))
            req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        using var resp = await SendAsync(req, ct).ConfigureAwait(false);
        return await ReadJsonAsync<T>(resp, ct).ConfigureAwait(false);
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        using var req  = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
        using var resp = await SendAsync(req, ct).ConfigureAwait(false);
        return await ReadJsonAsync<T>(resp, ct).ConfigureAwait(false);
    }

    private async Task<byte[]> PostJsonForBytesAsync(string path, object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));

        using var resp = await SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) await ThrowFromAsync(resp, ct).ConfigureAwait(false);
        return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                              .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LegichainNetworkException(ex.Message, ex);
        }
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode) await ThrowFromAsync(resp, ct).ConfigureAwait(false);
        var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var parsed = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct)
                                         .ConfigureAwait(false);
        return parsed ?? throw new LegichainException(
            "INVALID_RESPONSE", (int)resp.StatusCode, "empty response body");
    }

    private async Task ThrowFromAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var status = (int)resp.StatusCode;
        ProblemDetails? problem = null;
        string? detail = null;
        string  code   = $"HTTP_{status}";
        try
        {
            var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream, _jsonOptions, ct)
                                          .ConfigureAwait(false);
            if (problem is not null)
            {
                detail = problem.Detail ?? problem.Title;
                code   = problem.Code ?? code;
            }
        }
        catch { /* not RFC 7807 — fall through */ }

        throw new LegichainException(code, status, detail, problem);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
