namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class NotificationTests
{
	[Theory]
	[InlineData(NotificationKind.Success, "success", "check")]
	[InlineData(NotificationKind.Neutral, "neutral", "info")]
	[InlineData(NotificationKind.Danger, "danger", "circle-x")]
	public void Toast_WhenTyped_RendersSemanticVariantAndSingleAccessibleAnnouncement(NotificationKind kind, string variant, string icon)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var view = context.Render<ToastHost>(p => p.Add(c => c.Items, [new ToastNotification("Explicit result", kind)]));
		var toast = view.Find(".toast");
		Assert.Contains("toast-" + variant, toast.ClassList);
		Assert.Null(toast.GetAttribute("role"));
		Assert.Equal("status", Assert.Single(view.FindAll(".toast-announcer")).GetAttribute("role"));
		Assert.Equal(icon, toast.QuerySelector("svg")!.GetAttribute("data-icon"));
		Assert.Equal("true", toast.QuerySelector("svg")!.GetAttribute("aria-hidden"));
		Assert.Contains("Explicit result", toast.TextContent);
		Assert.Empty(view.FindAll("[aria-live]"));
	}
	[Fact]
	public void CircuitState_WhenPublishing_DeduplicatesBySemanticsCapsAtFourAndIsolatesSessions()
	{
		using var state = new NotificationState();
		using var otherCircuit = new NotificationState();
		state.Publish("First", NotificationKind.Success);
		state.Publish("First", NotificationKind.Success);
		Assert.Single(state.Items);
		Assert.Empty(otherCircuit.Items);
		state.Publish("First", NotificationKind.Danger);
		Assert.Equal(2, state.Items.Count);
		state.Publish("Second", NotificationKind.Neutral);
		state.Publish("Third", NotificationKind.Success);
		state.Publish("Fourth", NotificationKind.Success);
		Assert.Equal(4, state.Items.Count);
		Assert.Equal(NotificationKind.Danger, state.Items[0].Kind);
	}
	[Fact]
	public async Task Page_WhenNavigatedAway_LeavesNotificationInCircuitAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		context.Services.AddScoped<NotificationState>();
		var page = context.Render<NotificationProbe>();
		await page.InvokeAsync(() => page.Instance.Publish());
		var state = context.Services.GetRequiredService<NotificationState>();
		Assert.Single(state.Items);
		await page.Instance.DisposeAsync();
		Assert.Single(state.Items);
	}

	[Fact]
	public async Task GlobalOperationFailure_UsesDangerWhileFormValidationRemainsInlineAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var probe = context.Render<NotificationProbe>();
		await probe.InvokeAsync(() => probe.Instance.FailAsync());
		Assert.Equal(NotificationKind.Danger, Assert.Single(context.Services.GetRequiredService<NotificationState>().Items).Kind);
	}

	[Fact]
	public async Task ProjectCreation_PublishesConfirmedSuccessBeforeNavigationAsync()
	{
		var project = new PlanForge.Contracts.Planning.ProjectSnapshot(Guid.NewGuid(), 1, "Created", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(request.Method == HttpMethod.Post ? (object)project : new PlanForge.Contracts.Planning.ProjectSummaryPage([], 0, 0, 100, false))));
		var page = context.Render<PlanForge.Web.Components.Pages.Home>(p => p.Add(c => c.IsManagement, true));
		await page.FindAll("button").First(button => button.TextContent.Contains("New project", StringComparison.Ordinal)).ClickAsync(new());
		page.Find("dialog input").Change("Created");
		await page.Find("dialog form").SubmitAsync();
		var toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal("Project created", toast.Message);
		Assert.Equal(NotificationKind.Success, toast.Kind);
	}

	private sealed class NotificationProbe : ApiPage
	{
		public void Publish() => Notify("Saved successfully");
		public Task FailAsync() => ExecuteAsync(() => throw new HttpRequestException("Controlled transport failure"));
	}
}
