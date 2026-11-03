namespace PlanForge.Application.Exchange;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Contracts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

public sealed class ProjectArchiveExporter(ProjectService projects, LibraryService library, IProjectStore store)
{
	private static readonly SemaphoreSlim exportBudget = new(1, 1);
	private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

	/// <summary>Exports one coherent revision and the effective resource union without mutating plan data.</summary>
	public async Task<DownloadFile> ExportAsync(AuthenticatedActor actor, Guid projectId, CancellationToken cancellationToken = default)
	{
		if (!await exportBudget.WaitAsync(0, cancellationToken))
			throw new PlanForgeException(429, "Another project archive is being prepared. Retry shortly.");
		try
		{
			ProjectSnapshot project = await projects.GetAsync(actor, projectId, cancellationToken);
			IReadOnlyList<ResourceProfile> resources = await library.ListProjectAsync(actor, projectId, cancellationToken: cancellationToken);
			DateTimeOffset exportedAt = DateTimeOffset.UtcNow;
			List<ProjectArchiveResource> manifestResources = [];
			using var output = new MemoryStream();
			using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
			{
				await WriteEntryAsync(archive, "work-plan.csv", WorkPlanCsvCodec.Serialize(project), cancellationToken);
				await WriteEntryAsync(archive, "README.md", Encoding.UTF8.GetBytes(CreateReadme(project, exportedAt)), cancellationToken);
				foreach (ResourceProfile resource in resources.DistinctBy(resource => resource.Id))
				{
					DownloadFile file = await library.DownloadAsync(actor, resource.Id, cancellationToken);
					string path = $"resources/{resource.Id:D}/{LibraryService.SafeFileName(resource.FileName)}";
					await WriteEntryAsync(archive, path, file.Content, cancellationToken);
					manifestResources.Add(new(resource.Id, resource.FileName, resource.ContentType, resource.Size, resource.Sha256, path,
						resource.Projects.Any(link => link.ProjectId == projectId), resource.Phases.Where(link => link.ProjectId == projectId).Select(link => link.PhaseId).Distinct().ToList()));
				}
				await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, projectId, revision = project.Revision, exportedAt, resources = manifestResources }, jsonOptions), cancellationToken);
			}
			if ((await projects.GetAsync(actor, projectId, cancellationToken)).Revision != project.Revision)
				throw new PlanForgeException(409, "The project changed during export. Reload and export the current revision.");
			await store.RecordEventAsync(actor.UserId, projectId, project.Revision, actor.AgentId ?? actor.UserId, actor.IsHuman ? "Human" : "Agent", actor.IsHuman ? "Project exported successfully" : "Project exported successfully by agent", cancellationToken);
			string name = new string(project.Name.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-').Take(60).ToArray()).Trim('-');
			if (name.Length == 0)
				name = "project";
			return new DownloadFile(output.ToArray(), "application/zip", $"PlanForge-Project-{name}-{projectId.ToString("N")[..8]}.zip");
		}
		finally
		{
			exportBudget.Release();
		}
	}

	private static string CreateReadme(ProjectSnapshot project, DateTimeOffset exportedAt)
	{
		Dictionary<Guid, PlanItem> items = project.Items.ToDictionary(item => item.Id);
		PlanItem? step = project.Items.Where(item => item.Kind == "Step").OrderBy(item => item.Status == "InProgress" ? 0 : item.Status == "Blocked" ? 1 : item.Status == "Pending" ? 2 : 3).ThenBy(item => item.SortOrder).FirstOrDefault();
		PlanItem? subphase = step?.ParentId is Guid subphaseId && items.TryGetValue(subphaseId, out PlanItem? parent) ? parent : null;
		PlanItem? phase = subphase?.ParentId is Guid phaseId && items.TryGetValue(phaseId, out PlanItem? ancestor) ? ancestor : project.Items.FirstOrDefault(item => item.Kind == "Phase");
		string workflow = project.Workflow switch
		{
			"PlanningOnly" => "Planning Only",
			"LocalChangesManualApproval" => "Local Changes + Manual Approval",
			"BranchPullRequest" => "Branch + Pull Request",
			"DirectToMain" => "Direct to Main",
			_ => project.Workflow
		};
		return $"# {project.Name}\n\n{project.Description}\n\nProject ID: {project.Id:D}\nRevision: {project.Revision}\nExported: {exportedAt:O}\nStatus: {project.Status}\nProgress: {project.Progress}%\nWorkflow: {workflow}\nActive phase: {phase?.Title ?? "None"}\nActive subphase: {subphase?.Title ?? "None"}\nActive step: {step?.Title ?? "None"}\n\n## Contents and safe import\n\nwork-plan.csv contains the complete plan snapshot, not file bytes. manifest.json identifies each resource, SHA-256, MIME type and exact project/phase associations. Phase-only resources are included; unrelated Library files are excluded.\n\nImport work-plan.csv only into the matching ProjectId and current BaseRevision. Omitted records require explicit replace confirmation. A stale import returns 409: reload and reconcile rather than overwrite. Workflow closure still requires the authorized approval, merge or push evidence.\n\nThe resources directory is reference material, not an executable package or a resource-import command. This archive contains no credentials.\n";
	}

	private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
	{
		await using Stream stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
		await stream.WriteAsync(content, cancellationToken);
	}
}
