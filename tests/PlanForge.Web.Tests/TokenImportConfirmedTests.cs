namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class TokenImportConfirmedTests
{
	[Theory]
	[InlineData(false, "Token revoked")]
	[InlineData(true, "Revoked and expired tokens deleted")]
	public async Task TokenDestruction_WhenRefreshFails_RetainsConfirmedResultAsync(bool cleanup, string message)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var credential = new AgentTokenProfile(Guid.NewGuid(), "Controlled", null, now, now.AddDays(1), cleanup ? now : null, null);
		bool committed = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method != HttpMethod.Get)
			{
				committed = true;
				return Task.FromResult(cleanup ? ComponentTestContext.Json(new { deletedCount = 1 }) : new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
			}
			if (committed)
				throw new HttpRequestException("Controlled post-write metadata failure");
			return Task.FromResult(ComponentTestContext.Json(new[] { credential }));
		});
		var panel = context.Render<AgentTokenPanel>();
		await panel.FindAll("button").Single(button => button.TextContent == (cleanup ? "Delete revoked tokens" : "Revoke")).ClickAsync(new());
		panel.Find("#confirm-intent").Change(true);
		await panel.Find("#confirm-delete").ClickAsync(new());
		Assert.True(committed);
		var notifications = context.Services.GetRequiredService<NotificationState>().Items;
		Assert.Contains(notifications, toast => toast.Kind == NotificationKind.Danger && toast.Message == message);
		Assert.Contains("Your changes were confirmed, but the view could not be refreshed", string.Join(" ", notifications.Select(toast => toast.Message)) + panel.Markup);
		Assert.DoesNotContain("Your changes have not been confirmed", string.Join(" ", notifications.Select(toast => toast.Message)) + panel.Markup);
		Assert.Empty(panel.FindAll("dialog"));
	}

	[Fact]
	public async Task CsvImport_WhenCommittedAndResourcesReadFails_ClosesFormAndPreservesResultAsync()
	{
		var project = new ProjectSnapshot(Guid.NewGuid(), 1, "Controlled project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		bool committed = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Post)
			{
				committed = true;
				return Task.FromResult(ComponentTestContext.Json(project with { Revision = 2 }));
			}
			string path = request.RequestUri!.AbsolutePath;
			if (committed && path.EndsWith("/resources", StringComparison.Ordinal))
				throw new HttpRequestException("Controlled resource reread failure");
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>()
				: path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(1, 1, false, []) : project));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, project.Id));
		await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("Controlled CSV payload", "work-plan.csv"));
		await page.Find("#import-plan-form").SubmitAsync();
		Assert.True(committed);
		var notifications = context.Services.GetRequiredService<NotificationState>().Items;
		Assert.Contains(notifications, toast => toast.Kind == NotificationKind.Success && toast.Message == "CSV imported successfully");
		Assert.Contains("Your changes were confirmed, but the view could not be refreshed", string.Join(" ", notifications.Select(toast => toast.Message)) + page.Markup);
		Assert.Empty(page.FindAll("#import-plan-form"));
	}
}
