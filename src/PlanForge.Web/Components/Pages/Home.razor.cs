namespace PlanForge.Web.Components.Pages;

using Microsoft.AspNetCore.Components;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public partial class Home : ApiPage
{
	private readonly Dictionary<Guid, ProjectChangeCursor> cursors = [];
	private WorkspaceDashboard? dashboard;
	private string description = "", name = "", search = "", sort = "updated", status = "", workflow = "PlanningOnly";
	private bool? managementRoute;
	private List<ProjectSummary>? projects;
	private int recentSkip, recentTotal;
	private bool showCreate;

	protected override bool HasOpenOperation => showCreate;
	[Parameter] public bool IsManagement { get; set; }
	private IEnumerable<ProjectSummary> Filtered => (projects ?? []).Where(project => (status.Length == 0 || project.Status == status) && project.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
		.OrderBy(project => sort == "name" ? project.Name : "").ThenByDescending(project => sort == "progress" ? project.Progress : 0).ThenByDescending(project => project.UpdatedAt).ThenBy(project => project.Id);

	protected override async Task OnParametersSetAsync()
	{
		if (managementRoute != IsManagement) { managementRoute = IsManagement; projects = null; dashboard = null; recentSkip = 0; ClearFeedback(); }
		await ExecuteAsync(LoadAsync);
		StartPolling(LoadAsync);
	}

	private async Task LoadAsync()
	{
		List<ProjectSummary> loaded = [];
		ProjectSummaryPage page;
		if (IsManagement)
		{
			do
			{
				page = await Api.GetAsync<ProjectSummaryPage>($"projects/summary?skip={loaded.Count}&take=100", Token);
				loaded.AddRange(page.Items);
			} while (page.HasMore);
		}
		else
		{
			dashboard = await Api.GetAsync<WorkspaceDashboard>("projects/dashboard", Token);
			page = await Api.GetAsync<ProjectSummaryPage>($"projects/summary?skip={recentSkip}&take=25", Token);
			recentTotal = page.Total;
			loaded.AddRange(page.Items);
		}
		foreach (ProjectSummary project in loaded)
		{
			if (!cursors.TryGetValue(project.Id, out ProjectChangeCursor? cursor))
			{
				cursor = new(); cursors.Add(project.Id, cursor);
				cursor.Observe(new(project.Revision, project.LatestChangeId, false, []));
			}
			else if (cursor.After != project.LatestChangeId)
			{
				ProjectChangePage changes = await Api.GetAsync<ProjectChangePage>($"projects/{project.Id}/changes?after={cursor.After}", Token);
				foreach (string message in cursor.Observe(changes)) Notify(message);
			}
		}
		projects = loaded;
	}

	private void OnCloseCreate() { ClearFeedback(); showCreate = false; }
	private Task OnCreateAsync() => ExecuteAsync(async () =>
	{
		ProjectSnapshot project = await Api.SendAsync<ProjectSnapshot>(HttpMethod.Post, "projects", new CreateProjectRequest(name, description, workflow), cancellationToken: Token);
		showCreate = false; Navigation.NavigateTo($"/projects/{project.Id}");
	});
	private Task OnNextAsync() => ExecuteAsync(async () => { recentSkip += 25; await LoadAsync(); });
	private void OnOpenCreate() { ClearFeedback(); showCreate = true; }
	private Task OnPreviousAsync() => ExecuteAsync(async () => { recentSkip = Math.Max(0, recentSkip - 25); await LoadAsync(); });
}
