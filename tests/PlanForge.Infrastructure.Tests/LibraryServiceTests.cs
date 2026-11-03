namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Application.Common;
using PlanForge.Domain.Agents;
using PlanForge.Contracts;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;
using System.Security.Cryptography;
using System.Text;

public sealed class LibraryServiceTests
{
	[Fact]
	public async Task ResourceLinks_WhenPhaseOrProjectDeleted_PreserveBinaryAndBecomeTrueOrphansAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		ResourceProfile phaseOnly = await service.UploadAsync(actor, new ResourceUploadRequest("phase.md", "Phase only", "text/markdown", PhaseId: phase), [1, 2, 3]);
		Assert.True(phaseOnly.IsAssigned);
		Assert.Empty(phaseOnly.Projects);
		Assert.Single(phaseOnly.Phases);
		Assert.Empty(fixture.Database.ProjectResources);
		project = await projects.GetAsync(actor, project.Id);
		project = await editor.DeleteItemAsync(actor, project.Id, "Phase", phase, new(project.Revision, true));
		Assert.False((await service.ListAsync(actor)).Single().IsAssigned);
		Assert.Equal(new byte[] { 1, 2, 3 }, (await service.DownloadAsync(actor, phaseOnly.Id)).Content);
		ResourceProfile direct = await service.UploadAsync(actor, new ResourceUploadRequest("project.md", "Direct", "text/markdown", ProjectId: project.Id), [4, 5, 6]);
		project = await projects.GetAsync(actor, project.Id);
		await projects.DeleteAsync(actor, project.Id, project.Revision, true);
		Assert.All(await service.ListAsync(actor), resource => Assert.False(resource.IsAssigned));
		Assert.Equal(new byte[] { 4, 5, 6 }, (await service.DownloadAsync(actor, direct.Id)).Content);
		Assert.Equal(2, fixture.Database.Resources.Count());
	}

	[Fact]
	public async Task LibraryLifecycle_WhenMixedLinksAndOrphans_CleansOnlyZeroLinksAndPreservesAvatarsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot first = await projects.CreateAsync(actor, new CreateProjectRequest("First"));
		ProjectSnapshot second = await projects.CreateAsync(actor, new CreateProjectRequest("Second"));
		first = await editor.SaveItemAsync(actor, first.Id, "Phase", null, new(first.Revision, "Phase"));
		Guid phase = first.Items.Single(item => item.Kind == "Phase").Id;
		ResourceProfile resource = await service.UploadAsync(actor, new ResourceUploadRequest("../safe.bin", "Reusable", "application/octet-stream"), [1, 2, 3]);
		ResourceProfile orphan = await service.UploadAsync(actor, new ResourceUploadRequest("orphan.bin", "Orphan", "application/octet-stream"), [4]);
		fixture.Database.Agents.Add(new Agent { OwnerUserId = actor.UserId, Name = "Avatar", AvatarBytes = [9, 8, 7], AvatarContentType = "image/png" });
		await fixture.Database.SaveChangesAsync();
		Assert.Equal("safe.bin", resource.FileName);
		resource = await service.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, PhaseId: phase));
		Assert.True(resource.IsAssigned);
		Assert.Empty(resource.Projects);
		Assert.Single(resource.Phases);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SetLinkAsync(actor, resource.Id, new(1, true, ProjectId: second.Id)))).StatusCode);
		resource = await service.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, ProjectId: second.Id));
		first = await projects.GetAsync(actor, first.Id);
		await editor.DeleteItemAsync(actor, first.Id, "Phase", phase, new(first.Revision, true));
		resource = (await service.ListAsync(actor)).Single(item => item.Id == resource.Id);
		Assert.True(resource.IsAssigned);
		Assert.Empty(resource.Phases);
		Assert.Equal(1, await service.CleanOrphansAsync(actor, new(true, [resource.Id, orphan.Id])));
		Assert.Equal(new byte[] { 9, 8, 7 }, fixture.Database.Agents.Single().AvatarBytes);
		Assert.Equal(new byte[] { 1, 2, 3 }, (await service.DownloadAsync(actor, resource.Id)).Content);
		resource = await service.SetLinkAsync(actor, resource.Id, new(resource.Revision, false, ProjectId: second.Id));
		Assert.False(resource.IsAssigned);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.CleanOrphansAsync(actor, new(false, [resource.Id])))).StatusCode);
		long previousRevision = resource.Revision;
		resource = await service.UpdateAsync(actor, resource.Id, new(resource.Revision, "Renamed"));
		Assert.Equal("Renamed", resource.Title);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => service.UpdateAsync(actor, resource.Id, new(previousRevision, "Stale")))).StatusCode);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.DeleteAsync(actor, resource.Id, resource.Revision, false))).StatusCode);
		await service.DeleteAsync(actor, resource.Id, resource.Revision, true);
		Assert.Empty(await service.ListAsync(actor));
	}

	[Fact]
	public async Task ListProjectAsync_WhenPhaseOnlyDirectAndOrphan_ReturnsDeduplicatedEffectiveUnionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await new PlanEditor(projects).SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		ResourceProfile phaseOnly = await library.UploadAsync(actor, new("phase.bin", "Phase", "application/octet-stream", PhaseId: phase), [1]);
		ResourceProfile direct = await library.UploadAsync(actor, new("direct.bin", "Direct", "application/octet-stream", project.Id), [2]);
		await library.UploadAsync(actor, new("orphan.bin", "Orphan", "application/octet-stream"), [3]);
		Assert.Equal(2, (await library.ListProjectAsync(actor, project.Id)).Count);
		Assert.Equal(phaseOnly.Id, (await library.ListProjectAsync(actor, project.Id, phase)).Single().Id);
		await library.SetLinkAsync(actor, phaseOnly.Id, new(phaseOnly.Revision, true, ProjectId: project.Id));
		Assert.Equal(2, (await library.ListProjectAsync(actor, project.Id)).Count);
		Assert.DoesNotContain(await library.ListProjectAsync(actor, project.Id, phase), item => item.Id == direct.Id);
	}

	[Fact]
	public async Task UsageAsync_WhenApproachingQuota_ReportsOwnerUsageAndWarningWithoutBinaryPayloadsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(8, 5, 10), TimeProvider.System);
		await library.UploadAsync(actor, new("one.bin", "One", "application/octet-stream"), [1, 2, 3]);
		ResourceUsage first = await library.UsageAsync(actor);
		Assert.Equal(3, first.OwnerBytes);
		Assert.False(first.NearLimit);
		await library.UploadAsync(actor, new("two.bin", "Two", "application/octet-stream"), [4, 5]);
		ResourceUsage full = await library.UsageAsync(actor);
		Assert.Equal(5, full.OwnerBytes);
		Assert.True(full.NearLimit);
		Assert.Equal(8, full.MaximumFileBytes);
	}

	[Fact]
	public async Task UploadAsync_WhenUnassigned_PersistsExactBytesHashAndMetadataWithoutAvatarLinksAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		byte[] original = Encoding.UTF8.GetBytes("# Reference; España\nExact bytes.");

		ResourceProfile resource = await service.UploadAsync(actor, new ResourceUploadRequest("reference.md", "Reference", "text/markdown"), original);
		DownloadFile download = await service.DownloadAsync(actor, resource.Id);

		Assert.Equal(original, download.Content);
		Assert.Equal(Convert.ToHexString(SHA256.HashData(original)), resource.Sha256);
		Assert.Equal(original.Length, resource.Size);
		Assert.False(resource.IsAssigned);
		Assert.Equal("reference.md", download.FileName);
		Assert.Equal(resource.Id, (await service.ListAsync(actor)).Single().Id);
		Assert.Empty(fixture.Database.ProjectResources);
		Assert.Empty(fixture.Database.PhaseResources);
	}
}
