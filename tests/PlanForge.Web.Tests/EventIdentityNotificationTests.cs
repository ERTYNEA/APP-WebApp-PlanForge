namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class EventIdentityNotificationTests
{
	[Fact]
	public async Task DistinctSameActorSameRevisionSameTextEvents_RemainSeparateAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var probe = context.Render<EventProbe>();
		var first = new ProjectChangeInfo(10, 5, Guid.NewGuid(), "Human", "Same actor", "Project exported successfully", DateTimeOffset.UtcNow) { Operation = ProjectChangeOperation.Export };
		await probe.InvokeAsync(() => { probe.Instance.Publish(first); probe.Instance.Publish(first with { Id = 11 }); });
		Assert.Equal(2, context.Services.GetRequiredService<NotificationState>().Items.Count);
		await probe.InvokeAsync(() => probe.Instance.Publish(first));
		Assert.Equal(2, context.Services.GetRequiredService<NotificationState>().Items.Count);
	}

	[Fact]
	public void ExportAcknowledgement_SuppressesOnlyExactIdentityAcrossPagedObservations()
	{
		var cursor = new ProjectChangeCursor();
		cursor.ObserveEvents(new(5, 1, false, []));
		cursor.AcknowledgeEvent(3);
		var first = new ProjectChangeInfo(2, 5, Guid.NewGuid(), "Human", "Same actor", "Project exported successfully", DateTimeOffset.UtcNow) { Operation = ProjectChangeOperation.Export };
		Assert.Equal(2, Assert.Single(cursor.ObserveEvents(new(5, 2, false, [first]))).Id);
		Assert.Empty(cursor.ObserveEvents(new(5, 3, false, [first with { Id = 3 }])));
		Assert.Equal(4, Assert.Single(cursor.ObserveEvents(new(5, 4, false, [first with { Id = 4 }]))).Id);
	}

	private sealed class EventProbe : ApiPage
	{
		public void Publish(ProjectChangeInfo change) => Notify(change);
	}
}
