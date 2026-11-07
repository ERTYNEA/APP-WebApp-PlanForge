namespace PlanForge.Api.Http;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PlanForge.Application.Common;
using PlanForge.Contracts;
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
		byte[] payload = await ReadBytesAsync(request, 65_536);
		return JsonSerializer.Deserialize<T>(payload, jsonOptions) ?? throw new JsonException("A JSON body is required.");
	}

	public async Task<byte[]> ReadBytesAsync(HttpRequestData request, int maximumBytes)
	{
		using var payload = new MemoryStream();
		var buffer = new byte[4096];
		int count;
		while ((count = await request.Body.ReadAsync(buffer, request.FunctionContext.CancellationToken)) > 0)
		{
			if (payload.Length + count > maximumBytes)
				throw new PlanForgeException(413, "The request body exceeds its allowed size limit.");
			await payload.WriteAsync(buffer.AsMemory(0, count), request.FunctionContext.CancellationToken);
		}
		return payload.ToArray();
	}

	private static async Task<HttpResponseData> WriteAsync(HttpRequestData request, int status, object? result, string correlationId)
	{
		HttpResponseData response = request.CreateResponse((HttpStatusCode)status);
		response.Headers.Add("Cache-Control", "no-store");
		response.Headers.Add("Content-Type", result is DownloadFile download ? download.ContentType : status >= 400 ? "application/problem+json; charset=utf-8" : "application/json; charset=utf-8");
		response.Headers.Add("X-Content-Type-Options", "nosniff");
		response.Headers.Add("X-Correlation-ID", correlationId);
		if (status == 429)
			response.Headers.Add("Retry-After", "60");
		if (result is DownloadFile file)
		{
			response.Headers.Add("Content-Disposition", (file.Inline ? "inline" : "attachment") + "; filename*=UTF-8''" + Uri.EscapeDataString(file.FileName));
			await response.Body.WriteAsync(file.Content, request.FunctionContext.CancellationToken);
		}
		else if (status != 204)
			await JsonSerializer.SerializeAsync(response.Body, result, jsonOptions, request.FunctionContext.CancellationToken);
		return response;
	}

	private static Task<HttpResponseData> WriteProblemAsync(HttpRequestData request, int status, string detail, string correlationId)
		=> WriteAsync(request, status, new { type = "about:blank", title = ApplicationText.Get("RequestFailedTitle"), status, detail = ApplicationText.Get(detail), correlationId }, correlationId);
}
