namespace PlanForge.Web.Api;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class ApiTransport(IHttpClientFactory clients)
{
	public async Task<T> SendAsync<T>(HttpMethod method, string path, string? token = null, object? body = null, byte[]? bytes = null, string? contentType = null, IReadOnlyDictionary<string,string>? headers = null, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(method, path);
		if (token is not null)
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (bytes is not null)
		{
			request.Content = new ByteArrayContent(bytes);
			request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
		}
		else if (body is not null)
			request.Content = JsonContent.Create(body, body.GetType());
		if (headers is not null)
			foreach (var header in headers)
				request.Headers.Add(header.Key, header.Value);
		using HttpClient client = clients.CreateClient("PlanForgeApi");
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);
		if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
			return default!;
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new ApiException(502, "The service returned an empty response.");
	}

	public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
	{
		if (response.IsSuccessStatusCode)
			return;
		string detail = "The service could not complete this request. Please try again.";
		string? correlation = response.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.FirstOrDefault() : null;
		if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
		{
			try
			{
				using JsonDocument problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
				if (problem.RootElement.TryGetProperty("detail", out var value))
					detail = value.GetString() ?? detail;
			}
			catch (JsonException) { detail = "The service returned an invalid error response."; }
		}
		throw new ApiException((int)response.StatusCode, detail, correlation);
	}
}
