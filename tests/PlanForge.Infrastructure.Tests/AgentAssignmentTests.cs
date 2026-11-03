namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Domain.Agents;
using PlanForge.Infrastructure.Planning;

public sealed class AgentAssignmentTests
{
	[Fact]
	public async Task SaveAsync_WhenAgentBelongsToAnotherUser_RejectsKnownGuidBeforeWriteAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		var foreign = new Agent { OwnerUserId = b.User.Id, Name = "Foreign" };
		fixture.Database.Agents.Add(foreign);
		await fixture.Database.SaveChangesAsync();
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		Guid phase = Guid.NewGuid();
		ProjectSnapshot invalid = project with { Items = [new(phase, "Phase", null, 0, "Phase", AssignmentMode: "Override", AssignedAgentId: foreign.Id), new(Guid.NewGuid(), "Subphase", phase, 0, "Subphase")] };

		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => projects.SaveAsync(actor, project.Id, invalid))).StatusCode);
		Assert.Empty((await projects.GetAsync(actor, project.Id)).Items);
	}
}
