namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class PlanPersistenceTests
{
	[Theory]
	[InlineData("missing-subphase")]
	[InlineData("wrong-parent")]
	[InlineData("duplicate-id")]
	[InlineData("invalid-status")]
	[InlineData("duplicate-order")]
	[InlineData("blank-title")]

	[InlineData("decision-without-resolution")]
	[InlineData("invalid-evidence")]
	public async Task SaveAsync_WhenInvalidSnapshot_RejectsBeforeAnyWriteAsync(string invalid)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Plan"));
		Guid phase = Guid.NewGuid();
		Guid subphase = Guid.NewGuid();
		var items = new List<PlanItem> { new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase") };
		switch (invalid)
		{
			case "missing-subphase": items.RemoveAt(1); break;
			case "wrong-parent": items[1] = items[1] with { ParentId = Guid.NewGuid() }; break;
			case "duplicate-id": items.Add(items[1]); break;
			case "invalid-status": items[1] = items[1] with { Status = "93" }; break;
			case "duplicate-order": items.Add(items[1] with { Id = Guid.NewGuid() }); break;
			case "blank-title": items[1] = items[1] with { Title = " " }; break;

			case "decision-without-resolution": items.Add(new(Guid.NewGuid(), "Decision", phase, 0, "Question?", Status: "Completed")); break;
			case "invalid-evidence": items.Add(new(Guid.NewGuid(), "Step", subphase, 0, "Step", Evidence: "Invented")); break;
		}

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, project.Id, project with { Items = items }));

		Assert.Equal(400, error.StatusCode);
		Assert.Equal(1, (await service.GetAsync(actor, project.Id)).Revision);
		Assert.Empty(fixture.Database.WorkItems);
	}

	[Fact]
	public async Task SaveAsync_WhenValidHierarchy_PersistsStableIdsAndIncrementsOnceAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Plan"));
		Guid phase = Guid.NewGuid();
		Guid subphase = Guid.NewGuid();
		Guid step = Guid.NewGuid();
		ProjectSnapshot edit = project with { Items = [new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "ñ; \"quoted\"\nline")] };

		ProjectSnapshot saved = await service.SaveAsync(actor, project.Id, edit);

		Assert.Equal(2, saved.Revision);
		Assert.Equal(edit.Items.Select(item => item.Id).Order(), (await service.GetAsync(actor, project.Id)).Items.Select(item => item.Id).Order());
		Assert.Equal(edit.Items.Last().Title, saved.Items.Single(item => item.Id == step).Title);
		Assert.Equal(3, fixture.Database.WorkItems.Count());
		var change = Assert.Single(fixture.Database.ProjectChanges);
		Assert.Equal(2, change.Revision);
		Assert.Equal("Human", change.ActorKind);
		Assert.Equal("Project updated", change.Action);
		Assert.Equal(actor.UserId, change.ActorId);
	}
}
