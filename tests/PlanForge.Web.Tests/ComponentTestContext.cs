namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Api;
using PlanForge.Web.Security;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

public sealed class ComponentTestContext : BunitContext
{
	public ComponentTestContext(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddLocalization(options => options.ResourcesPath = "Resources");
		Services.AddSingleton<AuthenticationStateProvider, TestAuthentication>();
		Services.AddHttpClient("PlanForgeApi", client => client.BaseAddress = new Uri("https://api.example.test/api/v1/")).ConfigurePrimaryHttpMessageHandler(() => new Responder(responder));
		Services.AddScoped<ApiTransport>();
		Services.AddScoped<PlanForgeClient>();
	}

	public static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value, value.GetType()) };

	private sealed class TestAuthentication : AuthenticationStateProvider
	{
		public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(WebSession.TokenClaim, "test-session"), new Claim(ClaimTypes.Name, "test")], "Test"))));
	}
	private sealed class Responder(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
	}
}
