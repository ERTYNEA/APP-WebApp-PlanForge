namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

public sealed class PasswordFlowTests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PasswordPost_WhenVerified_RequiresCsrfAndClearsCookieOnlyOnSuccessAsync(bool valid)
	{
		var handler = new PasswordApiHandler(valid);
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = GetCsrf(html);
		await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "test@example.test", ["password"] = "TestOnlyPassword123!", ["__RequestVerificationToken"] = csrf }));
		html = await client.GetStringAsync("/settings");
		csrf = GetCsrf(html);
		Assert.NotEmpty(csrf);
		var missing = await client.PostAsync("/account/password", new FormUrlEncodedContent(new Dictionary<string, string> { ["currentPassword"] = "NotSent" }));
		Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
		Assert.Null(handler.Password);
		var response = await client.PostAsync("/account/password", new FormUrlEncodedContent(new Dictionary<string, string> { ["currentPassword"] = "OldPrivateValue", ["newPassword"] = "NewPrivateValue123!", ["confirmPassword"] = "NewPrivateValue123!", ["__RequestVerificationToken"] = csrf }));
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("OldPrivateValue", handler.Password?.CurrentPassword);
		Assert.Equal("NewPrivateValue123!", handler.Password?.NewPassword);
		Assert.StartsWith(valid ? "/login" : "/settings?error=", response.Headers.Location!.ToString());
		if (valid) Assert.Contains("expires=Thu, 01 Jan 1970", string.Join(";", response.Headers.GetValues("Set-Cookie")));
		Assert.DoesNotContain("PrivateValue", response.Headers.Location.ToString());
	}
	private static string GetCsrf(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
	private sealed class PasswordApiHandler(bool valid) : HttpMessageHandler
	{
		public ChangePasswordRequest? Password { get; private set; }
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.RequestUri!.AbsolutePath.EndsWith("/password", StringComparison.Ordinal))
			{
				Password = await request.Content!.ReadFromJsonAsync<ChangePasswordRequest>(cancellationToken);
				return valid ? new(HttpStatusCode.NoContent) : ComponentTestContext.Json(new { detail = "The current password is incorrect." }, HttpStatusCode.BadRequest);
			}
			var profile = new UserProfile(Guid.NewGuid(), "test@example.test", "test", DateTimeOffset.UtcNow);
			return ComponentTestContext.Json(request.RequestUri.AbsolutePath.EndsWith("/login", StringComparison.Ordinal) ? (object)new AccessGrant("pf_h_" + new string('d', 64), DateTimeOffset.UtcNow.AddDays(7), profile) : profile);
		}
	}
}
