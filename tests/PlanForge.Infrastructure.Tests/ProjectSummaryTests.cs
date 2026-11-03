namespace PlanForge.Infrastructure.Tests;

using PlanForge.Contracts.Planning;

public sealed class ProjectSummaryTests
{
	[Fact]
	public void Summary_WhenInheritedOverriddenOrUnassigned_ResolvesOnlyExpectedDescendants()
	{
		Guid a = Guid.NewGuid();
		Guid b = Guid.NewGuid();
		Guid phase = Guid.NewGuid();
		Guid subphase = Guid.NewGuid();
		Guid first = Guid.NewGuid();
		Guid second = Guid.NewGuid();
		Guid third = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 1, "Plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "Phase", AssignmentMode: "Override", AssignedAgentId: a), new(subphase, "Subphase", phase, 0, "Subphase"),
			new(first, "Step", subphase, 0, "Inherit", Status: "Completed"), new(second, "Step", subphase, 1, "Override", AssignmentMode: "Override", AssignedAgentId: b), new(third, "Step", subphase, 2, "None", AssignmentMode: "Unassigned")]);
		Assert.Equal(a, project.GetEffectiveAgentId(first));
		Assert.Equal(b, project.GetEffectiveAgentId(second));
		Assert.Null(project.GetEffectiveAgentId(third));
		Assert.Equal(33, project.Progress);
		Assert.Equal(0, (project with { Items = [] }).Progress);
	}
}
