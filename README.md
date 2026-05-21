# Legichain .NET SDK

Official .NET client for the **[Legichain](https://legichain.com)** AML, KYC
and Travel Rule API.

```powershell
dotnet add package Legichain --version 0.1.0
```

[![NuGet](https://img.shields.io/nuget/v/Legichain.svg)](https://www.nuget.org/packages/Legichain/)
[![License](https://img.shields.io/github/license/legichain/legichain-dotnet.svg)](https://github.com/legichain/legichain-dotnet/blob/main/LICENSE)

- .NET 8 + .NET Standard 2.0 (works on .NET Framework 4.6.1+)
- `System.Net.Http.HttpClient` + `System.Text.Json` — no third-party deps on net8
- Typed `LegichainException` carries the full RFC 7807 problem body
- HMAC-SHA256 webhook verifier (constant-time compare)

## Get an API key

Sign up at **<https://legichain.com>** — the Free plan ships with 1 RPS
and 300 monthly credits, no card required. Once signed in:

> **panel.legichain.com → Settings → API Keys → New key**

Keys look like `lc_live_<22>.sk_live_<44>` (production) or
`lc_test_<22>.sk_test_<44>` (test mode — never spends credits, safe in CI).
Store the secret half in your secret manager — the Argon2 hash means a
lost key can't be recovered. See the
[full API guide](https://legichain.com/developers) for plans, rate limits
and reference docs.

## Quick start

```csharp
using Legichain;
using Legichain.Models;

using var lc = new LegichainClient(Environment.GetEnvironmentVariable("LEGICHAIN_API_KEY")!);

try
{
    var r = await lc.ScreenPersonAsync(
        new PersonQuery("Vladimir Putin", Country: "RU", BirthDate: "1952-10-07"));

    switch (r.Summary.Recommendation)
    {
        case Recommendation.Block:  DenyOnboarding(customerId);                       break;
        case Recommendation.Review: QueueForCompliance(customerId, r.ScreeningId);    break;
        case Recommendation.Clear:  ApproveOnboarding(customerId);                    break;
    }

    Console.WriteLine($"{r.CostCredits} credits spent; {r.CreditsRemaining} remaining");
}
catch (LegichainException e)
{
    // RFC 7807 — branch on stable codes
    switch (e.Code)
    {
        case "BIL_001_INSUFFICIENT_CREDITS": TopUp();           break;
        case "RL_001_RATE_LIMITED":          await Backoff();   break;
        case "AUTH_002_INVALID_TOKEN":       RotateKey();       break;
        default:                             Log(e.Status, e.Detail); break;
    }
}
```

### Company + crypto wallet

```csharp
var company = await lc.ScreenCompanyAsync(
    new CompanyQuery("Rosneft Oil Company", Country: "RU"));

var wallet = await lc.ScreenCryptoAsync(
    new CryptoQuery("0x098B716B8Aaf21512996dC57EB0615e2383E2f96"));
```

### Batch (sync + async)

```csharp
var items = new object[]
{
    new PersonQuery("Acme Trading GmbH"),
    new CryptoQuery("TVj7RNVH...", "tron"),
    new PersonQuery("Maria Lopez", "ES"),
};

// up to 200 items synchronously
var results = await lc.ScreenBatchAsync(items);

// async — result delivered to your webhook
var job  = await lc.ScreenBatchAsyncJobAsync(items, webhookUrl: "https://you.example.com/webhooks/legichain");
var done = await lc.GetJobAsync(job.JobId);
```

### PDF reports

```csharp
byte[] pdf = await lc.ReportWalletAsync(new CryptoQuery(
    "0x6c0bD2BB04Fda9CBfeBb8DC1208Db32a0F8a4Edd", "eth"));
await File.WriteAllBytesAsync("wallet.pdf", pdf);
```

### Idempotency

```csharp
await lc.ScreenPersonAsync(
    new PersonQuery("Maria Lopez"),
    idempotencyKey: "onboarding-2026-05-20-7f3c");
// Re-running the same key within 24h returns the cached response.
```

### Webhook verification (ASP.NET Core)

```csharp
using Legichain;

app.MapPost("/webhooks/legichain", async (HttpContext ctx) =>
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var body = ms.ToArray();

    var ok = Webhooks.VerifySignature(
        body,
        ctx.Request.Headers["Legichain-Signature"]!,
        Environment.GetEnvironmentVariable("LEGICHAIN_WEBHOOK_SECRET")!);

    if (!ok) return Results.Unauthorized();
    // ... handle the event
    return Results.Ok();
});
```

## Configuration

```csharp
using var lc = new LegichainClient(
    apiKey: key,
    baseUrl: "https://staging.api.legichain.com",
    requestTimeout: TimeSpan.FromSeconds(60),
    defaultHeaders: new Dictionary<string, string> { ["X-My-App"] = "billing-svc" });
```

Inject your own `HttpClient` (e.g. one that goes through a corporate
proxy or uses `IHttpClientFactory`):

```csharp
var handler = new HttpClientHandler
{
    Proxy    = new WebProxy("http://proxy.bank.tr:8080"),
    UseProxy = true,
};
var http = new HttpClient(handler);

using var lc = new LegichainClient(key, httpClient: http);
```

## Reference

| Method | Endpoint |
| --- | --- |
| `lc.ScreenPersonAsync(q)`            | `POST /v1/screen/person` |
| `lc.ScreenCompanyAsync(q)`           | `POST /v1/screen/company` |
| `lc.ScreenCryptoAsync(q)`            | `POST /v1/screen/crypto` |
| `lc.ScreenBatchAsync(items)`         | `POST /v1/screen/batch` |
| `lc.ScreenBatchAsyncJobAsync(items)` | `POST /v1/screen/batch/async` |
| `lc.GetJobAsync(jobId)`              | `GET  /v1/screen/jobs/{id}` |
| `lc.ReportWalletAsync(q)`            | `POST /v1/reports/wallet` → `byte[]` (PDF) |
| `lc.ReportPersonAsync(q)`            | `POST /v1/reports/person` → `byte[]` (PDF) |
| `lc.ReportCompanyAsync(q)`           | `POST /v1/reports/company` → `byte[]` (PDF) |
| `lc.GetStatusAsync()`                | `GET  /v1/status` |

---

## Versioning & support

- Tracks API `v1`. Breaking changes ship on `/v2/` with ≥ 6 months overlap.
- Status: <https://legichain.com/status>
- Email: `contact@legichain.com`
- GitHub: <https://github.com/legichain/legichain-dotnet>
