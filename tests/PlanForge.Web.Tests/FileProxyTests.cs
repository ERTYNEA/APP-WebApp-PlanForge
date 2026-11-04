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
	[InlineData("resources","resources/{0}/download")]
	[InlineData("projects","projects/{0}/export")]
	[InlineData("csv","projects/{0}/work-plan/export")]
	[InlineData("agent-avatars","agents/{0}/avatar")]
	[InlineData("user-avatar","users/me/avatar")]
	public async Task Download_WhenAuthenticated_ProxiesAuthorizedBytesWithoutSendingBearerToBrowserAsync(string kind,string apiPath)
	{
		var handler = new FileApiHandler();
		await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddHttpClient("PlanForgeApi").ConfigurePrimaryHttpMessageHandler(() => handler)));
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		string html = await client.GetStringAsync("/login");
		string csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
		await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string> { ["email"]="test@example.test", ["password"]="TestOnlyPassword123!", ["__RequestVerificationToken"]=csrf }));
		Guid id = Guid.NewGuid();
		var response = await client.GetAsync($"/files/{kind}/{id}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(new byte[]{1,2,3,4}, await response.Content.ReadAsByteArrayAsync());
		Assert.Contains("attachment",response.Content.Headers.ContentDisposition!.ToString());
		Assert.Equal("/api/v1/"+string.Format(System.Globalization.CultureInfo.InvariantCulture,apiPath,id),handler.FilePath);
		Assert.Equal(FileApiHandler.Token,handler.FileToken);
		Assert.DoesNotContain(FileApiHandler.Token,response.ToString());
	}

	[Fact]
	public async Task Download_WhenAnonymous_ChallengesBeforeApiAsync()
	{
		await using var app = new WebApplicationFactory<Program>();
		using var client = app.CreateClient(new() { AllowAutoRedirect = false });
		Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync($"/files/resources/{Guid.NewGuid()}")).StatusCode);
	}

	private sealed class FileApiHandler : HttpMessageHandler
	{
		public static string Token => "pf_h_" + new string('b',64);
		public string? FilePath { get; private set; }
		public string? FileToken { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
		{
			if (!request.RequestUri!.AbsolutePath.Contains("/auth/",StringComparison.Ordinal))
			{
				FilePath=request.RequestUri.AbsolutePath;
				FileToken=request.Headers.Authorization?.Parameter;
				var result=new HttpResponseMessage(HttpStatusCode.OK) { Content=new ByteArrayContent([1,2,3,4]) };
				result.Content.Headers.ContentType=new("application/octet-stream");
				result.Content.Headers.ContentDisposition=new("attachment") { FileNameStar="test.bin" };
				return Task.FromResult(result);
			}
			var user=new UserProfile(Guid.NewGuid(),"test@example.test","test",DateTimeOffset.UtcNow);
			object body=request.RequestUri.AbsolutePath.EndsWith("auth/login",StringComparison.Ordinal) ? new AccessGrant(Token,DateTimeOffset.UtcNow.AddDays(7),user) : user;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(body,body.GetType()) });
		}
	}
}
