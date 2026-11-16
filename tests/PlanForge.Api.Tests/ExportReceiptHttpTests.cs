namespace PlanForge.Api.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Exchange;
using PlanForge.Application.Exchange;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;
using System.Net;

[Collection("Project archive budget")]
public sealed class ExportReceiptHttpTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SuccessfulExport_ReceiptsExactCommittedEventAndNeverOnOwnerFailureAsync(bool archive)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("export-receipt@example.test", "CorrectPassword123!", "QA"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		var project = await projects.CreateAsync(actor, new("Receipt fixture"));
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + grant.Token);
		var response = archive
			? await new ProjectExportFunctions(new ProjectArchiveExporter(projects, new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System), store), fixture.Service, fixture.Runner).ExportAsync(request, project.Id)
			: await new WorkPlanFunctions(new WorkPlanExchange(projects, store), projects, fixture.Service, fixture.Runner).ExportAsync(request, project.Id);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var committed = await fixture.Database.ProjectChanges.SingleAsync(change => change.Action == "Project exported successfully");
		Assert.True(response.Headers.TryGetValues("X-PlanForge-Change-Id", out var receipts));
		Assert.Equal(committed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Assert.Single(receipts));
		var failure = archive
			? await new ProjectExportFunctions(new ProjectArchiveExporter(projects, new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System), store), fixture.Service, fixture.Runner).ExportAsync(request, Guid.NewGuid())
			: await new WorkPlanFunctions(new WorkPlanExchange(projects, store), projects, fixture.Service, fixture.Runner).ExportAsync(request, Guid.NewGuid());
		Assert.Equal(HttpStatusCode.NotFound, failure.StatusCode);
		Assert.False(failure.Headers.Contains("X-PlanForge-Change-Id"));
		Assert.Equal(1, await fixture.Database.ProjectChanges.CountAsync(change => change.Action == "Project exported successfully"));
	}
}
