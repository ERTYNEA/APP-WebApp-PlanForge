namespace PlanForge.Api.Tests;

using PlanForge.Api.Http;
using PlanForge.Application.Common;
using System.Net;

public sealed class HttpEndpointRunnerTests
{
	[Theory]
	[InlineData(400)]
	[InlineData(401)]
	[InlineData(403)]
	[InlineData(404)]
	[InlineData(409)]
	[InlineData(413)]
	[InlineData(429)]
	public async Task ExecuteAsync_WhenApplicationRejects_ReturnsStructuredStatusAsync(int status)
	{
		var runner = new HttpEndpointRunner();
		var response = await runner.ExecuteAsync(new HttpTestRequest(), () => throw new PlanForgeException(status, "Safe explanation."));

		Assert.Equal((HttpStatusCode)status, response.StatusCode);
		response.Body.Position = 0;
		string body = await new StreamReader(response.Body).ReadToEndAsync();
		Assert.Contains("Safe explanation.", body);
		Assert.Contains("correlationId", body);
	}

	[Fact]
	public async Task ExecuteAsync_WhenUnexpectedFailure_DoesNotLeakExceptionDetailsAsync()
	{
		var runner = new HttpEndpointRunner();
		var response = await runner.ExecuteAsync(new HttpTestRequest(), () => throw new InvalidOperationException("synthetic-sensitive-detail"));

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		response.Body.Position = 0;
		string body = await new StreamReader(response.Body).ReadToEndAsync();
		Assert.DoesNotContain("synthetic-sensitive-detail", body);
		Assert.Contains("correlationId", body);
	}

	[Theory]
	[InlineData("{invalid", 400)]
	[InlineData("null", 400)]
	public async Task ReadJsonAsync_WhenMalformed_ReturnsBadRequestAsync(string body, int status)
	{
		var runner = new HttpEndpointRunner();
		var request = new HttpTestRequest(body);
		var response = await runner.ExecuteAsync(request, async () => await runner.ReadJsonAsync<Dictionary<string, string>>(request));

		Assert.Equal((HttpStatusCode)status, response.StatusCode);
	}

	[Fact]
	public async Task ReadJsonAsync_WhenPayloadExceedsLimit_ReturnsPayloadTooLargeAsync()
	{
		var runner = new HttpEndpointRunner();
		var request = new HttpTestRequest("{\"value\":\"" + new string('x', 70_000) + "\"}");
		var response = await runner.ExecuteAsync(request, async () => await runner.ReadJsonAsync<Dictionary<string, string>>(request));

		Assert.Equal((HttpStatusCode)413, response.StatusCode);
	}
}
