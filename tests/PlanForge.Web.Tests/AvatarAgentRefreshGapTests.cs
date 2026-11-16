namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.State;

public sealed class AvatarAgentRefreshGapTests
{
	[Fact]
	public async Task OptionalAvatar_WhenAcceptedButParentReloadFailsKeepsItsWarningVisibleAsync()
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		bool accepted = false;
		int creates = 0;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post)
			{
				creates++;
				return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "New", "", true, false)));
			}
			if (request.Method == HttpMethod.Put)
			{
				accepted = true;
				return Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, version)));
			}
			if (accepted && path.EndsWith("/agents", StringComparison.Ordinal))
				throw new HttpRequestException("Parent list refresh unavailable.");
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : Array.Empty<AgentProfile>()));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("New");
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1], "avatar.png", contentType: "image/png"));
		await page.Find("#agent-form").SubmitAsync();

		Assert.True(accepted);
		Assert.Equal(1, creates);
		var toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal("Avatar updated", toast.Message);
		Assert.Equal(NotificationKind.Success, toast.Kind);
		var warning = Assert.Single(page.FindAll("[role=alert]"));
		Assert.Contains(UiText.Get("Your changes were confirmed, but the view could not be refreshed. Reload to see the latest data."), warning.TextContent);
		Assert.NotNull(warning.Closest("dialog .avatar-editor"));
		Assert.DoesNotContain("not confirmed", warning.TextContent);
		Assert.False(page.FindAll("button").Single(button => button.TextContent == "Save agent").HasAttribute("disabled"));
		await page.Find("button[aria-label='Close dialog']").ClickAsync(new());
		Assert.Empty(page.FindAll("[role=alert]"));
	}
}
