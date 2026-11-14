namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class InspectorStatusDateTests
{
	[Theory]
	[InlineData("Project")]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public void Inspector_RendersBrowserLocalDateBesideTheEffectiveBadge(string kind)
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException());
		context.JSInterop.Setup<string>("planForge.formatStatusDate", _ => true).SetResult("Nov 14, 2026 · 09:42");
		DateTimeOffset date = new(2026, 11, 14, 8, 42, 0, TimeSpan.Zero);
		var item = new PlanItem(Guid.NewGuid(), kind, null, 0, "Selected", Status: "InProgress") { EffectiveStatus = "Blocked", EffectiveStatusChangedAt = date };
		var project = new ProjectSnapshot(Guid.NewGuid(), 1, "Plan", "", "Blocked", "PlanningOnly", date, [item]) { EffectiveStatusChangedAt = date };
		var inspector = context.Render<WorkPlanInspector>(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.Item, kind == "Project" ? null : item));
		inspector.WaitForAssertion(() => Assert.Equal("Since Nov 14, 2026 · 09:42", inspector.Find(".inspector-status time").TextContent));
		Assert.Equal(date.ToString("O"), inspector.Find("time").GetAttribute("datetime"));
		Assert.Contains("Blocked", inspector.Find(".inspector-status .badge").TextContent);
	}

	[Fact]
	public void StatusDate_ReturningFromUnknownRendersTheSameKnownInstantAgain()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException());
		context.JSInterop.Setup<string>("planForge.formatStatusDate", _ => true).SetResult("Nov 14, 2026 · 09:42");
		DateTimeOffset date = new(2026, 11, 14, 8, 42, 0, TimeSpan.Zero);
		var component = context.Render<StatusDate>(parameters => parameters.Add(value => value.Value, date));
		component.WaitForAssertion(() => Assert.Contains("Nov 14", component.Markup));
		component.Render(parameters => parameters.Add(value => value.Value, null));
		Assert.Contains("Status date unavailable", component.Markup);
		component.Render(parameters => parameters.Add(value => value.Value, date));
		component.WaitForAssertion(() => Assert.Contains("Nov 14", component.Markup));
	}

	[Fact]
	public void Inspector_LegacyUnknownDoesNotBorrowUpdatedAt()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException());
		var project = new ProjectSnapshot(Guid.NewGuid(), 1, "Legacy", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		var inspector = context.Render<WorkPlanInspector>(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Contains("Status date unavailable", inspector.Find(".inspector-status").TextContent);
		Assert.Empty(inspector.FindAll(".inspector-status time"));
	}
}
