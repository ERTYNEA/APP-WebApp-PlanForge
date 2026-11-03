namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Planning;

public sealed class WorkflowTransitionTests
{
	[Theory]
	[InlineData("PlanningOnly", "WorkDone", true, true)]
	[InlineData("LocalChangesManualApproval", "WorkDone", true, false)]
	[InlineData("LocalChangesManualApproval", "OwnerApproved", true, true)]
	[InlineData("LocalChangesManualApproval", "OwnerApproved", false, false)]
	[InlineData("BranchPullRequest", "WorkDone", false, false)]
	[InlineData("BranchPullRequest", "MergeConfirmed", false, true)]
	[InlineData("DirectToMain", "WorkDone", false, false)]
	[InlineData("DirectToMain", "PushConfirmed", false, true)]
	public async Task SaveAsync_WhenStepClosed_EnforcesRealWorkflowEvidenceAsync(string workflow, string evidence, bool human, bool allowed)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		if (!human)
			actor = actor with { Kind = CredentialKind.AgentToken };
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Plan", "", workflow));
		Guid phase = Guid.NewGuid();
		Guid subphase = Guid.NewGuid();
		Guid step = Guid.NewGuid();
		project = project with { Items = [new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "Step", Status: "Completed", Evidence: evidence)] };
		if (allowed)
		{
			ProjectSnapshot saved = await service.SaveAsync(actor, project.Id, project);
			PlanItem completed = saved.Items.Single(item => item.Id == step);
			Assert.Equal(evidence, completed.Evidence);
			Assert.Equal(actor.UserId, completed.CompletedBy);
			Assert.NotNull(completed.CompletedAt);
		}
		else
		{
			Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, project.Id, project))).StatusCode);
			Assert.Equal(1, (await service.GetAsync(actor, project.Id)).Revision);
		}
	}
}
