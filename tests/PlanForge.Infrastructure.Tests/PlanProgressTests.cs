namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;

public sealed class PlanProgressTests
{
	[Theory]
	[InlineData("Phase", "Pending", "Completed", "InProgress")]
	[InlineData("Subphase", "Pending", "Completed", "InProgress")]
	[InlineData("Step", "Pending", "Completed", "InProgress")]
	[InlineData("Step", "Decided", "Completed", "Completed")]
	[InlineData("Subphase", "Pending", "Pending", "Pending")]
	[InlineData("Phase", "Decided", "Pending", "Pending")]
	[InlineData("Step", "Pending", "Blocked", "Blocked")]
	public void Recalculate_PendingDecisionPreventsClosureWithoutInventingWorkOrBlocking(string scope, string decisionStatus, string stepStatus, string expected)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"),
			new(step, "Step", subphase, 0, "Step", Status: stepStatus),
			new(Guid.NewGuid(), "Decision", scope == "Phase" ? phase : scope == "Subphase" ? subphase : step, 0, "Decision", Status: decisionStatus, Resolution: decisionStatus == "Decided" ? "Resolved" : "")
		]);
		Assert.Equal(expected, PlanProgress.Recalculate(snapshot).Status);
	}

	[Fact]
	public void Recalculate_EmptyParentsArePendingEvenWhenManuallyCompletedOrBlocked()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "Completed", "PlanningOnly", DateTimeOffset.UtcNow, []);
		Assert.Equal("Pending", PlanProgress.Recalculate(snapshot).Status);
		snapshot = snapshot with { Items = [new(phase, "Phase", null, 0, "Phase", Status: "Blocked"), new(subphase, "Subphase", phase, 0, "Subphase", Status: "Completed")] };
		Assert.All(PlanProgress.Recalculate(snapshot).Items, item => Assert.Equal("Pending", item.Status));
	}

	[Theory]
	[InlineData("Pending", "Pending", "Pending")]
	[InlineData("Completed", "Completed", "Completed")]
	[InlineData("Pending", "Completed", "InProgress")]
	[InlineData("Pending", "InProgress", "InProgress")]
	[InlineData("Blocked", "Completed", "Blocked")]
	[InlineData("Blocked", "InProgress", "Blocked")]
	public void Recalculate_DerivesAllAncestorsIgnoringManualParentState(string first, string second, string expected)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "Blocked", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase", Status: "Blocked"),
			new(subphase, "Subphase", phase, 0, "Subphase", Status: "Blocked"),
			new(Guid.NewGuid(), "Step", subphase, 0, "First", Status: first),
			new(Guid.NewGuid(), "Step", subphase, 1, "Second", Status: second)
		]);

		ProjectSnapshot actual = PlanProgress.Recalculate(snapshot);

		Assert.Equal(expected, actual.Status);
		Assert.All(actual.Items.Where(item => item.Kind is "Phase" or "Subphase"), item => Assert.Equal(expected, item.Status));
		Assert.Equal(first, actual.Items.Single(item => item.Title == "First").Status);
		Assert.Equal(second, actual.Items.Single(item => item.Title == "Second").Status);
	}
}
