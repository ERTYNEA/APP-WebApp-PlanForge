namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

/// <summary>Closes operation/type coverage gaps without deriving semantics from display text.</summary>
public sealed class NotificationSemanticAuditTests
{
	[Theory]
	[InlineData(ProjectChangeOperation.Informational, NotificationKind.Neutral)]
	[InlineData(ProjectChangeOperation.Destructive, NotificationKind.Danger)]
	[InlineData(ProjectChangeOperation.Import, NotificationKind.Success)]
	[InlineData(ProjectChangeOperation.Export, NotificationKind.Success)]
	public async Task RemoteEvent_UsesStructuredOperationAndActualActorAsync(ProjectChangeOperation operation, NotificationKind expected)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var probe = context.Render<EventProbe>();
		var change = new ProjectChangeInfo(17, 3, Guid.NewGuid(), "Agent", "Actual collaborator", "Project updated", DateTimeOffset.UtcNow) { Operation = operation };
		await probe.InvokeAsync(() => probe.Instance.Publish(change));
		ToastNotification toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(expected, toast.Kind);
		Assert.Equal("Project updated — Actual collaborator", toast.Message);
	}

	[Theory]
	[InlineData(0, 0, NotificationKind.Neutral)]
	[InlineData(0, 2, NotificationKind.Neutral)]
	[InlineData(2, 0, NotificationKind.Danger)]
	[InlineData(2, 1, NotificationKind.Danger)]
	public async Task OrphanCleanup_UsesRealCountsAndEmitsOneTruthfulResultAsync(int deleted, int skipped, NotificationKind expected)
	{
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(
			request.Method == HttpMethod.Post ? (object)new { deletedCount = deleted, skippedCount = skipped }
			: request.RequestUri!.AbsolutePath.EndsWith("/usage", StringComparison.Ordinal) ? new ResourceUsage(0, 10000, 1024, false)
			: new ResourcePage([], 0, 0, 50))));
		var panel = context.Render<ResourcePanel>();
		await panel.FindAll("button").Single(button => button.TextContent == "Clean orphan resources").ClickAsync(new());
		panel.Find("#confirm-intent").Change(true);
		await panel.Find("#confirm-delete").ClickAsync(new());
		ToastNotification toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(expected, toast.Kind);
		Assert.Equal($"Deleted {deleted} orphan resources; skipped {skipped}.", toast.Message);
	}

	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	[InlineData("Decision")]
	public async Task StructuralDelete_ReportsExactKindAsDangerAsync(string kind)
	{
		Guid projectId = Guid.NewGuid(), phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid(), decision = Guid.NewGuid();
		var project = new ProjectSnapshot(projectId, 5, "Audit plan", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "Phase", Status: "InProgress"), new(subphase, "Subphase", phase, 0, "Subphase", Status: "InProgress"), new(step, "Step", subphase, 0, "Step", Status: "InProgress"), new(decision, "Decision", step, 0, "Decision")]);
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Delete)
				project = project with { Revision = 6, Items = [] };
			string path = request.RequestUri!.AbsolutePath;
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>()
				: path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(project.Revision, 1, false, []) : project));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, projectId));
		if (kind is "Phase" or "Subphase")
		{
			Guid selected = kind == "Phase" ? phase : subphase;
			await page.Find($"button[data-select-id='{selected}']").ClickAsync(new());
		}
		string label = "Delete " + kind.ToLowerInvariant();
		await page.FindAll("button").Single(button => button.TextContent == label).ClickAsync(new());
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		ToastNotification toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(NotificationKind.Danger, toast.Kind);
		Assert.Equal(kind + " deleted", toast.Message);
	}

	[Theory]
	[InlineData(false, NotificationKind.Neutral, "No completed phases were removed")]
	[InlineData(true, NotificationKind.Danger, "Completed phases removed")]
	public async Task CompletedPhaseCleanup_ClassifiesActualRemovalAsync(bool removed, NotificationKind expected, string message)
	{
		Guid projectId = Guid.NewGuid();
		var project = new ProjectSnapshot(projectId, 5, "Audit plan", "", "Completed", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(Guid.NewGuid(), "Phase", null, 0, "Completed phase", Status: "Completed")]);
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Post)
				project = project with { Revision = 6, Items = removed ? [] : project.Items };
			string path = request.RequestUri!.AbsolutePath;
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>()
				: path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(project.Revision, 1, false, []) : project));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, projectId));
		await page.FindAll("button").Single(button => button.TextContent == "Settings").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Clean completed phases").ClickAsync(new());
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		ToastNotification toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(expected, toast.Kind);
		Assert.Equal(message, toast.Message);
	}

	private sealed class EventProbe : ApiPage
	{
		public void Publish(ProjectChangeInfo change) => Notify(change);
	}
}
