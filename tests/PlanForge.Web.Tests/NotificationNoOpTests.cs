namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class NotificationNoOpTests
{
	[Fact]
	public async Task TokenCleanup_WhenAnotherSessionAlreadyRemovedHistory_ReportsNeutralNoOpAsync()
	{
		var token = new PlanForge.Contracts.Agents.AgentTokenProfile(Guid.NewGuid(), "Controlled", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, null);
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(request.Method == HttpMethod.Post ? (object)new { deletedCount = 0 } : new[] { token })));
		var panel = context.Render<AgentTokenPanel>();
		await panel.FindAll("button").Single(button => button.TextContent == "Delete revoked tokens").ClickAsync(new());
		panel.Find("#confirm-intent").Change(true);
		await panel.Find("#confirm-delete").ClickAsync(new());
		ToastNotification notification = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(NotificationKind.Neutral, notification.Kind);
		Assert.Equal("No revoked or expired tokens were removed", notification.Message);
	}

	[Fact]
	public async Task AttachWithoutSelection_DoesNotSubmitOrClaimSuccessAsync()
	{
		using var fixture = new ResourcePanelFixture();
		var panel = fixture.Context.Render<ResourcePanel>(parameters => parameters.Add(component => component.ProjectId, fixture.ProjectId));
		await panel.FindAll("button").Single(button => button.TextContent == "Attach existing").ClickAsync(new());
		await panel.Find("#attach-resource-form").SubmitAsync();
		Assert.Empty(fixture.Commands);
		Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationState>().Items);
	}

	[Fact]
	public async Task UploadWithoutFile_DoesNotClaimSuccessAsync()
	{
		using var fixture = new ResourcePanelFixture();
		var panel = fixture.Context.Render<ResourcePanel>();
		await panel.FindAll("button").Single(button => button.TextContent == "Upload file").ClickAsync(new());
		await panel.Find("#upload-resource-form").SubmitAsync();
		Assert.Empty(fixture.Commands);
		Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationState>().Items);
	}
}
