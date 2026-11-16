namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Layout;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;

public sealed class AvatarSurfaceTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Inspector_UsesLiveVersionedImageWithoutAddingAnInitialOnlyBadge(bool hasAvatar)
	{
		Guid agentId = Guid.NewGuid(), version = Guid.NewGuid();
		var phase = new PlanForge.Contracts.Planning.PlanItem(Guid.NewGuid(), "Phase", null, 0, "Phase", AssignmentMode: "Override", AssignedAgentId: agentId);
		var snapshot = new PlanForge.Contracts.Planning.ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [phase]);
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(hasAvatar, version))));
		var inspector = context.Render<PlanForge.Web.Components.Planning.WorkPlanInspector>(parameters => parameters.Add(component => component.Snapshot, snapshot).Add(component => component.Item, phase).Add(component => component.Agents, new[] { new AgentProfile(agentId, "NEA", "", true, hasAvatar, version) }));
		var image = inspector.FindComponent<AvatarImage>().Instance;
		Assert.Equal($"agents/{agentId}/avatar", image.ApiPath);
		Assert.Equal(version, image.InitialVersion);
		Assert.Equal(hasAvatar, image.InitialHasAvatar);
		Assert.Equal("NEA", inspector.Find(".agent-tag").TextContent.Trim());
	}

	[Fact]
	public async Task Settings_AcceptedVersionAndUnsavedNameSurviveProfileRefreshAsync()
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		bool accepted = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Put)
			{
				accepted = true;
				return Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, version)));
			}
			return Task.FromResult(ComponentTestContext.Json(new UserProfile(id, "qa@example.test", "Human", DateTimeOffset.UtcNow, accepted)));
		});
		var page = context.Render<Settings>();
		page.Find("#display-name").Change("Unsaved draft");
		var editor = page.FindComponent<AvatarEditor>();
		editor.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "avatar.png", contentType: "image/png"));
		await editor.Find("form").SubmitAsync();
		Assert.Equal(version, editor.FindComponent<AvatarImage>().Instance.InitialVersion);
		Assert.Equal("Unsaved draft", page.Find("#display-name").GetAttribute("value"));
	}

	[Fact]
	public void Settings_UsesPersistentAvatarMetadataAndDisplayName()
	{
		Guid id = Guid.NewGuid();
		using var context = new ComponentTestContext(_ => Task.FromResult(ComponentTestContext.Json(new UserProfile(id, "qa@example.test", "Human", DateTimeOffset.UtcNow, true))));
		var page = context.Render<Settings>();
		var image = page.FindComponent<AvatarImage>().Instance;
		Assert.Equal("users/me/avatar", image.ApiPath);
		Assert.Equal($"/files/user-avatar/{id}", image.ImageUrl);
		Assert.Equal("Human", image.Name);
		Assert.True(image.InitialHasAvatar);
	}

	[Fact]
	public void Sidebar_UsesHumanMetadataAndAuthenticatedFileRoute()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.Empty))));
		Guid userId = Guid.NewGuid();
		var authorization = context.AddAuthorization();
		authorization.SetAuthorized("test");
		authorization.SetClaims(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()));
		var layout = context.Render<MainLayout>();
		var image = layout.FindComponent<AvatarImage>().Instance;
		Assert.Equal("users/me/avatar", image.ApiPath);
		Assert.Equal($"/files/user-avatar/{userId}", image.ImageUrl);
		Assert.Equal("test", image.Name);
	}

	[Fact]
	public async Task AgentListAndEditor_UseSamePersistentAvatarIdentityAsync()
	{
		Guid id = Guid.NewGuid();
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "NEA", "", true, true) })));
		var page = context.Render<Agents>();
		var listImage = page.FindComponent<AvatarImage>().Instance;
		Assert.Equal($"agents/{id}/avatar", listImage.ApiPath);
		Assert.Equal("NEA", listImage.Name);
		await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
		var editorImage = page.FindComponent<AvatarEditor>().FindComponent<AvatarImage>().Instance;
		Assert.Equal(listImage.ApiPath, editorImage.ApiPath);
		Assert.Equal(listImage.ImageUrl, editorImage.ImageUrl);
		Assert.Equal("NEA", editorImage.Name);
	}
}
