namespace PlanForge.Api.Tests;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

public sealed class HttpTestResponse(FunctionContext context) : HttpResponseData(context)
{
	public override Stream Body { get; set; } = new MemoryStream();
	public override HttpCookies Cookies => throw new NotSupportedException();
	public override HttpHeadersCollection Headers { get; set; } = new();
	public override HttpStatusCode StatusCode { get; set; }
}
