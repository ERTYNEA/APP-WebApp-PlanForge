namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Planning;

public sealed class AgentDeletionTests
{
	[Fact]
	public async Task SaveAsync_WhenAuthenticatedUnassignedAgentWasDeleted_RetainsRealInFlightActorNameAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("inflight-history@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("In-flight historical author"));
		var token = await agents.CreateTokenAsync(owner, new("Linked", agent.Id));
		var acceptedActor = await fixture.Service.AuthenticateAsync(token.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await projects.CreateAsync(owner, new("No assignment revision gate"));
		await agents.DeleteAsync(owner, agent.Id);

		await projects.SaveAsync(acceptedActor, project.Id, project with { Description = "Accepted request completed after deletion" });

		fixture.Database.ChangeTracker.Clear();
		var change = Assert.Single((await projects.GetChangesAsync(owner, project.Id)).Changes);
		Assert.Equal("In-flight historical author (deleted agent)", change.ActorName);
		Assert.Equal(agent.Id, change.ActorId);
		Assert.Equal("Accepted request completed after deletion", (await projects.GetAsync(owner, project.Id)).Description);
		Assert.Empty(await agents.ListAsync(owner));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExportAsync_WhenAcceptedAgentDeleted_RetainsHistoricalActorForLateEventAsync(bool archive)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("inflight-export@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Accepted export author"));
		var token = await agents.CreateTokenAsync(owner, new("Linked", agent.Id));
		var acceptedActor = await fixture.Service.AuthenticateAsync(token.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		var project = await projects.CreateAsync(owner, new("Retained export"));
		await agents.DeleteAsync(owner, agent.Id);

		if (archive)
			await new PlanForge.Application.Exchange.ProjectArchiveExporter(projects, new PlanForge.Application.Library.LibraryService(new PlanForge.Infrastructure.Library.EfLibraryStore(fixture.Database), new PlanForge.Application.Library.ResourceLimits(), TimeProvider.System), store).ExportAsync(acceptedActor, project.Id);
		else
			await new PlanForge.Application.Exchange.WorkPlanExchange(projects, store).ExportAsync(acceptedActor, project.Id);

		Assert.Equal("Accepted export author (deleted agent)", Assert.Single((await projects.GetChangesAsync(owner, project.Id)).Changes).ActorName);
		Assert.Equal(project.Revision, (await projects.GetAsync(owner, project.Id)).Revision);
	}

	[Fact]
	public async Task ResourceAsync_WhenAcceptedAgentDeleted_RetainsHistoricalActorForLateTouchAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("inflight-resource@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Accepted resource author"));
		var token = await agents.CreateTokenAsync(owner, new("Linked", agent.Id));
		var acceptedActor = await fixture.Service.AuthenticateAsync(token.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await projects.CreateAsync(owner, new("Retained resource event"));
		await agents.DeleteAsync(owner, agent.Id);
		var library = new PlanForge.Application.Library.LibraryService(new PlanForge.Infrastructure.Library.EfLibraryStore(fixture.Database), new PlanForge.Application.Library.ResourceLimits(), TimeProvider.System);

		await library.UploadAsync(acceptedActor, new("qa.txt", "QA", "text/plain", ProjectId: project.Id), System.Text.Encoding.UTF8.GetBytes("QA"));

		Assert.Equal("Accepted resource author (deleted agent)", Assert.Single((await projects.GetChangesAsync(owner, project.Id)).Changes).ActorName);
		Assert.Equal(project.Revision + 1, (await projects.GetAsync(owner, project.Id)).Revision);
	}

