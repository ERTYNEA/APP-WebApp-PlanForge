namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Exchange;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Contracts;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Library;
using PlanForge.Infrastructure.Planning;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

public sealed class ProjectArchiveTests
{
	[Fact]
	public void EnglishArchiveScaffold_WhenLocalized_UsesEmbeddedCanonicalTemplate()
	{
		var resources = new System.Resources.ResourceManager("PlanForge.Application.Resources.ArchiveText", typeof(ProjectArchiveExporter).Assembly);
		string? template = resources.GetString("ProjectReadme", System.Globalization.CultureInfo.InvariantCulture);
		Assert.NotNull(template);
		Assert.Contains("# {0}\n\n{1}", template);
		DirectoryInfo root = new(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(root.FullName, "APP-WebApp-PlanForge.slnx"))) root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
		Assert.DoesNotContain("## Contents and safe import", File.ReadAllText(Path.Combine(root.FullName, "src/PlanForge.Application/Exchange/ProjectArchiveExporter.cs")));
	}

	[Fact]
	public async Task ExportAsync_WhenMixedResources_IncludesPhaseOnlyUnionExactBytesManifestAndNoOrphansAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		AuthenticatedActor other = await fixture.Service.AuthenticateAsync(b.Token);
		var store = new EfProjectStore(fixture.Database);
		var projects = new ProjectService(store, TimeProvider.System);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("../Project ñ"));
		project = await new PlanEditor(projects).SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		ResourceProfile phaseOnly = await library.UploadAsync(actor, new("../image.png", "Phase only", "image/png", PhaseId: phase), image);
		ResourceProfile direct = await library.UploadAsync(actor, new("notes.md", "Direct", "text/markdown", ProjectId: project.Id), Encoding.UTF8.GetBytes("# Notes; ñ"));
		await library.SetLinkAsync(actor, direct.Id, new(direct.Revision, true, PhaseId: phase));
		ResourceProfile orphan = await library.UploadAsync(actor, new("orphan.bin", "Orphan", "application/octet-stream"), [9, 9]);
		project = await projects.GetAsync(actor, project.Id);
		var exporter = new ProjectArchiveExporter(projects, library, store);

		DownloadFile file = await exporter.ExportAsync(actor, project.Id);

		Assert.Equal("application/zip", file.ContentType);
		Assert.DoesNotContain('/', file.FileName);
		using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
		Assert.Equal(5, archive.Entries.Count);
		Assert.NotNull(archive.GetEntry("work-plan.csv"));
		Assert.NotNull(archive.GetEntry("README.md"));
		using var readmeReader = new StreamReader(archive.GetEntry("README.md")!.Open());
		string readme = await readmeReader.ReadToEndAsync();
		Assert.StartsWith("# ../Project ñ\n\n", readme);
		Assert.Contains("Active phase: Phase", readme);
		Assert.All(archive.Entries, entry => Assert.DoesNotContain("..", entry.FullName.Split('/')));
		Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains(orphan.Id.ToString()));
		using var bytes = new MemoryStream();
		await archive.GetEntry($"resources/{phaseOnly.Id:D}/image.png")!.Open().CopyToAsync(bytes);
		Assert.Equal(image, bytes.ToArray());
		using JsonDocument manifest = await JsonDocument.ParseAsync(archive.GetEntry("manifest.json")!.Open());
		Assert.Equal(2, manifest.RootElement.GetProperty("resources").GetArrayLength());
		Assert.Equal(project.Revision, (await projects.GetAsync(actor, project.Id)).Revision);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => exporter.ExportAsync(other, project.Id))).StatusCode);
	}
}
