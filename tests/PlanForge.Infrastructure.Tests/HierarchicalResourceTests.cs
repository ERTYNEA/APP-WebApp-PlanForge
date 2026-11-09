namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;

public sealed class HierarchicalResourceTests
{
	[Fact]
	public async Task ExportZip_RecordsAllDirectOriginsOnceWithoutExpandingInheritedLinksAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("resource-zip@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var store = new EfProjectStore(fixture.Database);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(store, TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		ResourceProfile resource = await library.UploadAsync(actor, new("shared.bin", "Shared", "application/octet-stream", ProjectId: project.Id), [1, 2, 3]);
		resource = await library.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, SubphaseId: subphase));
		await library.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, StepId: step));
		var download = await new PlanForge.Application.Exchange.ProjectArchiveExporter(projects, library, store).ExportAsync(actor, project.Id);
		using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(download.Content));
		Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("resources/", StringComparison.Ordinal));
		using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
		string manifest = await reader.ReadToEndAsync();
		Assert.Contains(subphase.ToString(), manifest);
		Assert.Contains(step.ToString(), manifest);
		Assert.Contains("\"schemaVersion\": 2", manifest);
		Assert.Equal(2, fixture.Database.PhaseResources.Count());
	}

	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task DeleteLinkedNode_RemovesOnlyItsAssociationsAndPreservesLibraryBytesAsync(string kind)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("resource-delete@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Subphase", null, new(project.Revision, "Survivor", phase));
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase));
		Guid node = project.Items.First(item => item.Kind == kind).Id;
		ResourceProfile resource = await library.UploadAsync(actor, new("safe.bin", "Safe", "application/octet-stream"), [4, 5]);
		resource = await library.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, PhaseId: kind == "Phase" ? node : null, SubphaseId: kind == "Subphase" ? node : null, StepId: kind == "Step" ? node : null));
		project = await projects.GetAsync(actor, project.Id);
		await editor.DeleteItemAsync(actor, project.Id, kind, node, new(project.Revision, true));
		Assert.False((await library.GetAsync(actor, resource.Id)).IsAssigned);
		Assert.Equal(new byte[] { 4, 5 }, (await library.DownloadAsync(actor, resource.Id)).Content);
		Assert.Empty(fixture.Database.PhaseResources);
	}

	[Fact]
	public async Task EffectiveNodeResources_FollowAncestorsMovesAndIndependentLinksWithoutSiblingLeakageAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("resource-move@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "First"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Second"));
		Guid first = project.Items.Single(item => item.Title == "First").Id, second = project.Items.Single(item => item.Title == "Second").Id;
		Guid subphase = project.Items.Single(item => item.ParentId == first).Id, otherSubphase = project.Items.Single(item => item.ParentId == second).Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		ResourceProfile shared = await library.UploadAsync(actor, new("shared.bin", "Shared", "application/octet-stream", ProjectId: project.Id), [1]);
		ResourceProfile scoped = await library.UploadAsync(actor, new("scope.bin", "Scoped", "application/octet-stream", PhaseId: first), [2]);
		Assert.Equal(2, (await library.ListProjectAsync(actor, project.Id, step, itemKind: "Step")).Count);
		Assert.Single(await library.ListProjectAsync(actor, project.Id, otherSubphase, itemKind: "Subphase"));
		scoped = await library.SetLinkAsync(actor, scoped.Id, new(scoped.Revision, true, StepId: step));
		Assert.Equal(2, (await library.ListProjectAsync(actor, project.Id, step, itemKind: "Step")).Count);
		project = await projects.GetAsync(actor, project.Id);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", step, new(project.Revision, "Step", otherSubphase));
		Assert.Equal(2, (await library.ListProjectAsync(actor, project.Id, step, itemKind: "Step")).Count);
		scoped = await library.SetLinkAsync(actor, scoped.Id, new(scoped.Revision, false, StepId: step));
		Assert.Equal(shared.Id, Assert.Single(await library.ListProjectAsync(actor, project.Id, step, itemKind: "Step")).Id);
		Assert.Single((await library.GetAsync(actor, scoped.Id)).Phases);
	}

	[Theory]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task ResourceLink_AcceptsTypedStructuralNodeAndReportsActualOriginAsync(string kind)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("resource-node@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase));
		Guid node = project.Items.Single(item => item.Kind == kind).Id;
		ResourceProfile resource = await library.UploadAsync(actor, new("node.bin", "Node", "application/octet-stream"), [1, 2, 3]);
		string json = $"{{\"revision\":{resource.Revision},\"attach\":true,\"{kind.ToLowerInvariant()}Id\":\"{node}\"}}";
		ResourceLinkRequest command = System.Text.Json.JsonSerializer.Deserialize<ResourceLinkRequest>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
		resource = await library.SetLinkAsync(actor, resource.Id, command);
		Assert.True(resource.IsAssigned);
		Assert.Empty(resource.Projects);
		Assert.Empty(resource.Phases);
		Assert.Contains(node.ToString(), System.Text.Json.JsonSerializer.Serialize(resource));
		Assert.Single(await library.ListProjectAsync(actor, project.Id));
		Assert.Equal(new byte[] { 1, 2, 3 }, (await library.DownloadAsync(actor, resource.Id)).Content);
		Assert.Single(fixture.Database.PhaseResources);
	}

	[Fact]
	public async Task PhaseResources_IncludeProjectInheritedLinksWithoutMaterializingDuplicatesAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("resource-inheritance@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await new PlanEditor(projects).SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		ResourceProfile resource = await library.UploadAsync(actor, new("shared.bin", "Shared", "application/octet-stream", project.Id), [1, 2, 3]);
		Assert.Equal(resource.Id, Assert.Single(await library.ListProjectAsync(actor, project.Id, phase)).Id);
		Assert.Empty(fixture.Database.PhaseResources);
		resource = await library.SetLinkAsync(actor, resource.Id, new(resource.Revision, true, PhaseId: phase));
		Assert.Single(await library.ListProjectAsync(actor, project.Id, phase));
		Assert.Single(fixture.Database.PhaseResources);
		Assert.Single(fixture.Database.ProjectResources);
	}
}
