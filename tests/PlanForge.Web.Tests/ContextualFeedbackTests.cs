namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;

public sealed class ContextualFeedbackTests
{
	[Theory]
	[InlineData("title")]
	[InlineData("association")]
	[InlineData("unlink")]
	public async Task ManageResource_FailureBelongsOnlyToItsOwnOperationAsync(string operation)
	{
		using var fixture = new ResourcePanelFixture { FailMutation = true };
		fixture.Resource = fixture.Resource with { Projects = [new(fixture.ProjectId, "Target project")] };
		var page = fixture.Context.Render<ResourcePanel>();
		await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		if (operation == "title") await page.Find("#resource-metadata-form").SubmitAsync();
		else if (operation == "association")
		{
			await page.Find("#link-project").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = fixture.ProjectId.ToString() });
			await page.Find("#resource-link-form").SubmitAsync();
		}
		else await page.FindAll("button").Single(button => button.TextContent == "Unlink").ClickAsync(new());
		var error = Assert.Single(page.FindAll("[role=alert]"));
		Assert.NotNull(error.Closest(operation == "title" ? "#resource-metadata-form" : operation == "association" ? "#resource-link-form" : ".resource-associations"));
	}

	[Theory]
	[InlineData("agent")]
	[InlineData("token")]
	[InlineData("profile")]
	[InlineData("project")]
	[InlineData("upload")]
	public async Task ShortForms_SubmitFailureIsInsideFormImmediatelyBeforeActionsAsync(string operation)
	{
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method != HttpMethod.Get) return Task.FromResult(ComponentTestContext.Json(new { detail = "The fixture operation failed." }, System.Net.HttpStatusCode.BadRequest));
			string path = request.RequestUri!.AbsolutePath;
			object result = path.EndsWith("/users/me", StringComparison.Ordinal) ? new PlanForge.Contracts.Accounts.UserProfile(Guid.NewGuid(), "fixture@example.test", "Fixture", DateTimeOffset.UtcNow)
				: path.EndsWith("/agent-tokens", StringComparison.Ordinal) ? Array.Empty<PlanForge.Contracts.Agents.AgentTokenProfile>()
				: path.EndsWith("/usage", StringComparison.Ordinal) ? new PlanForge.Contracts.Library.ResourceUsage(0, 1024, 512, false)
				: path.EndsWith("/resources", StringComparison.Ordinal) ? new PlanForge.Contracts.Library.ResourcePage([], 0, 0, 50)
				: path.EndsWith("/dashboard", StringComparison.Ordinal) ? new PlanForge.Contracts.Planning.WorkspaceDashboard(0, 0, 0, 0, [])
				: path.EndsWith("/summary", StringComparison.Ordinal) ? new PlanForge.Contracts.Planning.ProjectSummaryPage([], 0, 0, 100, false)
				: Array.Empty<PlanForge.Contracts.Agents.AgentProfile>();
			return Task.FromResult(ComponentTestContext.Json(result));
		});
		Type type = operation switch { "agent" => typeof(Agents), "token" => typeof(AgentTokenPanel), "profile" => typeof(Settings), "project" => typeof(Home), _ => typeof(ResourcePanel) };
		var page = context.Render<Microsoft.AspNetCore.Components.DynamicComponent>(parameters => parameters.Add(component => component.Type, type));
		if (operation != "profile")
		{
			string label = operation == "agent" ? "New agent" : operation == "token" ? "Create token" : operation == "project" ? "New project" : "Upload file";
			await page.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).ClickAsync(new());
		}
		if (operation == "upload") page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "fixture.txt", contentType: "text/plain"));
		await page.Find(operation == "profile" ? "#profile-form" : "dialog form").SubmitAsync();
		var error = Assert.Single(page.FindAll("[role=alert]"));
		Assert.NotNull(error.Closest("form"));
		Assert.Equal("form-actions", error.NextElementSibling?.ClassName);
		if (operation != "profile")
		{
			await page.Find("button[aria-label='Close dialog']").ClickAsync(new());
			Assert.Empty(page.FindAll("[role=alert]"));
		}
	}

	[Fact]
	public void Confirmation_ErrorIsAfterExplanationAndBeforeActions()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var dialog = context.Render<ConfirmDialog>(parameters => parameters.Add(component => component.Error, "Deletion was not confirmed."));
		var error = Assert.Single(dialog.FindAll("[role=alert]"));
		Assert.Equal("form-actions", error.NextElementSibling?.ClassName);
		Assert.NotNull(error.PreviousElementSibling?.QuerySelector("#confirm-intent"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Project_SaveFailureStaysInItsFormWithoutBackgroundDuplicateAsync(bool settings)
	{
		using var fixture = new ProjectPageFixture { Conflict = true };
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		await page.FindAll("button").Single(button => settings ? button.TextContent == "Settings" : button.TextContent.Contains("Add phase", StringComparison.Ordinal)).ClickAsync(new());
		if (settings) page.Find("#project-name").Change("Retained unsaved name");
		else page.Find("#item-title").Change("Retained unsaved name");
		await page.Find("dialog form").SubmitAsync();
		var error = Assert.Single(page.FindAll("[role=alert]"));
		Assert.NotNull(error.Closest("dialog form"));
		Assert.Equal("form-actions", error.NextElementSibling?.ClassName);
		Assert.Equal("Retained unsaved name", page.Find(settings ? "#project-name" : "#item-title").GetAttribute("value"));
	}
}
