namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Planning;
using PlanForge.Web.State;

public sealed class ProjectChangeCursorTests
{
	[Fact]
	public void Observe_WhenExportDoesNotChangeRevision_StillDeliversNewExactActorToastOnlyOnce()
	{
		var cursor = new ProjectChangeCursor();
		ProjectChangeInfo Change(long id,string text,string kind="Agent") => new(id,2,Guid.NewGuid(),kind,"Test",text,DateTimeOffset.UtcNow);
		Assert.Empty(cursor.Observe(new(2,10,false,[Change(10,"Project updated")])));
		Assert.Equal(10,cursor.After);
		var page = new ProjectChangePage(2,11,false,[Change(11,"Project exported successfully by agent")]);
		Assert.Equal(new[]{"Project exported successfully by agent"},cursor.Observe(page));
		Assert.Empty(cursor.Observe(page));
		Assert.Equal(new[]{"CSV imported successfully by agent","Project updated"},cursor.Observe(new(3,13,false,[Change(12,"CSV imported successfully by agent"),Change(13,"Project updated","Human")])));
	}
}
