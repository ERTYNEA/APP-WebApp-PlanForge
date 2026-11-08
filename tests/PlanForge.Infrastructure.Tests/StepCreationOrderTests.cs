namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Exchange;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Text.Json;

public sealed class StepCreationOrderTests
{
	[Fact]
	public async Task SaveAsync_CreatesPersistentAgeUnaffectedByEditReorderOrCsvAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("age@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Oldest", subphase));
		PlanItem oldest = project.Items.Single(item => item.Kind == "Step");
		long age = Order(oldest);
		Assert.True(age > 0);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Newest", subphase));
		Assert.True(Order(project.Items.Single(item => item.Title == "Newest")) > age);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", oldest.Id, new(project.Revision, "Oldest edited", subphase, SortOrder: 1));
		Assert.Equal(age, Order(project.Items.Single(item => item.Id == oldest.Id)));
		ProjectSnapshot csv = WorkPlanCsvCodec.Parse(WorkPlanCsvCodec.Serialize(project));
		project = await service.SaveAsync(actor, project.Id, csv);
		Assert.Equal(age, Order(project.Items.Single(item => item.Id == oldest.Id)));
		Assert.Equal(age, Order((await service.GetAsync(actor, project.Id)).Items.Single(item => item.Id == oldest.Id)));
		project = await service.SaveAsync(actor, project.Id, project with { Items = project.Items.Select(item => item with { CreationOrder = long.MinValue }).ToList() });
		Assert.Equal(age, project.Items.Single(item => item.Id == oldest.Id).CreationOrder);
		project = await editor.SaveItemAsync(actor, project.Id, "Subphase", null, new(project.Revision, "Move target", project.Items.Single(item => item.Kind == "Phase").Id));
		Guid movedParent = project.Items.Single(item => item.Title == "Move target").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", oldest.Id, new(project.Revision, "Oldest moved", movedParent));
		Assert.Equal(age, project.Items.Single(item => item.Id == oldest.Id).CreationOrder);
		Guid first = Guid.Parse("00000001-0000-0000-0000-000000000001"), second = Guid.Parse("00000002-0000-0000-0000-000000000001");
		project = await service.SaveAsync(actor, project.Id, project with { Items = project.Items.Concat(new[] { new PlanItem(second, "Step", movedParent, 1, "Bulk second"), new PlanItem(first, "Step", movedParent, 2, "Bulk first") }).ToList() });
		Assert.Equal(project.Items.Single(item => item.Id == first).CreationOrder, project.Items.Single(item => item.Id == second).CreationOrder);
		for (int index = 0; index < 105; index++)
			await new EfProjectStore(fixture.Database).RecordEventAsync(actor.UserId, project.Id, project.Revision, actor.UserId, "Human", "Project exported successfully", default);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", oldest.Id, new(project.Revision, "Oldest after event pruning", movedParent));
		Assert.Equal(age, project.Items.Single(item => item.Id == oldest.Id).CreationOrder);
		ProjectSnapshot other = await service.CreateAsync(actor, new("Cross-project newer"));
		other = await editor.SaveItemAsync(actor, other.Id, "Phase", null, new(other.Revision, "Phase"));
		other = await editor.SaveItemAsync(actor, other.Id, "Step", null, new(other.Revision, "Newest across projects", other.Items.Single(item => item.Kind == "Subphase").Id));
		Assert.True(other.Items.Single(item => item.Kind == "Step").CreationOrder > project.Items.Single(item => item.Id == first).CreationOrder);
	}

	private static long Order(PlanItem item)
	{
		JsonElement json = JsonSerializer.SerializeToElement(item);
		Assert.True(json.TryGetProperty("CreationOrder", out JsonElement order), "A server-owned immutable creation order must be projected.");
		return order.GetInt64();
	}
}
