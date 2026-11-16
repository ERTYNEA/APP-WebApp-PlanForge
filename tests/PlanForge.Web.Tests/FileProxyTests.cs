namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

public sealed class FileProxyTests
{
	[Theory]
	[InlineData("resources", "resources/{0}/download")]
	[InlineData("projects", "projects/{0}/export")]
	[InlineData("csv", "projects/{0}/work-plan/export")]
	[InlineData("agent-avatars", "agents/{0}/avatar")]
	[InlineData("user-avatar", "users/me/avatar")]
	public async Task Download_WhenAuthenticated_ProxiesAuthorizedBytesWithoutSendingBearerToBrowserAsync(string kind, string apiPath)
	{
		var handler = new FileApiHandler();
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
		await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "test@example.test", ["password"] = "TestOnlyPassword123!", ["__RequestVerificationToken"] = csrf }));
		Guid id = Guid.NewGuid();
		var response = await client.GetAsync($"/files/{kind}/{id}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, await response.Content.ReadAsByteArrayAsync());
		Assert.Contains("attachment", response.Content.Headers.ContentDisposition!.ToString());
		Assert.Equal("/api/v1/" + string.Format(System.Globalization.CultureInfo.InvariantCulture, apiPath, id), handler.FilePath);
		Assert.Equal(FileApiHandler.Token, handler.FileToken);
		Assert.DoesNotContain(FileApiHandler.Token, response.ToString());
	}

	[Theory]
	[InlineData("csv", "9007199254740993", true)]
	[InlineData("projects", "9223372036854775807", true)]
	[InlineData("resources", "17", false)]
	[InlineData("csv", "not-an-event", false)]
	[InlineData("csv", "0", false)]
	[InlineData("csv", "17", false, true)]
	public async Task ExportReceipt_ForwardsOnlyValidExactEventIdentityAsync(string kind, string receipt, bool forwarded, bool repeated = false)
	{
		var handler = new FileApiHandler(receipt, repeated);
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
		await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "test@example.test", ["password"] = "TestOnlyPassword123!", ["__RequestVerificationToken"] = csrf }));
		using HttpResponseMessage response = await client.GetAsync($"/files/{kind}/{Guid.NewGuid()}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(forwarded, response.Headers.Contains("X-PlanForge-Change-Id"));
		if (forwarded)
			Assert.Equal(receipt, response.Headers.GetValues("X-PlanForge-Change-Id").Single());
	}

	[Theory]
	[InlineData("user-avatar")]
	[InlineData("agent-avatars")]
	public async Task AvatarProxy_PreservesVersionAndEtagWithPrivateNoStoreAsync(string kind)
	{
		var handler = new FileApiHandler(avatar: true);
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
		await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "test@example.test", ["password"] = "TestOnlyPassword123!", ["__RequestVerificationToken"] = csrf }));
		Guid version = Guid.NewGuid();
		using HttpResponseMessage response = await client.GetAsync($"/files/{kind}/{Guid.NewGuid()}?v={version:D}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("\"avatar-generation\"", response.Headers.ETag?.Tag);
		Assert.True(response.Headers.CacheControl?.NoStore);
		Assert.Equal($"?v={version:D}", handler.FileQuery);
		Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
	}

	[Fact]
	public async Task Download_WhenAnonymous_ChallengesBeforeApiAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/files/resources/{Guid.NewGuid()}")).StatusCode);
	}

	private sealed class FileApiHandler(string? receipt = null, bool repeated = false, bool avatar = false) : HttpMessageHandler
	{
		public static string Token => "pf_h_" + new string('b', 64);
		public string? FilePath { get; private set; }
		public string? FileQuery { get; private set; }
		public string? FileToken { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (!request.RequestUri!.AbsolutePath.Contains("/auth/", StringComparison.Ordinal))
			{
				FilePath = request.RequestUri.AbsolutePath;
				FileQuery = request.RequestUri.Query;
				FileToken = request.Headers.Authorization?.Parameter;
				var result = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) };
				if (receipt is not null) result.Headers.Add("X-PlanForge-Change-Id", repeated ? [receipt, receipt] : [receipt]);
				result.Content.Headers.ContentType = new("application/octet-stream");
				if (avatar)
				{
					result.Headers.ETag = new("\"avatar-generation\"");
					result.Headers.CacheControl = new() { NoStore = true };
					result.Content.Headers.ContentType = new("image/png");
				}
				result.Content.Headers.ContentDisposition = new("attachment") { FileNameStar = "test.bin" };
				return Task.FromResult(result);
			}
			var user = new UserProfile(Guid.NewGuid(), "test@example.test", "test", DateTimeOffset.UtcNow);
			object body = request.RequestUri.AbsolutePath.EndsWith("auth/login", StringComparison.Ordinal) ? new AccessGrant(Token, DateTimeOffset.UtcNow.AddDays(7), user) : user;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
		}
	}
}
