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
		if (long.TryParse(result.ChangeId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long eventId) && eventId > 0)
			cursor.AcknowledgeEvent(eventId);
	}, "Project exported successfully");
	private bool importAttemptFailed;
	private void OnOpenImport()
	{
		if (Busy) return;
		ResetImport(); ClearFeedback(); showImport = true;
	}
	private void OnCloseImport()
	{
		if (Busy) return;
		ResetImport(); ClearFeedback(); showImport = false;
	}
	private void ResetImport() { importFile = null; replacePlan = false; importAttemptFailed = false; }
	private void OnImportFileSelected(InputFileChangeEventArgs args)
	{
		if (Busy) return;
		importFile = args.File;
		if (importAttemptFailed) ClearFeedback();
		importAttemptFailed = false;
	}
	private async Task OnImportAsync()
	{
		if (Busy || importFile is null) return;
		IBrowserFile selectedFile = importFile;
		bool replace = replacePlan, confirmed = false;
		Guid projectId = Id;
		await ExecuteAsync(async () =>
		{
			ProjectSnapshot imported;
			try
			{
				byte[] bytes = await ReadFileAsync(selectedFile, 1024 * 1024, Token);
				imported = await Api.SendAsync<ProjectSnapshot>(HttpMethod.Post, $"projects/{projectId}/work-plan/import", bytes: bytes, contentType: "text/csv", headers: new Dictionary<string, string> { ["X-PlanForge-Replace"] = replace ? "true" : "false" }, cancellationToken: Token);
			}
			catch (Exception exception) when ((Id != projectId || Token.IsCancellationRequested) && exception is ApiException or HttpRequestException or IOException or OperationCanceledException)
			{
				// Retired CSV feedback must not reach the page wrapper's notifications.
				return;
			}
			confirmed = true;
			if (Id != projectId || Token.IsCancellationRequested) return;
			project = imported;
			cursor.AcknowledgeMutation(project.Revision);
			completedItemId = null; selectionEpoch++;
			showImport = false; importFile = null; replacePlan = false;
			Notify("CSV imported successfully");
			await RefreshConfirmedAsync(async () => resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token));
		});
		if (Id != projectId)
		{
			ClearFeedback();
			if (!Token.IsCancellationRequested) await ExecuteAsync(LoadAsync);
			return;
		}
		importAttemptFailed = !confirmed && Error is not null;
	}
	private StatusCounts CountStatuses(string kind)
	{
		IEnumerable<PlanItem> items = project!.Items.Where(item => item.Kind == kind);
		return new(items.Count(item => item.EffectiveStatus == "Pending"), items.Count(item => item.EffectiveStatus == "InProgress"), items.Count(item => item.EffectiveStatus == "Blocked"), items.Count(item => item.EffectiveStatus == "Completed"));
	}

	private string projectName = "", projectDescription = "", projectStatus = "Pending", projectWorkflow = "PlanningOnly";
	[SupplyParameterFromQuery(Name = "settings")] public bool Settings { get; set; }
	[SupplyParameterFromQuery(Name = "step")] public string? StepQuery { get; set; }
	private Guid? RequestedStepId => Guid.TryParse(StepQuery, out Guid id) ? id : null;
	private void OnSettings() { ClearFeedback(); projectName = project!.Name; projectDescription = project.Description; projectStatus = project.Status; projectWorkflow = project.Workflow; showSettings = true; }
	private Task OnSaveSettingsAsync() => ExecuteAsync(async () =>
	{
		project = await Api.SendAsync<ProjectSnapshot>(HttpMethod.Put, $"projects/{Id}", project! with { Name = projectName, Description = projectDescription, Status = projectStatus, Workflow = projectWorkflow }, cancellationToken: Token);
		cursor.AcknowledgeMutation(project!.Revision);
		showSettings = false;
	}, "Project updated");

	private string? deletePath, deleteTitle, deleteDescription;
	private string deleteMessage = "Project deleted";
	private bool deleteProject;
	private void OnDeleteItem(PlanItem item) { deleteMessage = item.Kind + " deleted"; PrepareDelete($"projects/{Id}/{PlanLabels.RouteKind(item.Kind)}/{item.Id}", T["Delete {0}", T[item.Kind.ToLowerInvariant()]].Value, T["Remove '{0}' and its descendants. Library files survive.", item.Title].Value, false); }
	private void OnDeleteProject() { deleteMessage = "Project deleted"; PrepareDelete($"projects/{Id}", "Delete project", T["Permanently delete '{0}' and all {1} plan records. Library files survive.", project!.Name, project.Items.Count].Value, true); }
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
			Notify("Project deleted", NotificationKind.Danger);
			Navigation.NavigateTo("/projects");
			return;
		}
		bool cleaning = deletePath.EndsWith("/clean", StringComparison.Ordinal);
		int before = project.Items.Count;
		project = await Api.SendAsync<ProjectSnapshot>(cleaning ? HttpMethod.Post : HttpMethod.Delete, deletePath, confirmation, cancellationToken: Token);
		cursor.AcknowledgeMutation(project.Revision);
		deletePath = null;
		Notify(cleaning ? before == project.Items.Count ? "No completed phases were removed" : "Completed phases removed" : deleteMessage,
			cleaning && before == project.Items.Count ? NotificationKind.Neutral : NotificationKind.Danger);
		await RefreshConfirmedAsync(async () => resources = await Api.GetAsync<List<ResourceProfile>>($"projects/{Id}/resources", Token));
	});

	[Parameter] public Guid Id { get; set; }

	private bool showResources;
	private PlanItem? resourceItem;
	private void OnProjectResources() { resourceItem = null; showSettings = false; showResources = true; }
	private void OnNodeResources(Guid nodeId) { resourceItem = project!.Items.SingleOrDefault(item => item.Id == nodeId); showResources = true; }
	private Task OnResourcesChangedAsync() => LoadAsync();
	private ProjectChangeCursor cursor = new();
	private Guid loadedProject;
	private IReadOnlyList<ProjectChangeInfo> activity = [];
	protected override async Task OnParametersSetAsync()
	{
		if (loadedProject != Id) { loadedProject = Id; cursor = new(); project = null; showImport = false; ResetImport(); ClearFeedback(); }
		await ExecuteAsync(LoadAsync);
		if (Settings && project is not null) OnSettings();
		else showSettings = false;
		StartPolling(PollProjectAsync);
	}

	private async Task ObserveAsync(ProjectChangePage page)
	{
		foreach (ProjectChangeInfo message in cursor.ObserveEvents(page)) Notify(message);
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

	private bool HasPendingStepDecision => editingId.HasValue && project!.Items.Any(item => item.Kind == "Decision" && item.ParentId == editingId && item.Status == "Pending");

	private void OnParentChanged()
	{
		Guid? parent = Guid.TryParse(itemParent, out Guid id) ? id : null;
		itemOrder = project!.Items.Count(item => item.Kind == itemKind && item.ParentId == parent && item.Id != editingId) + 1;
	}


	private void OnEdit(PlanItem item)
	{
		ClearFeedback();
		editingId = item.Id == Guid.Empty ? null : item.Id;
		itemKind = item.Kind;
		itemTitle = item.Title;
		itemDescription = item.Description;
		itemParent = item.ParentId?.ToString() ?? "";
		itemOrder = item.SortOrder + 1;
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
		var command = new PlanItemCommand(project!.Revision, itemTitle, Guid.TryParse(itemParent, out Guid parent) ? parent : null, itemOrder.HasValue ? itemOrder.Value - 1 : null, itemDescription, itemStatus, itemAssignment, itemAssignment == "Override" && Guid.TryParse(itemAgent, out Guid agent) ? agent : null, itemResolution, string.IsNullOrEmpty(itemEvidence) ? null : itemEvidence);
		string path = $"projects/{Id}/{PlanLabels.RouteKind(itemKind)}" + (editingId.HasValue ? $"/{editingId}" : "");
		project = await Api.SendAsync<ProjectSnapshot>(editingId.HasValue ? HttpMethod.Put : HttpMethod.Post, path, command, cancellationToken: Token);
		if (completing && project.Items.Any(item => item.Id == editingId && item.Status == "Completed")) { completedItemId = editingId; selectionEpoch++; }
		cursor.AcknowledgeMutation(project.Revision);
		showItem = false;
	}, "Project updated");
}
