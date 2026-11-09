namespace PlanForge.Web.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using System.Text.Json;

public partial class ResourcePanel : ApiPage
{
	private string assignment = "all", search = "";
	private bool listView;
	private List<ResourceProfile>? resources;
	private int skip, total;
	private ResourceUsage? usage;
	private ResourceProfile? managed, deleteResource;
	private string? deleteTitle;
	private string deleteDescription = "";
	private List<Guid> orphanIds = [];
	private Task OnPreviewCleanupAsync() => ExecuteAsync(async () =>
	{
		List<ResourceProfile> orphans = await GetAllResourcesAsync("orphan");
		orphanIds = orphans.Where(resource => !resource.IsAssigned).Select(resource => resource.Id).ToList();
		deleteResource = null; deleteTitle = "Clean orphan resources";
		deleteDescription = T["Permanently delete {0} currently unassigned files. Assigned files are never deleted. Preview: ", orphanIds.Count].Value + string.Join(", ", orphans.Select(resource => resource.FileName));
	});
	private void OnPreviewDelete()
	{
		deleteResource = managed; managed = null; deleteTitle = "Delete resource";
		deleteDescription = T["Permanently delete '{0}' and all its associations. This removes the Library binary.", deleteResource!.FileName].Value;
	}
	private Task OnConfirmDeleteAsync() => ExecuteAsync(async () =>
	{
		if (deleteResource is not null) await Api.SendAsync<object>(HttpMethod.Delete, $"resources/{deleteResource.Id}", new ConfirmedRevisionRequest(deleteResource.Revision, true), cancellationToken: Token);
		else
		{
			JsonElement result = await Api.SendAsync<JsonElement>(HttpMethod.Post, "resources/orphans/clean", new CleanOrphansRequest(true, orphanIds), cancellationToken: Token);
			Notify(T["Deleted {0} orphan resources; skipped {1}.", result.GetProperty("deletedCount").GetInt32(), result.GetProperty("skippedCount").GetInt32()].Value);
		}
		deleteTitle = null; deleteResource = null; skip = 0;
		await LoadAsync(); await Changed.InvokeAsync();
	}, "Resource cleanup completed");
	private string resourceTitle = "", resourceOperation = "";
	private Task ExecuteResourceAsync(string operation, Func<Task> action, string? message = null) { resourceOperation = operation; return ExecuteAsync(action, message); }
	private List<ProjectSummary> projectOptions = [];
	private List<PlanItem> phaseOptions = [];
	private string linkProject = "", linkPhase = "";
	private Task OnManageAsync(ResourceProfile resource) => ExecuteAsync(async () =>
	{
		managed = resource; resourceTitle = resource.Title; linkProject = ""; linkPhase = ""; phaseOptions = [];
		projectOptions = [];
		ProjectSummaryPage page;
		do { page = await Api.GetAsync<ProjectSummaryPage>($"projects/summary?skip={projectOptions.Count}&take=100", Token); projectOptions.AddRange(page.Items); } while (page.HasMore);
	});
	private Task OnLinkProjectChangedAsync() => ExecuteResourceAsync("association", async () =>
	{
		linkPhase = ""; phaseOptions = [];
		if (Guid.TryParse(linkProject, out Guid id)) phaseOptions = (await Api.GetAsync<ProjectSnapshot>($"projects/{id}", Token)).Items.Where(item => item.Kind != "Decision").ToList();
	});
	private Task OnAddAssociationAsync()
	{
		if (!Guid.TryParse(linkProject, out Guid projectId))
			return Task.CompletedTask;
		PlanItem? target = Guid.TryParse(linkPhase, out Guid id) ? phaseOptions.Single(item => item.Id == id) : null;
		return OnAssociationAsync(true, target is null ? projectId : null, target?.Kind == "Phase" ? target.Id : null, target?.Kind == "Subphase" ? target.Id : null, target?.Kind == "Step" ? target.Id : null);
	}
	private Task OnAssociationAsync(bool attach, Guid? projectId, Guid? phaseId, Guid? subphaseId = null, Guid? stepId = null) => ExecuteResourceAsync(attach ? "association" : "unlink", async () =>
	{
		if (managed is null) return;
		managed = await Api.SendAsync<ResourceProfile>(HttpMethod.Post, $"resources/{managed.Id}/links", new ResourceLinkRequest(managed.Revision, attach, projectId, phaseId, subphaseId, stepId), cancellationToken: Token);
		await LoadAsync(); await Changed.InvokeAsync();
	}, "Resource links updated");
	private Task OnSaveTitleAsync() => ExecuteResourceAsync("title", async () =>
	{
		if (managed is null) return;
		managed = await Api.SendAsync<ResourceProfile>(HttpMethod.Put, $"resources/{managed.Id}", new ResourceMetadataRequest(managed.Revision, resourceTitle), cancellationToken: Token);
		await LoadAsync(); await Changed.InvokeAsync();
	}, "Resource updated");
	private bool showUpload, showAttach;
	private List<ResourceProfile> candidates = [];
	private string selectedResource = "";
	private async Task<List<ResourceProfile>> GetAllResourcesAsync(string filter = "all")
	{
		List<ResourceProfile> result = [];
		ResourcePage page;
		do { page = await Api.GetAsync<ResourcePage>($"resources?skip={result.Count}&take=100&assignment={filter}", Token); result.AddRange(page.Items); } while (page.HasMore);
		return result;
	}
	private Task OnChooseExistingAsync() => ExecuteAsync(async () => { candidates = await GetAllResourcesAsync(); selectedResource = ""; showAttach = true; });
	private Task OnAttachExistingAsync() => ExecuteAsync(async () =>
	{
		ResourceProfile resource = candidates.Single(item => item.Id.ToString() == selectedResource);
		await Api.SendAsync<ResourceProfile>(HttpMethod.Post, $"resources/{resource.Id}/links", new ResourceLinkRequest(resource.Revision, true, NodeId.HasValue ? null : ProjectId, PhaseId, SubphaseId, StepId), cancellationToken: Token);
		showAttach = false; await LoadAsync(); await Changed.InvokeAsync();
	}, "Resource attached");
	private IBrowserFile? uploadFile;
	private string uploadTitle = "";
	private void OnFileSelected(InputFileChangeEventArgs args) { uploadFile = args.File; uploadTitle = uploadFile.Name; }
	private Task OnUploadAsync() => ExecuteAsync(async () =>
	{
		if (uploadFile is null || usage is null) return;
		byte[] bytes = await ReadFileAsync(uploadFile, usage.MaximumFileBytes, Token);
		var headers = new Dictionary<string, string> { ["X-File-Name"] = Uri.EscapeDataString(uploadFile.Name), ["X-Resource-Title"] = Uri.EscapeDataString(uploadTitle) };
		if (NodeId.HasValue) headers["X-" + NodeKind + "-Id"] = NodeId.ToString()!;
		else if (ProjectId.HasValue) headers["X-Project-Id"] = ProjectId.ToString()!;
		await Api.SendAsync<ResourceProfile>(HttpMethod.Post, "resources", bytes: bytes, contentType: string.IsNullOrWhiteSpace(uploadFile.ContentType) ? "application/octet-stream" : uploadFile.ContentType, headers: headers, cancellationToken: Token);
		showUpload = false; uploadFile = null; uploadTitle = "";
		await LoadAsync(); await Changed.InvokeAsync();
	}, "Resource uploaded successfully");
	[Parameter] public Guid? ProjectId { get; set; }
	[Parameter] public Guid? PhaseId { get; set; }
	[Parameter] public Guid? SubphaseId { get; set; }
	[Parameter] public Guid? StepId { get; set; }
	[Parameter] public EventCallback Changed { get; set; }

