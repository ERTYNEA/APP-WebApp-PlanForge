namespace PlanForge.Web.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Api;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public partial class ProjectDetail : ApiPage
{
	protected override bool HasOpenOperation => showItem || showSettings || showImport || showResources || deletePath is not null;
	private List<AgentProfile> agents = [];
	private Guid? editingId;
	private string itemAgent = "", itemAssignment = "Inherit", itemDescription = "", itemEvidence = "", itemKind = "Phase", itemParent = "", itemResolution = "", itemStatus = "Pending", itemTitle = "";
	private int? itemOrder;
	[Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
	private ProjectSnapshot? currentProject;
	private ProjectSnapshot? project { get => currentProject; set { currentProject = value; Breadcrumbs.SetProject(value); } }
	private List<ResourceProfile> resources = [];
	private bool showItem, showImport, replacePlan;
	private long selectionEpoch;
	private Guid? completedItemId;
	private bool settingsOpen;
	private bool showSettings { get => settingsOpen; set { settingsOpen = value; Breadcrumbs.SetSettings(value); } }
	private IBrowserFile? importFile;
	private Task OnExportAsync(string kind) => ExecuteAsync(async () =>
	{
		DownloadResult result = await JavaScript.InvokeAsync<DownloadResult>("planForge.download", Token, $"/files/{kind}/{Id}");
		if (!result.Success) throw new ApiException(result.Status, "The export was not downloaded. Reload before retrying.");
	}, "Project exported successfully");
	private Task OnImportAsync() => ExecuteAsync(async () =>
	{
		if (importFile is null) return;
		byte[] bytes = await ReadFileAsync(importFile, 1024 * 1024, Token);
		project = await Api.SendAsync<ProjectSnapshot>(HttpMethod.Post, $"projects/{Id}/work-plan/import", bytes: bytes, contentType: "text/csv", headers: new Dictionary<string, string> { ["X-PlanForge-Replace"] = replacePlan ? "true" : "false" }, cancellationToken: Token);
		completedItemId = null; selectionEpoch++;
		resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token);
		showImport = false; importFile = null; replacePlan = false;
	}, "CSV imported successfully");
	private string projectName = "", projectDescription = "", projectStatus = "Pending", projectWorkflow = "PlanningOnly";
	[SupplyParameterFromQuery(Name = "settings")] public bool Settings { get; set; }
	[SupplyParameterFromQuery(Name = "step")] public string? StepQuery { get; set; }
	private Guid? RequestedStepId => Guid.TryParse(StepQuery, out Guid id) ? id : null;
	private void OnSettings() { ClearFeedback(); projectName = project!.Name; projectDescription = project.Description; projectStatus = project.Status; projectWorkflow = project.Workflow; showSettings = true; }
	private Task OnSaveSettingsAsync() => ExecuteAsync(async () =>
	{
		project = await Api.SendAsync<ProjectSnapshot>(HttpMethod.Put, $"projects/{Id}", project! with { Name = projectName, Description = projectDescription, Status = projectStatus, Workflow = projectWorkflow }, cancellationToken: Token);
		showSettings = false;
	}, "Project updated");

	private string? deletePath, deleteTitle, deleteDescription;
	private bool deleteProject;
	private void OnDeleteItem(PlanItem item) => PrepareDelete($"projects/{Id}/{PlanLabels.RouteKind(item.Kind)}/{item.Id}", T["Delete {0}", T[item.Kind.ToLowerInvariant()]].Value, T["Remove '{0}' and its descendants. Library files survive.", item.Title].Value, false);
	private void OnDeleteProject() => PrepareDelete($"projects/{Id}", "Delete project", T["Permanently delete '{0}' and all {1} plan records. Library files survive.", project!.Name, project.Items.Count].Value, true);
	private void OnCleanCompleted() => PrepareDelete($"projects/{Id}/completed-phases/clean", "Clean completed phases", T["Remove only fully completed phases ({0} currently marked complete). Pending, blocked and active work is retained. Library files survive.", project!.Items.Count(item => item.Kind == "Phase" && item.Status == "Completed")].Value, false);
	private void PrepareDelete(string path, string title, string description, bool wholeProject) { ClearFeedback(); deletePath = path; deleteTitle = title; deleteDescription = description; deleteProject = wholeProject; showSettings = false; }
	private Task OnConfirmDeleteAsync() => ExecuteAsync(async () =>
	{
		if (deletePath is null) return;
		var confirmation = new ConfirmedRevisionRequest(project!.Revision, true);
		if (deleteProject)
		{
			await Api.SendAsync<object>(HttpMethod.Delete, deletePath, confirmation, cancellationToken: Token);
			deletePath = null;
			Navigation.NavigateTo("/projects");
			return;
		}
		else project = await Api.SendAsync<ProjectSnapshot>(deletePath.EndsWith("/clean", StringComparison.Ordinal) ? HttpMethod.Post : HttpMethod.Delete, deletePath, confirmation, cancellationToken: Token);
		deletePath = null;
		resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token);
	}, "Project updated");

	[Parameter] public Guid Id { get; set; }

	private bool showResources;
	private Guid? resourcePhase;
	private void OnProjectResources() { resourcePhase = null; showSettings = false; showResources = true; }
	private void OnPhaseResources(Guid phaseId) { resourcePhase = phaseId; showResources = true; }
	private Task OnResourcesChangedAsync() => ExecuteAsync(LoadAsync);
	private ProjectChangeCursor cursor = new();
	private Guid loadedProject;
	private IReadOnlyList<ProjectChangeInfo> activity = [];
	protected override async Task OnParametersSetAsync()
	{
		if (loadedProject != Id) { loadedProject = Id; cursor = new(); project = null; }
		await ExecuteAsync(LoadAsync);
		if (Settings && project is not null) OnSettings();
		else showSettings = false;
		StartPolling(PollProjectAsync);
	}

	private async Task ObserveAsync(ProjectChangePage page)
	{
		foreach (string message in cursor.Observe(page)) Notify(message);
		if (page.Changes.Count > 0) activity = page.Changes.OrderByDescending(change => change.Id).Take(8).ToList();
		await Task.CompletedTask;
	}
	private async Task PollProjectAsync()
	{
		if (showItem || showSettings || showImport || showResources || deletePath is not null) return;
		ProjectChangePage page = await Api.GetAsync<ProjectChangePage>($"projects/{Id}/changes?after={cursor.After}", Token);
		if (page.ResetRequired || project?.Revision != page.Revision)
		{
			project = await Api.GetAsync<ProjectSnapshot>($"projects/{Id}", Token);
			agents = await Api.GetAsync<List<AgentProfile>>("agents", Token);
			resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token);
		}
		await ObserveAsync(page);
	}

	private async Task LoadAsync()
	{
		project = await Api.GetAsync<ProjectSnapshot>($"projects/{Id}", Token);
		agents = await Api.GetAsync<List<AgentProfile>>("agents", Token);
		resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token);
		await ObserveAsync(await Api.GetAsync<ProjectChangePage>($"projects/{Id}/changes?after={cursor.After}", Token));
	}

	private void OnAdd((string Kind, Guid? ParentId) target) => OnEdit(new PlanItem(Guid.Empty, target.Kind, target.ParentId, project!.Items.Count(item => item.Kind == target.Kind && item.ParentId == target.ParentId), ""));

	private void OnAddPhase() => OnAdd(("Phase", null));

	private void OnEdit(PlanItem item)
	{
		ClearFeedback();
		editingId = item.Id == Guid.Empty ? null : item.Id;
		itemKind = item.Kind;
		itemTitle = item.Title;
		itemDescription = item.Description;
		itemParent = item.ParentId?.ToString() ?? "";
		itemOrder = item.SortOrder;
		itemStatus = item.Status;
		itemAssignment = item.AssignmentMode;
		itemAgent = item.AssignedAgentId?.ToString() ?? "";
		itemResolution = item.Resolution;
		itemEvidence = item.Evidence ?? "";
		showItem = true;
	}

	private Task OnSaveItemAsync() => ExecuteAsync(async () =>
	{
		bool completing = itemKind == "Step" && editingId.HasValue && itemStatus == "Completed" && project!.Items.Any(item => item.Id == editingId && item.Status != "Completed");
		var command = new PlanItemCommand(project!.Revision, itemTitle, Guid.TryParse(itemParent, out Guid parent) ? parent : null, itemOrder, itemDescription, itemStatus, itemAssignment, itemAssignment == "Override" && Guid.TryParse(itemAgent, out Guid agent) ? agent : null, itemResolution, string.IsNullOrEmpty(itemEvidence) ? null : itemEvidence);
		string path = $"projects/{Id}/{PlanLabels.RouteKind(itemKind)}" + (editingId.HasValue ? $"/{editingId}" : "");
		project = await Api.SendAsync<ProjectSnapshot>(editingId.HasValue ? HttpMethod.Put : HttpMethod.Post, path, command, cancellationToken: Token);
		if (completing && project.Items.Any(item => item.Id == editingId && item.Status == "Completed")) { completedItemId = editingId; selectionEpoch++; }
		showItem = false;
	}, "Project updated");
}
