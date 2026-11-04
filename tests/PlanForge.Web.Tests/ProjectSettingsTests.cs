namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using System.Text.Json;

public sealed class ProjectSettingsTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Resources_WhenOpened_ReceivesExactProjectAndOptionalPhaseContextAsync(bool phaseOnly)
	{
		using var fixture=new ProjectPageFixture();
		var page=fixture.Context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,fixture.Project.Id));
		if(!phaseOnly) await page.FindAll("button").Single(button=>button.TextContent=="Settings").ClickAsync(new());
		await page.FindAll("button").Single(button=>button.TextContent.Contains(phaseOnly ? "Attach resources" : "Manage project resources",StringComparison.Ordinal)).ClickAsync(new());
		var panel=page.FindComponent<PlanForge.Web.Components.Shared.ResourcePanel>();
		Assert.Equal(fixture.Project.Id,panel.Instance.ProjectId);
		Assert.Equal(phaseOnly ? fixture.Project.Items.Single(item=>item.Kind=="Phase").Id : null,panel.Instance.PhaseId);
	}

	[Fact]
	public async Task Save_WhenMetadataEdited_PreservesItemsRevisionAndSelectedWorkflowAsync()
	{
		using var fixture=new ProjectPageFixture();
		var page=fixture.Context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,fixture.Project.Id));
		await page.FindAll("button").Single(button=>button.TextContent=="Settings").ClickAsync(new());
		page.Find("#project-name").Change("Renamed project");
		page.Find("#project-workflow").Change("BranchPullRequest");
		await page.Find("#project-settings-form").SubmitAsync();
		var command=Assert.Single(fixture.Commands);
		Assert.Equal("PUT",command.Method);
		var snapshot=JsonSerializer.Deserialize<ProjectSnapshot>(command.Body!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.Equal(5,snapshot!.Revision);
		Assert.Equal("Renamed project",snapshot.Name);
		Assert.Equal("BranchPullRequest",snapshot.Workflow);
		Assert.Equal(3,snapshot.Items.Count);
	}
}
