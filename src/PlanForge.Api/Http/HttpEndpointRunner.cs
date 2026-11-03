namespace PlanForge.Api.Http;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PlanForge.Application.Common;
using System.Net;
using System.Text.Json;

public sealed class HttpEndpointRunner(ILogger<HttpEndpointRunner>? logger = null)
{
	private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
	private readonly RequestBudget requestBudget = new(TimeProvider.System);

	public void AcquireBudget(string key, int limit, TimeSpan window) => requestBudget.Acquire(key, limit, window);

	/// <summary>Runs an application action with safe HTTP error translation and correlation.</summary>
	public async Task<HttpResponseData> ExecuteAsync(HttpRequestData request, Func<Task<object?>> action, int successStatus = 200)
	{
		string correlationId = Guid.NewGuid().ToString("N");
		try
		{
			object? result = await action();
			return await WriteAsync(request, successStatus, result, correlationId);
		}
		catch (PlanForgeException exception)
		{
			return await WriteProblemAsync(request, exception.StatusCode, exception.Message, correlationId);
		}
		catch (JsonException)
		{
			return await WriteProblemAsync(request, 400, "Provide a valid JSON request body.", correlationId);
		}
		catch (Exception exception)
		{
			logger?.LogError("Request failed. FailureType={FailureType} CorrelationId={CorrelationId}", exception.GetType().Name, correlationId);
			return await WriteProblemAsync(request, 500, "The request could not be completed. Try again and quote the correlation ID if the problem persists.", correlationId);
		}
	}

	/// <summary>Reads a bounded JSON payload without trusting caller-supplied length headers.</summary>
	public async Task<T> ReadJsonAsync<T>(HttpRequestData request)
	{
		using var payload = new MemoryStream();
		var buffer = new byte[4096];
		int count;
		while ((count = await request.Body.ReadAsync(buffer, request.FunctionContext.CancellationToken)) > 0)
		{
			if (payload.Length + count > 65_536)
				throw new PlanForgeException(413, "The JSON request exceeds the 64 KB limit.");
			await payload.WriteAsync(buffer.AsMemory(0, count), request.FunctionContext.CancellationToken);
		}
		payload.Position = 0;
		return await JsonSerializer.DeserializeAsync<T>(payload, jsonOptions, request.FunctionContext.CancellationToken)
			?? throw new JsonException("A JSON body is required.");
	}

	private static async Task<HttpResponseData> WriteAsync(HttpRequestData request, int status, object? result, string correlationId)
	{
		HttpResponseData response = request.CreateResponse((HttpStatusCode)status);
		response.Headers.Add("Cache-Control", "no-store");
		response.Headers.Add("Content-Type", status >= 400 ? "application/problem+json; charset=utf-8" : "application/json; charset=utf-8");
		response.Headers.Add("X-Content-Type-Options", "nosniff");
		response.Headers.Add("X-Correlation-ID", correlationId);
		if (status == 429)
			response.Headers.Add("Retry-After", "60");
		if (status != 204)
			await JsonSerializer.SerializeAsync(response.Body, result, jsonOptions, request.FunctionContext.CancellationToken);
		return response;
	}

	private static Task<HttpResponseData> WriteProblemAsync(HttpRequestData request, int status, string detail, string correlationId)
		=> WriteAsync(request, status, new { type = "about:blank", title = "Request failed", status, detail, correlationId }, correlationId);
}
