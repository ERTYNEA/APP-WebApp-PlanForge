namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

public sealed class AccountCultureTests
{
	[Fact]
	public async Task Culture_WhenBrowserRequestsSpanish_UsesOnlySupportedAccountPreferenceAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		var options = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
		Assert.Equal("en", options.DefaultRequestCulture.Culture.Name);
		Assert.Equal("en", Assert.Single(options.SupportedUICultures!).Name);
		var context = new DefaultHttpContext();
		context.Request.Headers.AcceptLanguage = "es-ES";
		context.Request.QueryString = new QueryString("?culture=es&ui-culture=es");
		context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("planforge:language", "en")], "Test"));
		var result = await Assert.Single(options.RequestCultureProviders).DetermineProviderCultureResult(context);
		Assert.NotNull(result);
		Assert.Equal("en", Assert.Single(result.UICultures).Value);
	}
}