	[Fact]
	public async Task DeleteAsync_WhenFinalRelationalDeleteFails_RollsBackCredentialsAssignmentsHistoryAndRevisionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("rollback-delete@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Rollback author"));
		var linked = await agents.CreateTokenAsync(owner, new("Survivor", agent.Id));
		var actor = await fixture.Service.AuthenticateAsync(linked.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(owner, new("Rollback work"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		project = await projects.SaveAsync(actor, project.Id, project with { Items = [new(phase, "Phase", null, 0, "Phase", AssignmentMode: "Override", AssignedAgentId: agent.Id), new(subphase, "Subphase", phase, 0, "Subphase")] });
		string? beforeSnapshot = (await fixture.Database.ProjectChanges.SingleAsync()).ActorNameSnapshot;
		await fixture.Database.Database.ExecuteSqlRawAsync("CREATE TEMP TRIGGER block_agent_delete BEFORE DELETE ON Agents BEGIN SELECT RAISE(ABORT, 'Controlled relational failure'); END;");

		await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => agents.DeleteAsync(owner, agent.Id));

		fixture.Database.ChangeTracker.Clear();
		Assert.True((await fixture.Database.Agents.SingleAsync()).IsActive);
		Assert.Equal(agent.Id, (await fixture.Service.AuthenticateAsync(linked.Token)).AgentId);
		Assert.Equal(project.Revision, (await projects.GetAsync(owner, project.Id)).Revision);
		Assert.Equal(agent.Id, (await projects.GetAsync(owner, project.Id)).Items.Single(item => item.Id == phase).AssignedAgentId);
		Assert.Equal(beforeSnapshot, (await fixture.Database.ProjectChanges.SingleAsync()).ActorNameSnapshot);
		Assert.Equal("Rollback author", Assert.Single((await projects.GetChangesAsync(owner, project.Id)).Changes).ActorName);
	}

	[Fact]
	public async Task UpdateAsync_WhenStaleAfterDeactivation_RejectsInsteadOfResurrectingCredentialsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("stale-agent@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Stale agent"));
		var linked = await agents.CreateTokenAsync(owner, new("Linked", agent.Id));
		await using var stale = new PlanForge.Infrastructure.Database.PlanForgeDbContext(new DbContextOptionsBuilder<PlanForge.Infrastructure.Database.PlanForgeDbContext>().UseSqlite(fixture.Database.Database.GetDbConnection()).Options);
		var staleStore = new EfAgentStore(stale);
		Assert.NotNull(await staleStore.GetAsync(owner.UserId, agent.Id, default));
		await agents.UpdateAsync(owner, agent.Id, new("Stale agent", IsActive: false));
		var staleService = new AgentService(staleStore, new SecretProtector(), TimeProvider.System);

		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleService.UpdateAsync(owner, agent.Id, new("Stale edit", IsActive: true)));

		fixture.Database.ChangeTracker.Clear();
		Assert.False((await fixture.Database.Agents.SingleAsync()).IsActive);
		Assert.Equal("Stale agent", (await fixture.Database.Agents.SingleAsync()).Name);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(linked.Token))).StatusCode);
	}

	[Fact]
	public async Task DeleteAsync_WhenActorHasRetainedEvents_PreservesHistoricalNameWithoutOperationalRecordAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("history@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Historical author"));
		var token = await agents.CreateTokenAsync(owner, new("Linked", agent.Id));
		var actor = await fixture.Service.AuthenticateAsync(token.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await projects.CreateAsync(owner, new("Retained"));
		await projects.SaveAsync(actor, project.Id, project with { Description = "Actually changed by the agent" });

		await agents.DeleteAsync(owner, agent.Id);

		fixture.Database.ChangeTracker.Clear();
		var change = Assert.Single((await projects.GetChangesAsync(owner, project.Id)).Changes);
		Assert.Equal("Historical author (deleted agent)", change.ActorName);
		Assert.Equal(agent.Id, change.ActorId);
		Assert.Equal("Project updated", change.Action);
		Assert.Empty(await agents.ListAsync(owner));
		Assert.Equal("Actually changed by the agent", (await projects.GetAsync(owner, project.Id)).Description);
	}

	[Fact]
	public async Task DeleteAsync_WhenAssigned_DetachesOnlyOverridesAndAdvancesAffectedProjectOnceAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("delete@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var removed = await agents.CreateAsync(actor, new("Removed"));
		var survivor = await agents.CreateAsync(actor, new("Parent"));
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Preserved work"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), inherited = Guid.NewGuid(), explicitNone = Guid.NewGuid();
		project = await projects.SaveAsync(actor, project.Id, project with
		{
			Items = [
			new(phase, "Phase", null, 0, "Phase", AssignmentMode: "Override", AssignedAgentId: survivor.Id),
			new(subphase, "Subphase", phase, 0, "Subphase", AssignmentMode: "Override", AssignedAgentId: removed.Id),
			new(inherited, "Step", subphase, 0, "Inherited"),
			new(explicitNone, "Step", subphase, 1, "Explicit none", AssignmentMode: "Unassigned")
		]
		});
		ProjectSnapshot untouched = await projects.CreateAsync(actor, new("Unrelated"));

		await agents.DeleteAsync(actor, removed.Id);

		fixture.Database.ChangeTracker.Clear();
		ProjectSnapshot saved = await projects.GetAsync(actor, project.Id);
		Assert.Equal(project.Revision + 1, saved.Revision);
		Assert.Equal(project.Items.Count, saved.Items.Count);
		Assert.Equal("Inherit", saved.Items.Single(item => item.Id == subphase).AssignmentMode);
		Assert.Null(saved.Items.Single(item => item.Id == subphase).AssignedAgentId);
		Assert.Equal("Inherit", saved.Items.Single(item => item.Id == inherited).AssignmentMode);
		Assert.Equal("Unassigned", saved.Items.Single(item => item.Id == explicitNone).AssignmentMode);
		Assert.Equal(survivor.Id, saved.Items.Single(item => item.Id == phase).AssignedAgentId);
		Assert.Equal(untouched.Revision, (await projects.GetAsync(actor, untouched.Id)).Revision);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => projects.SaveAsync(actor, project.Id, project))).StatusCode);
		Assert.Single(await agents.ListAsync(actor));
		Assert.Equal(2, await fixture.Database.ProjectChanges.CountAsync(change => change.ProjectId == project.Id));
	}
}
