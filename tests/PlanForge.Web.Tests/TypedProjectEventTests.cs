namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Planning;
using PlanForge.Web.State;

public sealed class TypedProjectEventTests
{
	[Fact]
	public void Cursor_PreservesTypedEventsActorsAndIdsWithoutReplayingLocalMutation()
	{
		var cursor = new ProjectChangeCursor();
		Assert.Empty(cursor.ObserveEvents(new(1, 1, false, [])));
		cursor.AcknowledgeMutation(2);
		ProjectChangeInfo Change(long id, long revision, ProjectChangeOperation operation) => new(id, revision, Guid.NewGuid(), "Agent", "NEA", "Arbitrary user-like wording: delete", DateTimeOffset.UtcNow) { Operation = operation };
		ProjectChangeInfo remote = Change(3, 3, ProjectChangeOperation.Destructive);
		ProjectChangeInfo export = Change(4, 3, ProjectChangeOperation.Export);
		var events = cursor.ObserveEvents(new(3, 4, false, [Change(2, 2, ProjectChangeOperation.Informational), remote, export]));
		Assert.Equal(new[] { remote, export }, events);
		Assert.Equal(remote.ActorId, events[0].ActorId);
		Assert.Equal(ProjectChangeOperation.Destructive, events[0].Operation);
		Assert.Empty(cursor.ObserveEvents(new(3, 4, false, [remote, export])));
		Assert.Empty(cursor.ObserveEvents(new(10, 99, true, [Change(99, 10, ProjectChangeOperation.Import)])));
		Assert.Equal(99, cursor.After);
	}
}
