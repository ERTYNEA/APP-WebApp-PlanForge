namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Web.Components.Shared;
using System.Net;

public sealed class AvatarEditorTests
{
	[Fact]
	public async Task Reselection_ClearsRejectedUploadWithoutAnnouncingSuccessAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(ComponentTestContext.Json(new { detail = "Rejected upload" }, HttpStatusCode.BadRequest)));
		var page = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar"));
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("bad", "bad.png", contentType: "image/png"));
		await page.Find("form").SubmitAsync();
		Assert.Contains("Rejected upload", page.Markup);
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("new", "new.png", contentType: "image/png"));
		Assert.DoesNotContain("Rejected upload", page.Markup);
		Assert.DoesNotContain("Avatar updated", page.Markup);
		await page.Find("form").SubmitAsync();
		Assert.Contains("Rejected upload", page.Markup);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Change_WhenUploadedOrRemoved_UsesSeparateRasterEndpointAsync(bool remove)
	{
		string? method = null, path = null, mime = null;
		byte[]? bytes = null;
		using var context = new ComponentTestContext(async request => { method = request.Method.Method; path = request.RequestUri!.AbsolutePath; mime = request.Content?.Headers.ContentType?.MediaType; bytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(); return AvatarTestResponses.Json(new PlanForge.Contracts.Accounts.AvatarState(!remove, Guid.NewGuid())); });
		var page = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar").Add(component => component.HasAvatar, true));
		if (remove) await page.Find("button[aria-label='Remove avatar']").ClickAsync(new());
		else { page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "avatar.png", contentType: "image/png")); await page.Find("form").SubmitAsync(); }
		Assert.Empty(page.FindAll("[role=alert]"));
		Assert.Equal(remove ? "DELETE" : "PUT", method);
		Assert.Equal("/api/v1/users/me/avatar", path);
		if (!remove) { Assert.Equal("image/png", mime); Assert.Equal(new byte[] { 1, 2, 3 }, bytes); }
	}
}
