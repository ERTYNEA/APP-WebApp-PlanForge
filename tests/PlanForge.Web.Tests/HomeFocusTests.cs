namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;

public sealed class HomeFocusTests
{
	[Fact]
	public void Projects_CardHasOnlyOneQueryFreeOpenLinkAndPlainTitle()
	{
		Guid id = Guid.NewGuid();
		using var context = new ComponentTestContext(_ => Task.FromResult(ComponentTestContext.Json(new ProjectSummaryPage([Summary(id)], 1, 0, 100, false))));
		context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
		var page = context.Render<Projects>();
		var card = page.Find(".project-card");
		Assert.Empty(card.QuerySelectorAll("h2 a"));
		var link = Assert.Single(card.QuerySelectorAll("a"));
		Assert.Equal($"/projects/{id}", link.GetAttribute("href"));
		Assert.Equal("Open project →", link.TextContent);
		Assert.DoesNotContain("Settings", card.TextContent);
		Assert.False(card.HasAttribute("onclick"));
	}

	[Fact]
	public void Home_ShowsFourCategoriesAndOrderedDeepLinksWithoutFetchingTrees()
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
		Assert.Equal(4, page.FindAll("#workspace-metrics > article").Count);
		foreach (var card in page.FindAll("#workspace-metrics > article"))
		{
			Assert.Equal(4, card.QuerySelectorAll("dl > div").Length);
			Assert.Equal(new[] { "Pending", "In progress", "Blocked", "Completed" }, card.QuerySelectorAll("dt").Select(element => element.TextContent.Trim()));
		}
		Assert.Contains("20%", page.Find("#overall-progress").TextContent);
		Assert.Contains("1 of 5 steps completed", page.Find("#overall-progress").TextContent);
		Assert.Equal("20", page.Find("#overall-progress progress").GetAttribute("value"));
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
		var page = context.Render<Projects>();
		Assert.Single(page.FindAll(".project-card"));
		Assert.Empty(page.FindAll("#workspace-metrics, #next-in-focus, #recent-projects"));
		page.Find("input[aria-label='Search projects']").Input("No match");
		Assert.Empty(page.FindAll(".project-card"));
		Assert.Contains("No projects match", page.Markup);
	}

	[Fact]
	public void Home_EmptyWorkspaceKeepsSixteenZeroRowsAndProgress()
	{
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(
			request.RequestUri!.AbsolutePath.EndsWith("/dashboard", StringComparison.Ordinal) ? (object)new WorkspaceDashboard(0, 0, 0, 0, []) : new ProjectSummaryPage([], 0, 0, 25, false))));
		var page = context.Render<Home>();
		Assert.Equal(4, page.FindAll("#workspace-metrics > article").Count);
		Assert.All(page.FindAll("#workspace-metrics dd"), value => Assert.Equal("0", value.TextContent));
		Assert.Equal(16, page.FindAll("#workspace-metrics dd").Count);
		Assert.Contains("0 of 0 steps completed", page.Find("#overall-progress").TextContent);
		Assert.Equal("0", page.Find("#overall-progress progress").GetAttribute("value"));
		Assert.NotNull(page.Find("#next-in-focus"));
	}

	private static ProjectSummary Summary(Guid id) => new(id, 4, "Project name", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, 1, 5, 1, 0, 20, 0);
}
