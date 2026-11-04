namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;

public sealed class PlanProgressTests
{
	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	public void Recalculate_WhenAggregateExplicitlyStarted_PreservesInProgressWithPendingChildren(string kind)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase", Status: kind == "Phase" ? "InProgress" : "Pending"),
			new(subphase, "Subphase", phase, 0, "Subphase", Status: kind == "Subphase" ? "InProgress" : "Pending"),
			new(Guid.NewGuid(), "Step", subphase, 0, "Pending step")
		]);

		ProjectSnapshot actual = PlanProgress.Recalculate(snapshot);

		Assert.Equal("InProgress", actual.Items.Single(item => item.Kind == kind).Status);
		Assert.Equal(0, actual.Progress);
		Assert.Equal("Pending", actual.Items.Single(item => item.Kind == "Step").Status);
	}
}