	protected override Task OnParametersSetAsync() => ExecuteAsync(LoadAsync);
	private async Task LoadAsync()
	{
		usage = await Api.GetAsync<ResourceUsage>("resources/usage", Token);
		if (ProjectId.HasValue)
		{
			scopeIds = [ProjectId.Value];
			if (NodeId.HasValue)
			{
				ProjectSnapshot snapshot = await Api.GetAsync<ProjectSnapshot>($"projects/{ProjectId}", Token);
				Dictionary<Guid, PlanItem> items = snapshot.Items.ToDictionary(item => item.Id);
				Guid? current = NodeId;
				while (current is Guid id && scopeIds.Add(id) && items.TryGetValue(id, out PlanItem? item))
					current = item.ParentId;
			}
			string path = $"projects/{ProjectId}" + (NodeId.HasValue ? $"/{PlanLabels.RouteKind(NodeKind)}/{NodeId}" : "") + "/resources";
			resources = await Api.GetAsync<List<ResourceProfile>>(path, Token);
			if (!NodeId.HasValue) resources = resources.Where(resource => resource.Projects.Any(link => link.ProjectId == ProjectId)).ToList();
			total = resources.Count;
		}
		else
		{
			ResourcePage page = await Api.GetAsync<ResourcePage>($"resources?skip={skip}&take=50&assignment={assignment}&search={Uri.EscapeDataString(search)}", Token);
			resources = page.Items.ToList(); total = page.Total;
		}
	}
	private Guid? NodeId => PhaseId ?? SubphaseId ?? StepId;
	private string NodeKind => SubphaseId.HasValue ? "Subphase" : StepId.HasValue ? "Step" : "Phase";
	private HashSet<Guid> scopeIds = [];

