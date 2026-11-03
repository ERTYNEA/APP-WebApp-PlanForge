namespace PlanForge.Api.Tests;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using System.Net;
using System.Security.Claims;
using System.Text;

public sealed class HttpTestRequest(string body = "", string method = "POST") : HttpRequestData(new Mock<FunctionContext>().Object)
{
	public override Stream Body { get; } = new MemoryStream(Encoding.UTF8.GetBytes(body));
	public override IReadOnlyCollection<IHttpCookie> Cookies => [];
	public override HttpHeadersCollection Headers { get; } = new();
	public override IEnumerable<ClaimsIdentity> Identities => [];
	public override string Method => method;
	public override Uri Url => new("https://example.invalid/api/v1/auth/register");

	public override HttpResponseData CreateResponse() => new HttpTestResponse(FunctionContext);
}
