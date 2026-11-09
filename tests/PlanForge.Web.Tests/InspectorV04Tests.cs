namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class InspectorV04Tests
{
	[Theory]
	[InlineData("Project", "Add phase", 1)]
	[InlineData("Phase", "Add subphase", 2)]
	[InlineData("Subphase", "Add step", 3)]
	[InlineData("Step", "Edit step", 4)]
	public async Task Inspector_HasCompleteCurrentBreadcrumbAndScopedActionsResourcesAndDecisionsAsync(string kind, string action, int levels)
	{
		Guid projectId = Guid.NewGuid(), phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		string description = "Full description line\n\n" + new string('x', 800) + " <script>unsafe()</script>";
		Guid target = kind switch { "Phase" => phase, "Subphase" => subphase, "Step" => step, _ => projectId };
		var project = new ProjectSnapshot(projectId, 4, "Project title", description, "Blocked", "DirectToMain", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase title", description), new(subphase, "Subphase", phase, 0, "Subphase title", description),
			new(step, "Step", subphase, 0, "Step title", description, "InProgress") { EffectiveStatus = "Blocked" },
			new(Guid.NewGuid(), "Decision", target, 0, "Question title", "Full question\nsecond paragraph", "Pending", Resolution: "Prior resolution")
		]);
		var resource = new ResourceProfile(Guid.NewGuid(), "Shared", "shared.txt", "text/plain", 1, "hash", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [new(projectId, "Project title")], [])
		{
			Items = [new(step, "Step title", "Step", projectId, "Project title")]
		};
		(Guid? ParentId, string Kind)? added = null;
		Guid? attachment = null;
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var inspector = context.Render<WorkPlanInspector>(parameters => parameters
			.Add(component => component.Snapshot, project).Add(component => component.Item, kind == "Project" ? null : project.Items.Single(item => item.Id == target))
			.Add(component => component.Resources, [resource])
			.Add(component => component.Add, value => added = (value.ParentId, value.Kind))
			.Add(component => component.AttachResources, id => attachment = id));
		Assert.DoesNotContain("Back to structure", inspector.Markup);
		Assert.DoesNotContain("<details", inspector.Markup);
		Assert.DoesNotContain("<script", inspector.Markup);
		Assert.Equal(description, inspector.Find(".inspector-description").TextContent);
		Assert.Equal(levels, inspector.FindAll(".selection-path li").Count);
		Assert.Single(inspector.FindAll(".selection-path [aria-current='page']"));
		Assert.Empty(inspector.FindAll(".selection-path li:last-child button, .selection-path li:last-child a"));
		Assert.Contains(action, inspector.Find(".inspector-actions").TextContent);
		Assert.DoesNotContain("Add decision", inspector.Find(".inspector-actions").TextContent);
		Assert.Contains(kind + " resources", inspector.Find("#plan-inspector").TextContent);
		Assert.Single(inspector.FindAll($"[data-resource-id='{resource.Id}']"));
		Assert.Contains(kind == "Project" ? "Direct" : "Inherited from Project", inspector.Find("#plan-inspector").TextContent);
		Assert.Contains("Full question\nsecond paragraph", inspector.Find(".inspector-decision").TextContent);
		Assert.Contains("Prior resolution", inspector.Find(".inspector-decision").TextContent);
		Assert.Contains("Edit decision", inspector.Find(".inspector-decision").TextContent);
		Assert.Contains("Delete decision", inspector.Find(".inspector-decision").TextContent);
		await inspector.FindAll(".inspector-decisions button").Single(button => button.TextContent.Contains("Add decision", StringComparison.Ordinal)).ClickAsync(new());
		Assert.Equal((target, "Decision"), added);
		await inspector.FindAll(".inspector-resources button").Single(button => button.TextContent.Contains("Attach resources", StringComparison.Ordinal)).ClickAsync(new());
		Assert.Equal(target, attachment);
		if (kind == "Step")
		{
			Assert.DoesNotContain("Add step", inspector.Find(".inspector-actions").TextContent);
			Assert.Contains("Blocked", inspector.Find(".badge").TextContent);
			Assert.Contains("InProgress", inspector.Find("#plan-inspector").TextContent);
		}
	}
}
