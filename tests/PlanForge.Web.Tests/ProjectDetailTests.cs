namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using System.Net.Http.Json;

public sealed class ProjectDetailTests
{
	[Fact]
	public async Task AddPhase_WhenSubmitted_PostsCurrentRevisionAndRefreshesRealSnapshotAsync()
	{
		var project = new ProjectSnapshot(Guid.NewGuid(), 7, "Engine upgrade", "Purpose", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		PlanItemCommand? sent = null;
		using var context = new ComponentTestContext(async request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post && path.EndsWith("/phases", StringComparison.Ordinal))
			{
				sent = await request.Content!.ReadFromJsonAsync<PlanItemCommand>();
				Guid phase = Guid.NewGuid();
				project = project with { Revision = 8, Items = [new(phase, "Phase", null, 0, sent!.Title), new(Guid.NewGuid(), "Subphase", phase, 0, "Default subphase")] };
			}
			return ComponentTestContext.Json(path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>() : path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(project.Revision, 1, false, []) : project);
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, project.Id));
		await page.FindAll("button").Single(button => button.TextContent.Contains("Add phase", StringComparison.Ordinal)).ClickAsync(new());
		page.Find("#item-title").Change("New real phase");
		await page.Find("dialog form").SubmitAsync();
		page.WaitForAssertion(() => Assert.Equal(7, sent?.Revision));
		Assert.Equal("New real phase", sent!.Title);
		Assert.Contains("Revision 8", page.Markup);
		Assert.Contains("New real phase", page.Markup);
		Assert.Empty(page.FindAll("dialog"));
	}

	[Fact]
	public void Render_WhenProjectLoaded_ShowsActualHierarchyProgressAndWorkflow()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 7, "Engine upgrade", "Real purpose", "InProgress", "DirectToMain", DateTimeOffset.UtcNow, [new(phase, "Phase", null, 0, "Active phase", Status: "InProgress"), new(subphase, "Subphase", phase, 0, "Active subphase", Status: "InProgress"), new(Guid.NewGuid(), "Step", subphase, 0, "Current code step", Status: "InProgress")]);
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>() : request.RequestUri.AbsolutePath.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : request.RequestUri.AbsolutePath.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(7, 1, false, []) : project)));
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, project.Id));
		page.WaitForAssertion(() => Assert.Contains("Engine upgrade", page.Markup));
		Assert.Contains("Current code step", page.Markup);
		Assert.Contains("Direct to Main", page.Markup);
		Assert.Contains("Add phase", page.Markup);
		Assert.Contains("Export project", page.Markup);
		Assert.Contains("Import CSV", page.Markup);
		Assert.Contains("Revision 7", page.Markup);
		Assert.Contains("of steps complete", page.Markup);
	}
}
