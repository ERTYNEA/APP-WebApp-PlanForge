namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Library;
using PlanForge.Web.Components.Shared;
using System.Text.Json;

public sealed class ResourceScopeV04Tests
{
	[Theory]
	[InlineData("Project")]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task AttachExisting_SendsOnlySelectedNodesDirectAssociationAsync(string kind)
	{
		using var fixture = new ResourcePanelFixture();
		var page = fixture.Context.Render<ResourcePanel>(parameters => parameters
			.Add(component => component.ProjectId, fixture.ProjectId)
			.Add(component => component.PhaseId, kind == "Phase" ? fixture.PhaseId : null)
			.Add(component => component.SubphaseId, kind == "Subphase" ? fixture.SubphaseId : null)
			.Add(component => component.StepId, kind == "Step" ? fixture.StepId : null));
		await page.FindAll("button").Single(button => button.TextContent == "Attach existing").ClickAsync(new());
		page.Find("#existing-resource").Change(fixture.Resource.Id.ToString());
		await page.Find("#attach-resource-form").SubmitAsync();
		ResourceLinkRequest link = JsonSerializer.Deserialize<ResourceLinkRequest>(fixture.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		Assert.Equal(kind == "Project" ? fixture.ProjectId : null, link.ProjectId);
		Assert.Equal(kind == "Phase" ? fixture.PhaseId : null, link.PhaseId);
		Assert.Equal(kind == "Subphase" ? fixture.SubphaseId : null, link.SubphaseId);
		Assert.Equal(kind == "Step" ? fixture.StepId : null, link.StepId);
		Assert.True(link.Attach);
		Assert.Equal(fixture.Resource.Revision, link.Revision);
	}

	[Fact]
	public async Task ChildScope_OnlyUnlinksItsOwnDirectLinkAndShowsInheritedOriginsAsync()
	{
		using var fixture = new ResourcePanelFixture();
		fixture.Resource = fixture.Resource with
		{
			Projects = [new(fixture.ProjectId, "Target project")],
			Items = [new(fixture.PhaseId, "Target phase", "Phase", fixture.ProjectId, "Target project"), new(fixture.StepId, "Target step", "Step", fixture.ProjectId, "Target project")]
		};
		var page = fixture.Context.Render<ResourcePanel>(parameters => parameters.Add(component => component.ProjectId, fixture.ProjectId).Add(component => component.StepId, fixture.StepId));
		Assert.Single(page.FindAll(".resource-card"));
		Assert.Contains("Inherited from Project", page.Markup);
		Assert.Contains("Inherited from Phase", page.Markup);
		await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		Assert.Empty(page.FindAll("#resource-link-form"));
		Assert.DoesNotContain(page.FindAll("dialog button"), button => button.TextContent == "Delete resource");
		await page.Find("button[aria-label='Unlink association']").ClickAsync(new());
		ResourceLinkRequest link = JsonSerializer.Deserialize<ResourceLinkRequest>(fixture.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		Assert.False(link.Attach);
		Assert.Equal(fixture.StepId, link.StepId);
		Assert.Null(link.ProjectId);
		Assert.Null(link.PhaseId);
	}
}
