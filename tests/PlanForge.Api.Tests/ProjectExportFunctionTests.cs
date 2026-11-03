namespace PlanForge.Api.Tests;

using PlanForge.Api.Exchange;
using PlanForge.Application.Accounts;
using PlanForge.Application.Exchange;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;
using System.IO.Compression;
using System.Net;

public sealed class ProjectExportFunctionTests
{
	[Fact]
	public async Task ExportAsync_WhenOwned_ReturnsRealZipAndRejectsUnauthenticatedOrForeignAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var second = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		var functions = new ProjectExportFunctions(new ProjectArchiveExporter(projects, new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System), store), fixture.Service, fixture.Runner);
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + grant.Token);
		var response = await functions.ExportAsync(request, project.Id);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("application/zip", response.Headers.GetValues("Content-Type").Single());
		using var archive = new ZipArchive(new MemoryStream(((MemoryStream)response.Body).ToArray()));
		Assert.NotNull(archive.GetEntry("work-plan.csv"));
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.ExportAsync(new HttpTestRequest(method: "GET"), project.Id)).StatusCode);
		var foreign = new HttpTestRequest(method: "GET");
		foreign.Headers.Add("Authorization", "Bearer " + second.Token);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.ExportAsync(foreign, project.Id)).StatusCode);
	}
}
