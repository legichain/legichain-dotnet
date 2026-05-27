# Changelog

## v0.2.0 — 2026-05-28

* **KYC** identity verification — `KycCreateApplicationAsync`,
  `KycStatusAsync`, `KycUploadDocumentAsync`, `KycSubmitNfcAsync`
  (+ `KycNfcAccessErrorAsync` convenience), `KycUploadSelfieAsync`,
  `KycLivenessChallengeAsync`, `KycSubmitLivenessAsync`,
  `KycSubmitAsync`, `KycRetryAsync`, `KycExtendTtlAsync`.
  * Tenant admin: `KycAdminListAsync`, `KycAdminDetailAsync`,
    `KycAdminApproveAsync`, `KycAdminRejectAsync`,
    `KycAdminRequestRetryAsync`.
* **Address Verification** — `AddressVerificationCreateAsync`,
  `AddressVerificationUploadProofAsync`,
  `AddressVerificationSubmitAsync`,
  `AddressVerificationStatusAsync`.
* **Personas** — `PersonaCreateAsync`, `PersonaListAsync`,
  `PersonaGetAsync`.
* `PostJsonAsync` / `GetJsonAsync` overloads accept `clientToken` →
  `X-KYC-Client-Token` header for per-application calls.
* Server-side: forwards base64 NFC chip bytes produced by a mobile
  client; this SDK never reads chips itself.

# Changelog

## [0.1.0] — 2026-05-21
Initial public release.

- Sync + async client wrapping `POST /v1/screen/person|company|crypto|batch`.
- Async batch + job polling.
- PDF report endpoints (`wallet|person|company`).
- Typed `LegichainException` with full RFC 7807 problem body.
- HMAC-SHA256 webhook verifier (constant-time compare).
- Multi-targets `netstandard2.0` (works on .NET Framework 4.6.1+) and `net8.0`.
