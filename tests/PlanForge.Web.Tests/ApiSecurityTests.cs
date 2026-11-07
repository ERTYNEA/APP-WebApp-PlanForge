namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Api;

public sealed class ApiSecurityTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not a URI")]
	[InlineData("https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net/?query=1")]
	[InlineData("https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net/#fragment")]
	public void ApiConfiguration_WhenMissingOrMalformed_RejectsWithoutEchoingAddress(string? address)
	{
		var failure = Assert.Throws<InvalidOperationException>(() => new PlanForge.Web.Security.ApiEndpointPolicy(address, false));
		Assert.Equal("The configured PlanForge API origin is invalid.", failure.Message);
	}

	[Theory]
	[InlineData("https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net", false)]
	[InlineData("https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net:443/", false)]
	[InlineData("http://localhost:7601", true)]
	[InlineData("https://127.0.0.1:7601", true)]
	[InlineData("http://[::1]:7601", true)]
	[InlineData(null, true)]
	public void ApiConfiguration_WhenExpected_AdmitsOnlyResolvedApiPrefix(string? address, bool development)
	{
		var policy = new PlanForge.Web.Security.ApiEndpointPolicy(address, development);
		Assert.True(policy.Allows(new Uri(policy.BaseUri, "agents?skip=0&take=10")));
		Assert.False(policy.Allows(new Uri(policy.BaseUri, "../../private")));
		Assert.False(policy.Allows(new Uri(policy.BaseUri, "agents#fragment")));
	}

	[Fact]
	public async Task ApiResponse_WhenRedirected_StopsWithoutFollowingLocationOrLeakingDetailsAsync()
	{
		var recorder = new RedirectHandler();
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production").UseSetting("PlanForgeApi:BaseUrl", "https://" + PlanForge.Web.Security.ApiEndpointPolicy.ProductionHost).ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => recorder)));
		using var scope = app.Services.CreateScope();
		var transport = scope.ServiceProvider.GetRequiredService<ApiTransport>();
		var failure = await Assert.ThrowsAsync<ApiException>(() => transport.SendAsync<object>(HttpMethod.Get, "agents", "test-only-redirect-credential"));
		Assert.Equal(302, failure.StatusCode);
		Assert.Equal(1, recorder.Calls);
		Assert.Equal("Bearer test-only-redirect-credential", recorder.Authorization);
		Assert.DoesNotContain("redirect-credential", failure.Message);
		Assert.DoesNotContain("unexpected.example", failure.Message);
	}

	private sealed class RedirectHandler : HttpMessageHandler
	{
		public int Calls { get; private set; }
		public string? Authorization { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Assert.Equal(PlanForge.Web.Security.ApiEndpointPolicy.ProductionHost, request.RequestUri!.Host);
			Authorization = request.Headers.Authorization?.ToString();
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.Found) { Content = new StringContent("Untrusted redirect response body") };
			response.Headers.Location = new Uri("https://unexpected.example/collect");
			return Task.FromResult(response);
		}
	}

	[Fact]
	public async Task ApiHandler_WhenConfigured_DisablesAutomaticRedirectsAndSharedCookiesAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		var factory = app.Services.GetRequiredService<IHttpMessageHandlerFactory>();
		HttpMessageHandler handler = factory.CreateHandler("PlanForgeApi");
		while (handler is DelegatingHandler delegating)
			handler = delegating.InnerHandler!;
		bool redirects = handler is HttpClientHandler client ? client.AllowAutoRedirect : Assert.IsType<SocketsHttpHandler>(handler).AllowAutoRedirect;
		bool cookies = handler is HttpClientHandler http ? http.UseCookies : Assert.IsType<SocketsHttpHandler>(handler).UseCookies;
		Assert.False(redirects);
		Assert.False(cookies);
	}

	[Theory]
	[InlineData("https://unexpected.example/api/v1/agents")]
	[InlineData("https://localhost:7601/api/v1/agents")]
	[InlineData("https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net:444/api/v1/agents")]
	[InlineData("../private")]
	public async Task ApiRequest_WhenDestinationEscapesAllowedOrigin_RejectsBeforeSendingCredentialsAsync(string path)
	{
		bool sent = false;
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Production").UseSetting("PlanForgeApi:BaseUrl", "https://" + PlanForge.Web.Security.ApiEndpointPolicy.ProductionHost)
			.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(() => sent = true))));
		using var scope = app.Services.CreateScope();
		var transport = scope.ServiceProvider.GetRequiredService<ApiTransport>();

		await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync<object>(HttpMethod.Get, path, "test-only-credential"));

		Assert.False(sent);
	}

	private sealed class RecordingHandler(Action onSend) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			onSend();
			return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") });
		}
	}

	[Theory]
	[InlineData("Production", "https://unexpected.example")]
	[InlineData("Production", "https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net.attacker.example")]
	[InlineData("Production", "https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net:444")]
	[InlineData("Production", "http://127.0.0.1:7601")]
	[InlineData("Production", "https://localhost:7601")]
	[InlineData("Production", "https://user:password@planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net")]
	[InlineData("Production", "https://planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net/unexpected/")]
	[InlineData("Development", "http://127.0.0.1:7777")]
	public async Task ApiConfiguration_WhenUnexpected_FailsClosedBeforeAnyRequestAsync(string environment, string address)
	{
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment).UseSetting("PlanForgeApi:BaseUrl", address));
		Assert.Throws<InvalidOperationException>(() => app.Services);
	}
}
