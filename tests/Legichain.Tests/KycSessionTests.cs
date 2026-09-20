using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Legichain.Tests;
public class KycSessionTests {
 sealed class Transport:HttpMessageHandler {
  public string State="running";public int Submissions;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
   var path=request.RequestUri!.AbsolutePath;string json;
   if(path=="/v1/kyc/applications")json="{\"application_id\":\"tr_app\",\"client_token\":\"internal\"}";
   else if(path=="/v2/operations/tr_op")json="{\"status\":\""+State+"\"}";
   else if(path.EndsWith("/submit")){Submissions++;json="{\"pending\":true,\"outcome\":null}";}
   else {Assert.Equal("internal",string.Join("",request.Headers.GetValues("X-KYC-Client-Token")));json="{\"operation_id\":\"tr_op\",\"status\":\"queued\",\"protocol\":1}";}
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted){Content=new StringContent(json,Encoding.UTF8,"application/json")});
  }
 }
 [Fact] public async Task GatesSubmissionAndPreservesInternalToken() {
  using var transport=new Transport();using var http=new HttpClient(transport);using var client=new LegichainClient("synthetic",httpClient:http);
  var session=await KycSession.StartAsync(client,new {liveness_required=false},"create");
  var receipt=await session.EvidenceAsync("liveness",new {mode="passive",frame_b64="test",frame_mime_type="image/jpeg"},"capture");
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.SubmitAsync());Assert.Equal(0,transport.Submissions);
  transport.State="failed";await Assert.ThrowsAsync<InvalidOperationException>(()=>session.WaitAsync(receipt.OperationId,TimeSpan.FromSeconds(1)));
  transport.State="completed";var result=await session.SubmitAsync();Assert.Equal("True",result["pending"]!.ToString());Assert.Equal(1,transport.Submissions);
 }
}
