namespace PlanForge.Api.Tests;

using PlanForge.Api.Exchange;
using PlanForge.Application.Accounts;
using PlanForge.Application.Exchange;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Net;
using System.Text;

public sealed class WorkPlanFunctionTests
{
	[Fact]
	public async Task WorkPlanAsync_WhenExportedEditedAndImported_ReturnsCsvDownloadAndEnforcesRevisionAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		var functions = new WorkPlanFunctions(new WorkPlanExchange(projects, store), projects, fixture.Service, fixture.Runner);
		var export = new HttpTestRequest(method: "GET");
		export.Headers.Add("Authorization", "Bearer " + grant.Token);
		var downloaded = await functions.ExportAsync(export, project.Id);
		Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
		Assert.StartsWith("text/csv", downloaded.Headers.GetValues("Content-Type").Single());
		Assert.Contains("work-plan.csv", downloaded.Headers.GetValues("Content-Disposition").Single());
		ProjectSnapshot edited = WorkPlanCsvCodec.Parse(((MemoryStream)downloaded.Body).ToArray()) with { Description = "HTTP import; ñ" };
		var import = new HttpTestRequest(Encoding.UTF8.GetString(WorkPlanCsvCodec.Serialize(edited)));
		import.Headers.Add("Authorization", "Bearer " + grant.Token);
		Assert.Equal(HttpStatusCode.OK, (await functions.ImportAsync(import, project.Id)).StatusCode);
		Assert.Equal(edited.Description, (await projects.GetAsync(actor, project.Id)).Description);
		import.Body.Position = 0;
		Assert.Equal(HttpStatusCode.Conflict, (await functions.ImportAsync(import, project.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.ExportAsync(new HttpTestRequest(method: "GET"), project.Id)).StatusCode);
	}
}
