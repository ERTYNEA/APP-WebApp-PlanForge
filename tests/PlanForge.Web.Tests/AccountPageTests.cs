namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;
using System.Net.Http.Json;

public sealed class AccountPageTests
{
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
		page.Find("#display-name").Change("Updated display name");
		await page.Find("#profile-form").SubmitAsync();
		Assert.Equal("Updated display name", sent!.DisplayName);
	}
}
