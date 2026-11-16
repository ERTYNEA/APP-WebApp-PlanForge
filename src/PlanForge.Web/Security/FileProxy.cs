namespace PlanForge.Web.Security;

using PlanForge.Web.Api;
using System.Net.Http.Headers;

public static class FileProxy
{
	public static void MapFileEndpoints(WebApplication app)
	{
		app.MapGet("/files/projects/{id:guid}", (Guid id, HttpContext context, IHttpClientFactory clients) => DownloadAsync(context, clients, $"projects/{id}/export")).RequireAuthorization();
		app.MapGet("/files/csv/{id:guid}", (Guid id, HttpContext context, IHttpClientFactory clients) => DownloadAsync(context, clients, $"projects/{id}/work-plan/export")).RequireAuthorization();
		app.MapGet("/files/agent-avatars/{id:guid}", (Guid id, HttpContext context, IHttpClientFactory clients) => DownloadAsync(context, clients, $"agents/{id}/avatar")).RequireAuthorization();
		app.MapGet("/files/user-avatar/{id:guid}", (HttpContext context, IHttpClientFactory clients) => DownloadAsync(context, clients, "users/me/avatar")).RequireAuthorization();
		app.MapGet("/files/resources/{id:guid}", (Guid id, HttpContext context, IHttpClientFactory clients) => DownloadAsync(context, clients, $"resources/{id}/download")).RequireAuthorization();
	}

	private static async Task DownloadAsync(HttpContext context, IHttpClientFactory clients, string path)
	{
		string? token = context.User.FindFirst(WebSession.TokenClaim)?.Value;
		if (token is null)
		{
			context.Response.StatusCode = 401;
			return;
		}
		using HttpClient client = clients.CreateClient("PlanForgeApi");
		bool avatar = path.EndsWith("/avatar", StringComparison.Ordinal);
		string version = context.Request.Query["v"].ToString();
		if (avatar && Guid.TryParse(version, out _))
			path += "?v=" + Uri.EscapeDataString(version);
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		try
		{
			using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
			await ApiTransport.EnsureSuccessAsync(response, context.RequestAborted);
			if (avatar)
			{
				context.Response.Headers.CacheControl = "no-store";
				if (response.Headers.ETag is not null)
					context.Response.Headers.ETag = response.Headers.ETag.ToString();
			}
			if (path.EndsWith("/export", StringComparison.Ordinal) && response.Headers.TryGetValues("X-PlanForge-Change-Id", out var values))
			{
				string[] receipts = values.Take(2).ToArray();
				string? receipt = receipts.Length == 1 ? receipts[0] : null;
				if (long.TryParse(receipt, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long eventId) && eventId > 0)
					context.Response.Headers["X-PlanForge-Change-Id"] = receipt;
			}
			context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
			if (response.Content.Headers.ContentDisposition is not null)
				context.Response.Headers.ContentDisposition = response.Content.Headers.ContentDisposition.ToString();
			await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
		}
		catch (ApiException exception)
		{
			context.Response.StatusCode = exception.StatusCode;
			await context.Response.WriteAsJsonAsync(new { detail = exception.Message, correlationId = exception.CorrelationId }, context.RequestAborted);
		}
	}
}
