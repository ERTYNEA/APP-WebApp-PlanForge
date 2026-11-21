namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;

public sealed class HierarchyProgressV07Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Detail_UsesExactKindsAndEffectiveStatesWithoutChangingBlockedProject(bool empty)
	{
		Guid id = Guid.NewGuid(), phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		PlanItem[] items = empty ? [] : [new(phase, "Phase", null, 0, "Phase", Status: "Completed") { EffectiveStatus = "Blocked" }, new(subphase, "Subphase", phase, 0, "Subphase", Status: "Completed"), new(Guid.NewGuid(), "Step", subphase, 0, "Done", Status: "Completed"), new(Guid.NewGuid(), "Decision", id, 0, "Unresolved")];
		var snapshot = new ProjectSnapshot(id, 1, "Blocked project", "", "Blocked", "PlanningOnly", DateTimeOffset.UtcNow, items);
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			object value = path.EndsWith("/agents", StringComparison.Ordinal) ? Array.Empty<AgentProfile>() : path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(1, 0, false, []) : snapshot;
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, id));
		Assert.Equal("Blocked", page.Find(".project-overview .badge").TextContent);
		Assert.Equal(new[] { "Phases", "Subphases", "Steps" }, page.FindAll(".project-overview .status-progress h3").Select(element => element.TextContent));
		Assert.Equal(empty ? new[] { "0/0 · 0%", "0/0 · 0%", "0/0 · 0%" } : new[] { "0/1 · 0%", "1/1 · 100%", "1/1 · 100%" }, page.FindAll(".project-overview .status-ratio").Select(element => element.TextContent));
		Assert.Equal(12, page.FindAll(".project-overview .status-segment").Count);
		Assert.Equal(empty ? "0" : "1", page.Find(".project-overview .status-segment[data-status=error]").GetAttribute("data-count"));
		Assert.DoesNotContain("% complete", page.Find(".project-overview").TextContent);
	}
}
