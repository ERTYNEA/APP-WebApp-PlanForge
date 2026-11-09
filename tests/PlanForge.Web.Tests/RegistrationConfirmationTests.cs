namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class RegistrationConfirmationTests
{
	[Fact]
	public async Task RegistrationHelp_BelongsToPasswordAndConfirmationUsesTheSameLabelStructureAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient();
		var parser = new AngleSharp.Html.Parser.HtmlParser();
		var register = parser.ParseDocument(await client.GetStringAsync("/register"));
		var password = register.QuerySelector("input[name=password]")!;
		var help = register.QuerySelector("#password-requirements");
		Assert.NotNull(help);
		Assert.Equal("Use 12–128 characters.", help.TextContent);
		Assert.Equal("password-requirements", password.GetAttribute("aria-describedby"));
		Assert.Same(password.Closest("label"), help.Closest("label"));
		var confirmation = register.QuerySelector("#confirm-password")!;
		Assert.NotNull(confirmation.Closest("label"));
		Assert.StartsWith("Confirm password", confirmation.Closest("label")!.TextContent);
		Assert.Single(register.QuerySelectorAll("input[name=confirmPassword]"));
		Assert.DoesNotContain("No external identity provider", register.DocumentElement.TextContent);
		var login = parser.ParseDocument(await client.GetStringAsync("/login"));
		Assert.Null(login.QuerySelector("#password-requirements, input[name=confirmPassword]"));
		Assert.Equal("current-password", login.QuerySelector("input[name=password]")!.GetAttribute("autocomplete"));
	}

	[Fact]
	public async Task Register_ExactConfirmationForwardsOnlyOriginalPasswordAndPreservesNativeConstraintsAsync()
	{
		var handler = new RegistrationHandler();
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/register");
		var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
		var confirmation = document.QuerySelector("#confirm-password")!;
		Assert.Equal("password", confirmation.GetAttribute("type"));
		Assert.True(confirmation.HasAttribute("required"));
		Assert.Equal(2, document.QuerySelectorAll("input[autocomplete=new-password]").Length);
		Assert.False(document.QuerySelector("form")!.HasAttribute("novalidate"));
		Assert.False(document.QuerySelector("form")!.HasAttribute("onsubmit"));
		string csrf = document.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
		var response = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["email"] = "fixture@example.test",
			["displayName"] = "Fixture",
			["password"] = "CorrectPassword123!",
			["confirmPassword"] = "CorrectPassword123!",
			["__RequestVerificationToken"] = csrf
		}));
		Assert.Equal("/", response.Headers.Location?.ToString());
		Assert.Equal(1, handler.Calls);
		using JsonDocument payload = JsonDocument.Parse(handler.Body!);
		Assert.Equal("CorrectPassword123!", payload.RootElement.GetProperty("password").GetString());
		Assert.False(payload.RootElement.TryGetProperty("confirmPassword", out _));
		Assert.Equal(3, payload.RootElement.EnumerateObject().Count());
	}

	[Theory]
	[InlineData("")]
	[InlineData("DifferentPassword123!")]
	[InlineData("CorrectPassword123! ")]
	public async Task Register_MissingOrNonexactConfirmationNeverCallsApiOrIssuesSessionAsync(string confirmation)
	{
		var handler = new RegistrationHandler();
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/register");
		string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
		var response = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["email"] = "fixture@example.test",
			["displayName"] = "Fixture",
			["password"] = "CorrectPassword123!",
			["confirmPassword"] = confirmation,
			["__RequestVerificationToken"] = csrf
		}));
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/register?passwordMismatch=true", response.Headers.Location?.ToString());
		Assert.Equal(0, handler.Calls);
		Assert.False(response.Headers.Contains("Set-Cookie"));
		string result = await client.GetStringAsync(response.Headers.Location);
		Assert.Contains("Passwords do not match.", result);
		Assert.Contains("aria-invalid=\"true\"", result);
	}

	private sealed class RegistrationHandler : HttpMessageHandler
	{
		public int Calls { get; private set; }
		public string? Body { get; private set; }
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Body = await request.Content!.ReadAsStringAsync(cancellationToken);
			return ComponentTestContext.Json(new AccessGrant("test-only-session", DateTimeOffset.UtcNow.AddDays(7), new UserProfile(Guid.NewGuid(), "fixture@example.test", "Fixture", DateTimeOffset.UtcNow)));
		}
	}
}
