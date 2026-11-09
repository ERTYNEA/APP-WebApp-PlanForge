namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using System.Text.Json;

public sealed class PlanFormsV04Tests
{
	[Fact]
	public async Task NewProject_UsesDescriptionLabelWithoutChangingCompactTextareaAsync()
	{
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/dashboard", StringComparison.Ordinal) ? (object)new WorkspaceDashboard(0, 0, 0, 0, []) : new ProjectSummaryPage([], 0, 0, 25, false))));
		var page = context.Render<Home>();
		await page.FindAll("button").Single(button => button.TextContent.Contains("New project", StringComparison.Ordinal)).ClickAsync(new());
		Assert.DoesNotContain(page.FindAll("dialog label"), label => label.TextContent.StartsWith("Purpose", StringComparison.Ordinal));
		Assert.Contains(page.FindAll("dialog label"), label => label.TextContent.StartsWith("Description", StringComparison.Ordinal));
		Assert.Equal("3", page.Find("dialog textarea").GetAttribute("rows"));
	}

	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task EditForm_UsesOneBasedPositionCompactDescriptionAndOnlyValidStatesAsync(string kind)
	{
		using var fixture = new ProjectPageFixture();
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		PlanItem target = fixture.Project.Items.Single(item => item.Kind == kind);
		await page.Find($"button[data-select-id='{target.Id}']").ClickAsync(new());
		await page.Find($"button[aria-label='Edit {kind.ToLowerInvariant()} {target.Title}']").ClickAsync(new());
		Assert.Equal("1", page.Find("#item-order").GetAttribute("value"));
		Assert.Equal("1", page.Find("#item-order").GetAttribute("min"));
		Assert.Equal("Position", page.Find("label[for='item-order']").TextContent);
		Assert.Equal("3", page.Find("#item-description").GetAttribute("rows"));
		Assert.DoesNotContain("Order (zero-based)", page.Find("dialog").TextContent);
		if (kind == "Step")
			Assert.Equal(new[] { "Pending", "InProgress", "Completed" }, page.FindAll("#item-status option").Select(option => option.GetAttribute("value")));
		else
		{
			Assert.Empty(page.FindAll("#item-status"));
			Assert.StartsWith("InProgress", page.Find("dialog .calculated-status").TextContent.Trim());
			Assert.Contains("Status updates automatically as work progresses.", page.Find("dialog").TextContent);
		}
		page.Find("#item-order").Change("3");
		await page.Find("dialog form").SubmitAsync();
		PlanItemCommand sent = JsonSerializer.Deserialize<PlanItemCommand>(Assert.Single(fixture.Commands).Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		Assert.Equal(2, sent.SortOrder);
		Assert.Equal(target.Id.ToString(), fixture.Commands.Single().Path.Split('/').Last());
	}

	[Fact]
	public async Task ProjectSettings_HidesRevisionAndUsesDescriptionAndBadgeBeforeAutomaticCopyAsync()
	{
		using var fixture = new ProjectPageFixture();
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		Assert.DoesNotContain("Revision 5", page.Markup);
		Assert.Empty(page.FindAll(".project-heading p.user-text"));
		await page.FindAll("button").Single(button => button.TextContent == "Settings").ClickAsync(new());
		Assert.DoesNotContain(page.FindAll("#project-settings-form label"), label => label.TextContent.StartsWith("Purpose", StringComparison.Ordinal));
		Assert.Equal("Description", page.Find("label[for='project-description']").TextContent);
		Assert.Equal("3", page.Find("#project-description").GetAttribute("rows"));
		Assert.StartsWith("InProgress", page.Find("#project-settings-form .calculated-status").TextContent.Trim());
		Assert.Contains("Status updates automatically as work progresses.", page.Find("dialog").TextContent);
	}

	[Fact]
	public async Task ProjectDecision_CreatesWithRealRootParentAndCompactResolutionAsync()
	{
		using var fixture = new ProjectPageFixture();
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		await page.Find(".tree-roots > li > .plan-row .tree-select").ClickAsync(new());
		await page.Find(".inspector-decisions button").ClickAsync(new());
		Assert.Equal(fixture.Project.Id.ToString(), page.Find("#item-parent").GetAttribute("value"));
		Assert.Contains(page.FindAll("#item-parent option"), option => option.GetAttribute("value") == fixture.Project.Id.ToString());
		Assert.Equal("3", page.Find("#item-resolution").GetAttribute("rows"));
		page.Find("#item-title").Change("Root question");
		await page.Find("dialog form").SubmitAsync();
		PlanItemCommand sent = JsonSerializer.Deserialize<PlanItemCommand>(Assert.Single(fixture.Commands).Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		Assert.Equal(fixture.Project.Id, sent.ParentId);
	}
}
