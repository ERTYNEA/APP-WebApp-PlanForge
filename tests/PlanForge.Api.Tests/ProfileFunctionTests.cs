namespace PlanForge.Api.Tests;

using PlanForge.Api.Accounts;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Contracts.Accounts;
using System.Net;

public sealed class ProfileFunctionTests
{
	[Fact]
	public async Task UserAvatarEndpoint_WhenRasterUploaded_RoundTripsBytesAndEnforcesHttpPayloadLimitAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var functions = new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner);
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		HttpTestRequest Upload(byte[] bytes)
		{
			var request = new HttpTestRequest(method: "PUT", bytes: bytes);
			request.Headers.Add("Authorization", "Bearer " + grant.Token);
			request.Headers.Add("Content-Type", "image/png");
			return request;
		}
		Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await functions.AvatarAsync(Upload(new byte[AvatarValidator.MaximumBytes + 1]))).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.AvatarAsync(Upload(image))).StatusCode);
		var response = await functions.AvatarAsync(Request(grant.Token, method: "GET"));
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(image, ((MemoryStream)response.Body).ToArray());
		Assert.Equal("image/png", response.Headers.GetValues("Content-Type").Single());
		Assert.Equal(HttpStatusCode.OK, (await functions.ProfileAsync(Request(grant.Token, method: "GET"))).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.AvatarAsync(Request(grant.Token, method: "DELETE"))).StatusCode);
	}

	[Fact]
	public async Task ProfileEndpoints_WhenHuman_UpdateNameAvatarAndPasswordWithImmediateSessionRevocationAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var functions = new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner);
		Assert.Equal(HttpStatusCode.OK, (await functions.ProfileAsync(Request(grant.Token, """{"displayName":"Changed"}""", "PUT"))).StatusCode);
		var avatar = Request(grant.Token, "not an image", "PUT");
		avatar.Headers.Add("Content-Type", "image/png");
		Assert.Equal(HttpStatusCode.BadRequest, (await functions.AvatarAsync(avatar)).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AvatarAsync(Request(grant.Token, method: "GET"))).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.AvatarAsync(Request(grant.Token, method: "DELETE"))).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await functions.PasswordAsync(Request(grant.Token, """{"currentPassword":"Incorrect","newPassword":"NewPassword123!"}"""))).StatusCode);
		Assert.Equal(HttpStatusCode.NoContent, (await functions.PasswordAsync(Request(grant.Token, """{"currentPassword":"CorrectPassword123!","newPassword":"NewPassword123!"}"""))).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.ProfileAsync(Request(grant.Token, method: "GET"))).StatusCode);
		Assert.NotNull(await fixture.Service.LoginAsync(new("a@example.test", "NewPassword123!")));
	}

	[Fact]
	public async Task ProfileLanguage_WhenSaved_PersistsEnglishPerAccountAcrossFreshReadsAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("language@example.test", "CorrectPassword123!", "test"));
		var functions = new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner);
		var initial = await functions.ProfileAsync(Request(grant.Token, method: "GET"));
		initial.Body.Position = 0;
		using var first = await System.Text.Json.JsonDocument.ParseAsync(initial.Body);
		Assert.True(first.RootElement.TryGetProperty("language", out var language));
		Assert.Equal("en", language.GetString());
		Assert.Equal(HttpStatusCode.OK, (await functions.ProfileAsync(Request(grant.Token, """{"displayName":"Language saved","language":"en"}""", "PUT"))).StatusCode);
		fixture.Database.ChangeTracker.Clear();
		var readback = await functions.ProfileAsync(Request(grant.Token, method: "GET"));
		readback.Body.Position = 0;
		using var saved = await System.Text.Json.JsonDocument.ParseAsync(readback.Body);
		Assert.Equal("en", saved.RootElement.GetProperty("language").GetString());
		Assert.Equal("Language saved", saved.RootElement.GetProperty("displayName").GetString());
		Assert.Equal("en", typeof(PlanForge.Domain.Accounts.UserAccount).GetProperty("Language")!.GetValue(fixture.Database.Users.Single()));
	}

	[Theory]
	[InlineData("es")]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("unsupported-culture-value")]
	public async Task ProfileLanguage_WhenUnsupported_RejectsBeforeChangingProfileAsync(string? language)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("invalid-language@example.test", "CorrectPassword123!", "Original"));
		var functions = new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner);
		string body = System.Text.Json.JsonSerializer.Serialize(new { displayName = "Must not change", language });

		Assert.Equal(HttpStatusCode.BadRequest, (await functions.ProfileAsync(Request(grant.Token, body, "PUT"))).StatusCode);

		fixture.Database.ChangeTracker.Clear();
		Assert.Equal("Original", fixture.Database.Users.Single().DisplayName);
	}

	private static HttpTestRequest Request(string token, string body = "", string method = "POST")
	{
		var request = new HttpTestRequest(body, method);
		request.Headers.Add("Authorization", "Bearer " + token);
		return request;
	}
}
