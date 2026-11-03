namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;

public sealed class LibrarySecurityTests
{
	[Fact]
	public async Task LibraryAsync_WhenForeignIdsKnown_DeniesReadWriteLinksAndNeverCleansAssignedOrOtherOwnersAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		AuthenticatedActor other = await fixture.Service.AuthenticateAsync(b.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		ResourceProfile resource = await library.UploadAsync(actor, new ResourceUploadRequest("private.bin", "Private", "application/octet-stream", project.Id), [1, 2, 3]);
		Assert.Empty(await library.ListAsync(other));
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => library.DownloadAsync(other, resource.Id))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UpdateAsync(other, resource.Id, new(resource.Revision, "Attack")))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => library.DeleteAsync(other, resource.Id, resource.Revision, true))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UploadAsync(other, new ResourceUploadRequest("attack.bin", "Attack", "application/octet-stream", project.Id), [9]))).StatusCode);
		ResourceProfile orphan = await library.UploadAsync(other, new ResourceUploadRequest("orphan.bin", "Other", "application/octet-stream"), [9]);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => library.SetLinkAsync(other, orphan.Id, new(orphan.Revision, true, ProjectId: project.Id)))).StatusCode);
		Assert.Equal(0, await library.CleanOrphansAsync(actor, new(true, [resource.Id, orphan.Id])));
		Assert.Equal(new byte[] { 1, 2, 3 }, (await library.DownloadAsync(actor, resource.Id)).Content);
		Assert.Equal(new byte[] { 9 }, (await library.DownloadAsync(other, orphan.Id)).Content);
		Assert.Equal(2, (await projects.GetAsync(actor, project.Id)).Revision);
	}

	[Fact]
	public void SafeFileName_WhenOverLimit_RejectsInsteadOfSilentlyTruncatingOriginalName()
	{
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => LibraryService.SafeFileName(new string('x', 181))).StatusCode);
	}

	[Fact]
	public async Task UploadAsync_WhenTypeSizeOrQuotaInvalid_RejectsWithoutPartialRowsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(8, 5, 10), TimeProvider.System);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UploadAsync(actor, new("x.html", "Unsafe", "text/html"), [1]))).StatusCode);
		Assert.Equal(413, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UploadAsync(actor, new("x.bin", "Oversize", "application/octet-stream"), new byte[9]))).StatusCode);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UploadAsync(actor, new("x.bin", "Empty", "application/octet-stream"), []))).StatusCode);
		await library.UploadAsync(actor, new("first.bin", "First", "application/octet-stream"), [1, 2, 3]);
		Assert.Equal(413, (await Assert.ThrowsAsync<PlanForgeException>(() => library.UploadAsync(actor, new("next.bin", "Quota", "application/octet-stream"), [4, 5, 6]))).StatusCode);
		Assert.Single(await library.ListAsync(actor));
		Assert.Empty(fixture.Database.ProjectResources);
		Assert.Empty(fixture.Database.PhaseResources);
	}
}
