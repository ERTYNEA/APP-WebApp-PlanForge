namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;
using System.Net.Http.Json;

public sealed class AccountPageTests
{
	[Theory]
	[InlineData("display-name", "   ")]
	[InlineData("account-language", "unsupported")]
	public async Task Profile_InvalidFieldHasAssociatedLocalErrorAndDoesNotSendAsync(string field, string value)
	{
		int writes = 0;
		var profile = new UserProfile(Guid.NewGuid(), "test@example.test", "test", DateTimeOffset.UtcNow);
		using var context = new ComponentTestContext(request => { if (request.Method != HttpMethod.Get) writes++; return Task.FromResult(ComponentTestContext.Json(profile)); });
		var page = context.Render<Settings>();
		page.Find("#" + field).Change(value);
		await page.Find("#profile-form").SubmitAsync();
		Assert.Equal(0, writes);
		var control = page.Find("#" + field);
		Assert.Equal("true", control.GetAttribute("aria-invalid"));
		Assert.NotEmpty(control.GetAttribute("aria-describedby") ?? "");
		Assert.NotNull(page.Find("#" + control.GetAttribute("aria-describedby")));
	}

	[Fact]
	public async Task Save_WhenNameChanged_UpdatesOnlyDisplayNameAndKeepsSeparateAvatarAsync()
	{
		var profile = new UserProfile(Guid.NewGuid(), "test@example.test", "test", DateTimeOffset.UtcNow);
		UpdateProfileRequest? sent = null;
		using var context = new ComponentTestContext(async request => { if (request.Method == HttpMethod.Put) sent = await request.Content!.ReadFromJsonAsync<UpdateProfileRequest>(); return ComponentTestContext.Json(profile); });
		var page = context.Render<Settings>();
		Assert.Contains("test@example.test", page.Markup);
		Assert.Equal("users/me/avatar", page.FindComponent<AvatarEditor>().Instance.ApiPath);
		Assert.Contains("/account/password", page.Markup);
		Assert.Single(page.FindAll("#account-language option"));
		Assert.Equal("English", page.Find("#account-language option").TextContent);
		Assert.Equal("en", page.Find("#account-language option").GetAttribute("value"));
		page.Find("#display-name").Change("Updated display name");
		await page.Find("#profile-form").SubmitAsync();
		Assert.Equal("Updated display name", sent!.DisplayName);
	}
}
