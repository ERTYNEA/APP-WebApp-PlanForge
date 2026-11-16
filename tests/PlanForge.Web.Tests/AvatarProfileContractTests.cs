namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class AvatarProfileContractTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ProfileVersion_SeedsSurfaceBeforeFirstMetadataPollAsync(bool human)
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(
			request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal)
				? (object)Array.Empty<AgentTokenProfile>()
				: human ? new UserProfile(id, "qa@example.test", "Human", DateTimeOffset.UtcNow, true, AvatarVersion: version)
				: new[] { new AgentProfile(id, "Agent", "", true, true, AvatarVersion: version) })));
		if (human)
		{
			var page = context.Render<PlanForge.Web.Components.Pages.Settings>();
			Assert.Equal(version, page.FindComponent<AvatarImage>().Instance.InitialVersion);
			Assert.Equal($"/files/user-avatar/{id}?v={version:D}", page.Find("img[hidden]").GetAttribute("src"));
		}
		else
		{
			var page = context.Render<PlanForge.Web.Components.Pages.Agents>();
			Assert.Equal(version, page.FindComponent<AvatarImage>().Instance.InitialVersion);
			Assert.Equal($"/files/agent-avatars/{id}?v={version:D}", page.Find("img[hidden]").GetAttribute("src"));
			await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
			Assert.Equal(version, page.FindComponent<AvatarEditor>().FindComponent<AvatarImage>().Instance.InitialVersion);
		}
	}

	[Theory]
	[InlineData("users/me/avatar", "users/me")]
	[InlineData("agents/182065fe-353c-42f7-a285-c0e5b6402eaa/avatar", "agents/182065fe-353c-42f7-a285-c0e5b6402eaa")]
	public void Refresh_ReadsVersionFromExistingProfileEndpoint(string avatarPath, string profilePath)
	{
		Guid version = Guid.NewGuid();
		string? requestedPath = null;
		using var context = new ComponentTestContext(request =>
		{
			requestedPath = request.RequestUri!.AbsolutePath;
			Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
			object profile = profilePath == "users/me"
				? new UserProfile(Guid.NewGuid(), "qa@example.test", "Human", DateTimeOffset.UtcNow, true, AvatarVersion: version)
				: new AgentProfile(Guid.NewGuid(), "Agent", "", true, true, AvatarVersion: version);
			return Task.FromResult(ComponentTestContext.Json(profile));
		});
		context.JSInterop.Setup<bool>("planForge.isDocumentVisible").SetResult(true);
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, avatarPath).Add(component => component.ImageUrl, "/files/avatar"));
		image.WaitForAssertion(() => Assert.NotNull(requestedPath));
		Assert.Equal("/api/v1/" + profilePath, requestedPath);
		image.WaitForAssertion(() => Assert.Equal($"/files/avatar?v={version:D}", image.Find("img[hidden]").GetAttribute("src")));
		Assert.Empty(context.Services.GetRequiredService<NotificationState>().Items);
	}

	[Theory]
	[InlineData("users/me/avatar")]
	[InlineData("agents/182065fe-353c-42f7-a285-c0e5b6402eaa/avatar")]
	public async Task Upload_ProfileReceiptUsesAvatarVersionForPreloadedImageAsync(string path)
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		using var context = new ComponentTestContext(request =>
		{
			Assert.Equal(HttpMethod.Put, request.Method);
			Assert.Equal("/api/v1/" + path, request.RequestUri!.AbsolutePath);
			object profile = path == "users/me/avatar"
				? new UserProfile(id, "qa@example.test", "Human", DateTimeOffset.UtcNow, true, AvatarVersion: version)
				: new AgentProfile(id, "Agent", "", true, true, AvatarVersion: version);
			return Task.FromResult(ComponentTestContext.Json(profile));
		});
		var editor = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, path).Add(component => component.ImageUrl, "/files/avatar"));
		editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "avatar.png", contentType: "image/png"));
		await editor.Find("form").SubmitAsync();
		Assert.Equal($"/files/avatar?v={version:D}", editor.Find("img[hidden]").GetAttribute("src"));
		Assert.Equal("Avatar updated", Assert.Single(context.Services.GetRequiredService<NotificationState>().Items).Message);
	}
}
