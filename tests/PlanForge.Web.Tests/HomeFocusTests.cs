namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;

public sealed class HomeFocusTests
{
	[Fact]
	public void Home_ShowsThreeRealMetricsAndOrderedDeepLinksWithoutFetchingTrees()
	{
		Guid projectId = Guid.NewGuid(), firstStep = Guid.NewGuid();
		var focus = new FocusStep(projectId, 4, "Project name", firstStep, "Oldest pending", Guid.NewGuid(), "Phase name", Guid.NewGuid(), "Subphase name", "Pending", 1);
		var paths = new List<string>();
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath; paths.Add(path);
			object response = path.EndsWith("/dashboard", StringComparison.Ordinal) ? new WorkspaceDashboard(1, 2, 1, 5, [focus])
				: path.EndsWith("/summary", StringComparison.Ordinal) ? new ProjectSummaryPage([Summary(projectId)], 1, 0, 25, false)
				: path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<PlanForge.Contracts.Agents.AgentProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(4, 0, false, [])
				: new ProjectSnapshot(projectId, 4, "Project name", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
			return Task.FromResult(ComponentTestContext.Json(response));
		});
		var page = context.Render<Home>();
		Assert.Equal(3, page.FindAll("#workspace-metrics > div").Count);
		Assert.Contains("20%", page.Find("[data-metric=progress]").TextContent);
		Assert.Contains("2", page.Find("[data-metric=pending]").TextContent);
		Assert.Equal($"/projects/{projectId}?step={firstStep}", page.Find("#next-in-focus a").GetAttribute("href"));
		Assert.Contains("Phase name", page.Find("#next-in-focus").TextContent);
		Assert.Contains("Subphase name", page.Find("#next-in-focus").TextContent);
		Assert.Empty(page.FindAll(".project-grid, .filter-bar"));
		Assert.Equal(new[] { "/api/v1/projects/dashboard", "/api/v1/projects/summary" }, paths);
	}

	[Fact]
	public async Task Home_RecentProjectsCanReachEveryPageWithoutLoadingAllTreesAsync()
	{
		ProjectSummary[] all = Enumerable.Range(1, 26).Select(index => Summary(Guid.NewGuid()) with { Name = "Project " + index }).ToArray();
		var requests = new List<string>();
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.PathAndQuery; requests.Add(path);
			int skip = path.Contains("skip=25", StringComparison.Ordinal) ? 25 : 0;
			object response = path.Contains("/dashboard", StringComparison.Ordinal) ? new WorkspaceDashboard(26, 0, 0, 0, []) : new ProjectSummaryPage(all.Skip(skip).Take(25).ToList(), 26, skip, 25, skip == 0);
			return Task.FromResult(ComponentTestContext.Json(response));
		});
		var page = context.Render<Home>();
		Assert.Equal(25, page.FindAll("#recent-projects li").Count);
		Assert.Contains("Page 1 / 2", page.Find("#recent-project-pages").TextContent);
		await page.Find("#recent-project-pages button:last-child").ClickAsync(new());
		Assert.Single(page.FindAll("#recent-projects li"));
		Assert.Contains("Project 26", page.Find("#recent-projects").TextContent);
		Assert.True(page.Find("#recent-project-pages button:last-child").HasAttribute("disabled"));
		Assert.Equal(4, requests.Count);
	}

	[Fact]
	public void Projects_PreservesCatalogAndFiltersWithoutDuplicatingHomeMetrics()
	{
		using var context = new ComponentTestContext(request =>
		{
			Assert.EndsWith("/projects/summary", request.RequestUri!.AbsolutePath);
			return Task.FromResult(ComponentTestContext.Json(new ProjectSummaryPage([Summary(Guid.NewGuid())], 1, 0, 100, false)));
		});
		context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
		var page = context.Render<Home>();
		Assert.Single(page.FindAll(".project-card"));
		Assert.Empty(page.FindAll("#workspace-metrics, #next-in-focus, #recent-projects"));
		page.Find("input[aria-label='Search projects']").Input("No match");
		Assert.Empty(page.FindAll(".project-card"));
		Assert.Contains("No projects match", page.Markup);
	}

	private static ProjectSummary Summary(Guid id) => new(id, 4, "Project name", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, 1, 5, 1, 0, 20, 0);
}
