namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.State;
using System.Net;

public sealed class AvatarAgentSaveTests
{
	[Fact]
	public async Task SaveAgent_WhileAvatarUploadInFlightDisablesIndependentMutationControlsAsync()
	{
		Guid id = Guid.NewGuid();
		var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var context = new ComponentTestContext(request => request.Method == HttpMethod.Put ? response.Task : Task.FromResult(ComponentTestContext.Json(request.Method == HttpMethod.Post ? (object)new AgentProfile(id, "New", "", true, false) : request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? Array.Empty<AgentTokenProfile>() : Array.Empty<AgentProfile>())));
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("New");
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "test.png", contentType: "image/png"));
		Task saving = page.Find("#agent-form").SubmitAsync();
		try
		{
			page.WaitForAssertion(() => Assert.True(page.FindAll("button").Single(button => button.TextContent == "Upload avatar").HasAttribute("disabled")));
		}
		finally
		{
			response.SetResult(AvatarTestResponses.Json(new AvatarState(true, Guid.NewGuid())));
			await saving;
		}
	}

	[Fact]
	public async Task AcceptedAvatar_WhenListRefreshFailsStillUpdatesListAndNeverClaimsUploadUnconfirmedAsync()
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		bool accepted = false;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post)
				return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "New", "", true, false)));
			if (request.Method == HttpMethod.Put)
			{
				accepted = true;
				return Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, version)));
			}
			if (accepted && path.EndsWith("/agents", StringComparison.Ordinal))
				throw new HttpRequestException("List unavailable");
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : Array.Empty<AgentProfile>()));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("New");
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "test.png", contentType: "image/png"));
		await page.Find("#agent-form").SubmitAsync();
		Assert.DoesNotContain("not confirmed", page.Markup);
		var listImage = page.FindComponents<PlanForge.Web.Components.Shared.AvatarImage>().First().Instance;
		Assert.True(listImage.InitialHasAvatar);
		Assert.Equal(version, listImage.InitialVersion);
		Assert.Equal("Avatar updated", Assert.Single(context.Services.GetRequiredService<NotificationState>().Items).Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SaveAgent_WithPendingAvatarEmitsOnlyAvatarToastAndRetryNeverCreatesDuplicateAsync(bool failFirst)
	{
		Guid id = Guid.NewGuid(), version = Guid.NewGuid();
		int creates = 0, uploads = 0;
		bool uploaded = false;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post)
			{
				creates++;
				return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "New", "", true, false)));
			}
			if (request.Method == HttpMethod.Put && path.EndsWith("/avatar", StringComparison.Ordinal))
			{
				uploads++;
				if (failFirst && uploads == 1)
					return Task.FromResult(ComponentTestContext.Json(new { detail = "Rejected" }, HttpStatusCode.BadRequest));
				uploaded = true;
				return Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, version)));
			}
			if (request.Method == HttpMethod.Put)
				return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "New", "", true, uploaded)));
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : creates > 0 ? new[] { new AgentProfile(id, "New", "", true, uploaded) } : Array.Empty<AgentProfile>()));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("New");
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "test.png", contentType: "image/png"));
		await page.Find("#agent-form").SubmitAsync();
		if (failFirst)
		{
			Assert.Contains("The agent was saved, but its avatar upload was not confirmed.", page.Markup);
			Assert.Empty(context.Services.GetRequiredService<NotificationState>().Items);
			await page.Find("#agent-form").SubmitAsync();
		}
		Assert.Equal(1, creates);
		Assert.Equal(failFirst ? 2 : 1, uploads);
		Assert.True(uploaded);
		Assert.Empty(page.FindAll("dialog"));
		var toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal("Avatar updated", toast.Message);
		Assert.Equal(NotificationKind.Success, toast.Kind);
	}
}
