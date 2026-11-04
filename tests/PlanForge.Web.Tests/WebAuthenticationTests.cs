namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

public sealed class WebAuthenticationTests
{
	[Fact]
	public async Task LoginPost_WhenValid_UsesProtectedHttpOnlyCookieWithoutExposingApiTokenAsync()
	{
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => new LoginApiHandler())));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
		Assert.NotEmpty(csrf);
		var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string> { ["email"]="test@example.test", ["password"]="TestOnlyPassword123!", ["__RequestVerificationToken"]=WebUtility.HtmlDecode(csrf) }));
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		string cookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
		Assert.Contains("httponly", cookie.ToLowerInvariant());
		Assert.DoesNotContain(LoginApiHandler.Token, cookie);
		string home = await client.GetStringAsync("/");
		Assert.Contains("No projects yet", home);
		Assert.Contains("New project", home);
		Assert.Contains("Search projects", home);
		Assert.DoesNotContain(LoginApiHandler.Token, home);
		Assert.DoesNotContain("v@ProductVersion",home);
		Assert.Contains("v0.1.0-alpha.5",home);
	}

	private sealed class LoginApiHandler : HttpMessageHandler
	{
		public static string Token => "pf_h_" + new string('a',64);
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var user = new UserProfile(Guid.Parse("11111111-1111-1111-1111-111111111111"), "test@example.test", "test", DateTimeOffset.UtcNow);
			object body = request.RequestUri!.AbsolutePath.EndsWith("auth/login") ? new AccessGrant(Token,DateTimeOffset.UtcNow.AddDays(7),user) : request.RequestUri.AbsolutePath.EndsWith("auth/session") ? user : request.RequestUri.AbsolutePath.EndsWith("agents") ? Array.Empty<PlanForge.Contracts.Agents.AgentProfile>() : new { items = Array.Empty<object>(), total=0, skip=0, take=50, hasMore=false };
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
		}
	}

	[Fact]
	public async Task Login_WhenOpened_RendersRealAntiforgeryFormAndCentralVersionAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		var response = await client.GetAsync("/login");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		string html = await response.Content.ReadAsStringAsync();
		Assert.Contains("__RequestVerificationToken", html);
		Assert.Contains("/account/login", html);
		Assert.Contains("type=\"password\"", html);
		Assert.Contains("0.1.0-alpha.5", html);
	}

	[Fact]
	public async Task ProtectedRoutes_WhenAnonymous_ChallengeWithoutRenderingPrivateDataAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		var response = await client.GetAsync("/");
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.StartsWith("http://localhost/login", response.Headers.Location!.ToString());
	}

	[Fact]
	public async Task LoginPost_WhenMissingAntiforgery_RejectsBeforeCallingApiAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string> { ["email"]="test@example.test", ["password"]="NeverSentPassword123!" }));
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}
}
