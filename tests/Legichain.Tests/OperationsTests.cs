using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Legichain.Tests;

public class OperationsTests {
 sealed class Transport:HttpMessageHandler {
  public readonly List<string> Paths=new();
  public readonly List<string> Keys=new();
  public readonly List<string> Tokens=new();
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
   Paths.Add(request.RequestUri!.AbsolutePath);
   Keys.Add(request.Headers.TryGetValues("Idempotency-Key",out var keys)?string.Join("",keys):"");
   Tokens.Add(request.Headers.TryGetValues("X-KYC-Client-Token",out var tokens)?string.Join("",tokens):"");
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted){Content=new StringContent("{\"operation_id\":\"tr_op\",\"status\":\"queued\",\"status_url\":\"/v2/operations/tr_op\",\"deadline_at\":\"2026-09-07T00:00:00Z\",\"protocol\":1}",Encoding.UTF8,"application/json")});
  }
 }
 [Fact] public async Task ExplicitReceiptKeysAndStatus() {
  using var transport=new Transport();using var http=new HttpClient(transport);
  using var lc=new LegichainClient("synthetic.key",httpClient:http);
  for(int i=0;i<2;i++)Assert.Equal("queued",(await lc.EnqueueScreenAsync("person",new {name="Synthetic"},"stable-key")).Status);
  Assert.Equal(2,transport.Paths.Count);Assert.Equal("stable-key",transport.Keys[0]);Assert.Equal(transport.Keys[0],transport.Keys[1]);
  await lc.EnqueueKycEvidenceAsync("tr_app","nfc",new {access_error="synthetic"},"nfc","token");Assert.Equal("token",transport.Tokens[2]);
  await lc.EnqueueReportAsync("wallet",new {},"report");await lc.EnqueueKycReportAsync("tr_app",new {},"kyc-report");await lc.EnqueueAddressSubmitAsync("tr_av",new {},"av");
  Assert.Equal("queued",(await lc.OperationAsync("tr_op")).GetProperty("status").GetString());await lc.OperationTaskAsync("tr_op","task");await lc.OperationsAsync(state:"failed",limit:25);await lc.CancelOperationAsync("tr_op");
  int count=transport.Paths.Count;
  await Assert.ThrowsAsync<ArgumentException>(()=>lc.EnqueueScreenAsync("person",new {},""));
  await Assert.ThrowsAsync<ArgumentException>(()=>lc.EnqueueScreenAsync("../reports",new {},"key"));Assert.Equal(count,transport.Paths.Count);
 }
}
