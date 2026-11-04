namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;

public sealed class HomeFocusTests
{
	[Fact]
	public void Render_WhenSubphaseActiveWithoutSteps_ShowsRealCurrentContext()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "Purpose", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Implementation", Status: "InProgress"),
			new(subphase, "Subphase", phase, 0, "Active subphase without steps", Status: "InProgress")
		]);
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			object value = path.EndsWith("/summary", StringComparison.Ordinal)
				? new ProjectSummaryPage([new(project.Id, project.Revision, project.Name, project.Description, project.Status, project.Workflow, project.UpdatedAt, 1, 0, 0, 0, 0, 0)], 1, 0, 100, false)
				: path.EndsWith("/agents", StringComparison.Ordinal) ? Array.Empty<AgentProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(1, 0, false, []) : project;
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var component = context.Render<Home>();
		Assert.Contains("Active subphase without steps", component.Markup);
		Assert.Contains("Implementation", component.Markup);
	}
}