	private static IEnumerable<PlanItemResourceAssociation> ItemLinks(ResourceProfile resource) => resource.Items
		.Concat(resource.Phases.Select(link => new PlanItemResourceAssociation(link.PhaseId, link.PhaseTitle, "Phase", link.ProjectId, link.ProjectName))).DistinctBy(link => link.ItemId);

	private bool CanUnlinkProject(ProjectResourceAssociation link) => !ProjectId.HasValue || !NodeId.HasValue && link.ProjectId == ProjectId;
	private bool CanUnlinkItem(PlanItemResourceAssociation link) => !ProjectId.HasValue || link.ItemId == NodeId;

	private Task OnUnlinkItemAsync(PlanItemResourceAssociation link) => OnAssociationAsync(false, null, link.Kind == "Phase" ? link.ItemId : null, link.Kind == "Subphase" ? link.ItemId : null, link.Kind == "Step" ? link.ItemId : null);

	private IEnumerable<string> OriginLabels(ResourceProfile resource)
	{
		foreach (ProjectResourceAssociation link in resource.Projects.Where(link => !ProjectId.HasValue || link.ProjectId == ProjectId))
			yield return !ProjectId.HasValue ? T["Direct project: {0}", link.ProjectName].Value : !NodeId.HasValue ? T["Direct"].Value : T["Inherited from {0}: {1}", "Project", link.ProjectName].Value;
		foreach (PlanItemResourceAssociation link in ItemLinks(resource).Where(link => !ProjectId.HasValue || link.ProjectId == ProjectId && scopeIds.Contains(link.ItemId)))
			yield return !ProjectId.HasValue ? T["{0}: {1} · {2}", link.Kind, link.ItemTitle, link.ProjectName].Value : link.ItemId == NodeId ? T["Direct"].Value : T["Inherited from {0}: {1}", link.Kind, link.ItemTitle].Value;
	}

	private Task OnNextAsync() => ExecuteAsync(async () => { skip += 50; await LoadAsync(); });
	private Task OnPreviousAsync() => ExecuteAsync(async () => { skip = Math.Max(0, skip - 50); await LoadAsync(); });
	private Task OnSearchAsync() => ExecuteAsync(async () => { skip = 0; await LoadAsync(); });
}
