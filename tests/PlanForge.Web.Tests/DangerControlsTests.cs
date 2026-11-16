namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;
using PlanForge.Web.Components.Shared;

public sealed class DangerControlsTests
{
	[Theory]
	[InlineData(false, false, 0)]
	[InlineData(false, true, 0)]
	[InlineData(true, true, 0)]
	[InlineData(true, false, 1)]
	public async Task Confirmation_InvokesParentOnlyWithIntentWhenIdleAsync(bool intent, bool busy, int expectedCalls)
	{
		int calls = 0;
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var dialog = context.Render<ConfirmDialog>(parameters => parameters
			.Add(component => component.Busy, busy).Add(component => component.Confirm, () => calls++));
		await dialog.Find("#confirm-intent").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = intent });
		await dialog.Find("#confirm-delete").TriggerEventAsync("onclick", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
		Assert.Equal(expectedCalls, calls);
		Assert.Equal(!intent || busy, dialog.Find("#confirm-delete").HasAttribute("disabled"));
	}

	[Fact]
	public void Confirmation_RequestsCancelFocusWithoutChangingOrdinaryModal()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var confirmation = context.Render<ConfirmDialog>();
		Assert.Equal("Cancel", confirmation.Find("#confirm-cancel").TextContent);
		var invocation = context.JSInterop.Invocations.Last(call => call.Identifier == "planForge.openDialog");
		Assert.Equal("#confirm-cancel", Assert.IsType<string>(invocation.Arguments[1]));
		context.Render<Modal>(parameters => parameters.AddChildContent("<input autofocus />"));
		Assert.Single(context.JSInterop.Invocations.Last(call => call.Identifier == "planForge.openDialog").Arguments);
	}

	[Theory]
	[InlineData("header", false)]
	[InlineData("cancel", false)]
	[InlineData("escape", false)]
	[InlineData("header", true)]
	[InlineData("cancel", true)]
	[InlineData("escape", true)]
	public async Task Confirmation_PreventsClosingWhileBusyAsync(string action, bool busy)
	{
		int closes = 0;
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var dialog = context.Render<ConfirmDialog>(parameters => parameters
			.Add(component => component.Busy, busy).Add(component => component.Close, () => closes++));
		if (action == "escape")
			await dialog.Find("dialog").TriggerEventAsync("oncancel", EventArgs.Empty);
		else
			await dialog.Find(action == "header" ? "button[aria-label='Close dialog']" : "#confirm-cancel")
				.TriggerEventAsync("onclick", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
		Assert.Equal(busy ? 0 : 1, closes);
		Assert.Equal(busy, dialog.Find("button[aria-label='Close dialog']").HasAttribute("disabled"));
		Assert.Equal(busy, dialog.Find("#confirm-cancel").HasAttribute("disabled"));
		Assert.Equal(busy, dialog.Find("#confirm-intent").HasAttribute("disabled"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Tokens_PlaceRevokeBeforeRenewWithOriginalCallbacksAsync(bool busy)
	{
		Guid? revoked = null, renewed = null;
		var credential = new PlanForge.Contracts.Agents.AgentTokenProfile(Guid.NewGuid(), "Fixture token", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var row = context.Render<AgentTokenRow>(parameters => parameters
			.Add(component => component.Credential, credential).Add(component => component.Valid, true)
			.Add(component => component.Busy, busy).Add(component => component.Revoke, id => revoked = id)
			.Add(component => component.Renew, id => renewed = id));
		var buttons = row.FindAll(".toolbar button");
		Assert.Equal(new[] { "Revoke", "Renew" }, buttons.Select(button => button.TextContent));
		Assert.Contains("danger", buttons[0].ClassList);
		Assert.Contains("quiet", buttons[1].ClassList);
		Assert.All(buttons, button =>
		{
			Assert.Equal("button", button.GetAttribute("type"));
			Assert.Equal(busy, button.HasAttribute("disabled"));
		});
		if (busy)
			return;
		await buttons[0].ClickAsync(new());
		await row.FindAll(".toolbar button")[1].ClickAsync(new());
		Assert.Equal(credential.Id, revoked);
		Assert.Equal(credential.Id, renewed);
	}

	[Theory]
	[InlineData("Phase", false)]
	[InlineData("Subphase", false)]
	[InlineData("Step", false)]
	[InlineData("Decision", false)]
	[InlineData("Phase", true)]
	[InlineData("Subphase", true)]
	[InlineData("Step", true)]
	[InlineData("Decision", true)]
	public async Task Inspector_PlacesDeleteBeforeNormalSelectedItemActionsAsync(string kind, bool disabled)
	{
		var item = new PlanItem(Guid.NewGuid(), kind, null, 0, "Selected item", "");
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Fixture", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [item]);
		PlanItem? deleted = null, edited = null;
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var inspector = context.Render<WorkPlanInspector>(parameters => parameters
			.Add(component => component.Snapshot, snapshot).Add(component => component.Item, item)
			.Add(component => component.Disabled, disabled).Add(component => component.Delete, value => deleted = value)
			.Add(component => component.Edit, value => edited = value));
		var buttons = inspector.FindAll(".inspector-actions button");
		Assert.Equal($"Delete {kind.ToLowerInvariant()}", buttons[0].TextContent);
		Assert.Contains("danger", buttons[0].ClassList);
		Assert.Equal($"Edit {kind.ToLowerInvariant()}", buttons[1].TextContent);
		Assert.Equal(kind is "Phase" or "Subphase" ? 3 : 2, buttons.Count);
		Assert.All(buttons, button =>
		{
			Assert.Equal("button", button.GetAttribute("type"));
			Assert.Equal(disabled, button.HasAttribute("disabled"));
		});
		if (disabled)
			return;
		await buttons[0].ClickAsync(new());
		await inspector.FindAll(".inspector-actions button")[1].ClickAsync(new());
		Assert.Equal(item, deleted);
		Assert.Equal(item, edited);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Inspector_InlineDecisionPlacesDeleteBeforeEditAsync(bool disabled)
	{
		Guid projectId = Guid.NewGuid();
		var decision = new PlanItem(Guid.NewGuid(), "Decision", projectId, 0, "Question", "");
		var snapshot = new ProjectSnapshot(projectId, 1, "Fixture", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [decision]);
		PlanItem? deleted = null, edited = null;
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var inspector = context.Render<WorkPlanInspector>(parameters => parameters
			.Add(component => component.Snapshot, snapshot).Add(component => component.Disabled, disabled)
			.Add(component => component.Delete, value => deleted = value).Add(component => component.Edit, value => edited = value));
		var buttons = inspector.FindAll(".inspector-decision .toolbar button");
		Assert.Equal(new[] { "Delete decision", "Edit decision" }, buttons.Select(button => button.TextContent));
		Assert.Contains("danger", buttons[0].ClassList);
		Assert.All(buttons, button =>
		{
			Assert.Contains("compact", button.ClassList);
			Assert.Equal("button", button.GetAttribute("type"));
			Assert.Equal(disabled, button.HasAttribute("disabled"));
		});
		if (disabled)
			return;
		await buttons[0].ClickAsync(new());
		await inspector.FindAll(".inspector-decision .toolbar button")[1].ClickAsync(new());
		Assert.Equal(decision, deleted);
		Assert.Equal(decision, edited);
	}

	[Theory]
	[InlineData("Project")]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task Resources_DirectUnlinkUsesCompactDangerWithoutChangingTargetAsync(string kind)
	{
		using var fixture = new ResourcePanelFixture();
		Guid? itemId = kind switch { "Phase" => fixture.PhaseId, "Subphase" => fixture.SubphaseId, "Step" => fixture.StepId, _ => null };
		fixture.Resource = fixture.Resource with
		{
			Projects = [new(fixture.ProjectId, "Target project")],
			Items = itemId is Guid id ? [new(id, "Direct item", kind, fixture.ProjectId, "Target project")] : []
		};
		var panel = fixture.Context.Render<ResourcePanel>(parameters => parameters
			.Add(component => component.ProjectId, fixture.ProjectId)
			.Add(component => component.PhaseId, kind == "Phase" ? itemId : null)
			.Add(component => component.SubphaseId, kind == "Subphase" ? itemId : null)
			.Add(component => component.StepId, kind == "Step" ? itemId : null));
		await panel.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		var unlink = Assert.Single(panel.FindAll("button[aria-label='Unlink association']"));
		Assert.Contains("danger", unlink.ClassList);
		Assert.Contains("compact", unlink.ClassList);
		Assert.DoesNotContain("quiet", unlink.ClassList);
		Assert.Equal("button", unlink.GetAttribute("type"));
		Assert.False(unlink.HasAttribute("disabled"));
		await unlink.ClickAsync(new());
		var link = System.Text.Json.JsonSerializer.Deserialize<PlanForge.Contracts.Library.ResourceLinkRequest>(fixture.Body!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
		Assert.False(link.Attach);
		Assert.Equal(kind == "Project" ? fixture.ProjectId : null, link.ProjectId);
		Assert.Equal(kind == "Phase" ? itemId : null, link.PhaseId);
		Assert.Equal(kind == "Subphase" ? itemId : null, link.SubphaseId);
		Assert.Equal(kind == "Step" ? itemId : null, link.StepId);
		Assert.Equal(fixture.Resource.Revision, link.Revision);
	}

	[Fact]
	public async Task Resources_PlaceCleanupBeforeFilterActionsWithoutSkippingPreviewAsync()
	{
		using var fixture = new ResourcePanelFixture();
		var panel = fixture.Context.Render<ResourcePanel>();
		var buttons = panel.FindAll(".filter-bar button");
		Assert.Equal(new[] { "Clean orphan resources", "Apply filters", "List view" }, buttons.Select(button => button.TextContent));
		Assert.Contains("danger", buttons[0].ClassList);
		Assert.All(buttons, button => Assert.Equal("button", button.GetAttribute("type")));
		Assert.False(buttons[0].HasAttribute("disabled"));
		await buttons[0].ClickAsync(new());
		Assert.Single(panel.FindComponents<ConfirmDialog>());
		Assert.Empty(fixture.Commands);
		Assert.True(panel.Find("#confirm-delete").HasAttribute("disabled"));
	}

	[Fact]
	public async Task Resources_IsolatedDeleteIsExplicitButtonAndStillRequiresConfirmationAsync()
	{
		using var fixture = new ResourcePanelFixture();
		var panel = fixture.Context.Render<ResourcePanel>();
		await panel.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		var delete = panel.Find(".danger-zone button");
		Assert.Equal("button", delete.GetAttribute("type"));
		Assert.Contains("danger", delete.ClassList);
		Assert.False(delete.HasAttribute("disabled"));
		await delete.ClickAsync(new());
		Assert.Single(panel.FindComponents<ConfirmDialog>());
		Assert.Empty(fixture.Commands);
	}

	[Fact]
	public void Confirmation_PlacesDestructiveActionBeforeSafeCancel()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var dialog = context.Render<ConfirmDialog>();
		var actions = dialog.FindAll(".form-actions button");
		Assert.Equal("confirm-delete", actions[0].Id);
		Assert.Equal("Cancel", actions[1].TextContent);
		Assert.All(actions, button => Assert.Equal("button", button.GetAttribute("type")));
		Assert.True(actions[0].HasAttribute("disabled"));
		Assert.False(actions[1].HasAttribute("disabled"));
	}
}
