namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class AvatarLifecycleTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public void Controls_ShowDestructiveLeftAndQuietUploadRightOnlyWhenNotDeferred(bool deferred, bool hasAvatar)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(hasAvatar, Guid.NewGuid()))));
		var editor = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.Deferred, deferred).Add(component => component.HasAvatar, hasAvatar));
		Assert.Contains("maximum 4 MiB", editor.Find("label").TextContent);
		var buttons = editor.FindAll(".form-actions button");
		Assert.Equal(deferred ? 0 : hasAvatar ? 2 : 1, buttons.Count);
		if (!deferred && hasAvatar)
		{
			Assert.Equal("Remove avatar", buttons[0].TextContent);
			Assert.Contains("danger", buttons[0].ClassList);
		}
		if (!deferred)
		{
			Assert.Equal("Upload avatar", buttons[^1].TextContent);
			Assert.Contains("quiet", buttons[^1].ClassList);
			Assert.True(buttons[^1].HasAttribute("disabled"));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ConfirmedMutation_RefreshFailurePreservesResultAndSingleSemanticToastAsync(bool remove)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(!remove, Guid.NewGuid()))));
		var editor = context.Render<AvatarEditor>(parameters => parameters
			.Add(component => component.ApiPath, "users/me/avatar")
			.Add(component => component.HasAvatar, remove)
			.Add(component => component.Changed, () => throw new HttpRequestException("Refresh failed")));
		if (remove)
			await editor.Find("button[aria-label='Remove avatar']").ClickAsync(new());
		else
		{
			editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1 }, "a.png", contentType: "image/png"));
			await editor.Find("form").SubmitAsync();
		}
		var notification = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(remove ? "Avatar removed" : "Avatar updated", notification.Message);
		Assert.Equal(remove ? NotificationKind.Danger : NotificationKind.Success, notification.Kind);
		Assert.Equal(remove ? 0 : 1, editor.FindAll("button[aria-label='Remove avatar']").Count);
		Assert.Contains("Your changes were confirmed, but the view could not be refreshed.", editor.Markup);
		Assert.False(editor.Instance.HasPending);
	}

	[Fact]
	public async Task Upload_OverFourMiBDoesNotSendOrNotifyAsync()
	{
		int calls = 0;
		using var context = new ComponentTestContext(_ => { calls++; return Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, Guid.NewGuid()))); });
		var editor = context.Render<AvatarEditor>();
		editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[4 * 1024 * 1024 + 1], "large.png", contentType: "image/png"));
		await editor.Find("form").SubmitAsync();
		Assert.Equal(0, calls);
		Assert.Empty(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Contains("The file could not be read.", editor.Markup);
		Assert.True(editor.Instance.HasPending);
	}

	[Fact]
	public async Task Upload_AcceptsFourMiBInclusiveAsync()
	{
		byte[]? uploaded = null;
		using var context = new ComponentTestContext(async request =>
		{
			uploaded = await request.Content!.ReadAsByteArrayAsync();
			return AvatarTestResponses.Json(new AvatarState(true, Guid.NewGuid()));
		});
		var editor = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar"));
		editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[4 * 1024 * 1024], "avatar.png", contentType: "image/png"));
		await editor.Find("form").SubmitAsync();
		Assert.Equal(4 * 1024 * 1024, uploaded?.Length);
		Assert.Equal("Avatar updated", Assert.Single(context.Services.GetRequiredService<NotificationState>().Items).Message);
	}
}
