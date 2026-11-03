namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Exchange;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Planning;

public sealed class WorkPlanExchangeTests
{
	[Fact]
	public async Task ExchangeAsync_WhenAgentEditsCsv_ImportsAtomicallyEmitsExactEventsAndRejectsStaleOrForeignAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = (await fixture.Service.AuthenticateAsync(a.Token)) with { Kind = CredentialKind.AgentToken };
		AuthenticatedActor other = await fixture.Service.AuthenticateAsync(b.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan ñ;"));
		var exchange = new WorkPlanExchange(projects, store);

		byte[] csv = await exchange.ExportAsync(actor, project.Id);
		ProjectSnapshot edited = WorkPlanCsvCodec.Parse(csv) with { Description = "Edited; España" };
		ProjectSnapshot imported = await exchange.ImportAsync(actor, project.Id, WorkPlanCsvCodec.Serialize(edited));

		Assert.Equal(2, imported.Revision);
		Assert.Equal(edited.Description, (await projects.GetAsync(actor, project.Id)).Description);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => exchange.ImportAsync(actor, project.Id, csv))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => exchange.ImportAsync(other, project.Id, [0xFF]))).StatusCode);
		Assert.Equal(new[] { "Project exported successfully by agent", "CSV imported successfully by agent" }, fixture.Database.ProjectChanges.OrderBy(change => change.Id).Select(change => change.Action).ToArray());
		Assert.All(fixture.Database.ProjectChanges, change => Assert.Equal("Agent", change.ActorKind));
	}
}
