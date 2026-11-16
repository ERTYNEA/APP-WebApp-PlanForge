namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.State;

public sealed class ProjectResourceRefreshGapTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ConfirmedResourceWrite_WhenOnlyParentReloadFailsKeepsResultAndOneTruthfulWarningAsync(bool unlink)
	{
		Guid projectId = Guid.NewGuid();
		var project = new ProjectSnapshot(projectId, 1, "Project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		var resource = new ResourceProfile(Guid.NewGuid(), "File", "file.txt", "text/plain", 3, "hash", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [new(projectId, "Project")], []);
		bool committed = false;
		int childUsageReads = 0, childResourceReads = 0, failedParentReads = 0, writes = 0;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method != HttpMethod.Get)
			{
				writes++;
				committed = true;
				resource = resource with { Revision = 2, Title = unlink ? "File" : "Renamed", Projects = unlink ? [] : resource.Projects };
				return Task.FromResult(ComponentTestContext.Json(resource));
			}
			if (committed && path == $"/api/v1/projects/{projectId}")
			{
				failedParentReads++;
				throw new HttpRequestException("Parent-only project reload failed.");
			}
			if (committed && path.EndsWith("/usage", StringComparison.Ordinal))
				childUsageReads++;
			if (committed && path.EndsWith("/resources", StringComparison.Ordinal))
				childResourceReads++;
			object value = path.EndsWith("/usage", StringComparison.Ordinal) ? new ResourceUsage(3, 10000, 1024, false)
				: path.EndsWith("/summary", StringComparison.Ordinal) ? new ProjectSummaryPage([], 0, 0, 100, false)
				: path.EndsWith("/resources", StringComparison.Ordinal) ? new[] { resource }
				: path.EndsWith("/agents", StringComparison.Ordinal) ? Array.Empty<AgentProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(1, 0, false, [])
				: project;
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, projectId));
		await page.FindAll("button").Single(button => button.TextContent == "Settings").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Manage project resources").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		if (unlink)
			await page.FindAll("button").Single(button => button.TextContent == "Unlink").ClickAsync(new());
		else
		{
			page.Find("#resource-title").Change("Renamed");
			await page.Find("#resource-metadata-form").SubmitAsync();
		}

		Assert.Equal(1, writes);
		Assert.True(childUsageReads > 0);
		Assert.True(childResourceReads > 0);
		Assert.Equal(1, failedParentReads);
		var notification = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(unlink ? "Resource unlinked" : "Resource updated", notification.Message);
		Assert.Equal(unlink ? NotificationKind.Danger : NotificationKind.Success, notification.Kind);
		var warning = Assert.Single(page.FindAll("[role=alert]"));
		Assert.Contains(UiText.Get("Your changes were confirmed, but the view could not be refreshed. Reload to see the latest data."), warning.TextContent);
		Assert.NotNull(warning.Closest(unlink ? ".resource-associations" : "#resource-metadata-form"));
		Assert.DoesNotContain("Your changes have not been confirmed", page.Markup);
	}
}
