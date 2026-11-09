namespace PlanForge.Web.Components.Planning;

using Microsoft.AspNetCore.Components;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;

public partial class WorkPlanInspector
{
	private Dictionary<Guid, ResourceOrigin> scope = [];

	[Parameter] public EventCallback<(string Kind, Guid? ParentId)> Add { get; set; }
	[Parameter] public IReadOnlyList<AgentProfile> Agents { get; set; } = [];
	[Parameter] public EventCallback<Guid> AttachResources { get; set; }
	[Parameter] public EventCallback<PlanItem> Delete { get; set; }
	[Parameter] public bool Disabled { get; set; }
	[Parameter] public EventCallback<PlanItem> Edit { get; set; }
	[Parameter] public PlanItem? Item { get; set; }
	[Parameter] public EventCallback ProjectSettings { get; set; }
	[Parameter] public IReadOnlyList<ResourceProfile> Resources { get; set; } = [];
	[Parameter] public EventCallback<Guid?> Select { get; set; }
	[Parameter, EditorRequired] public ProjectSnapshot Snapshot { get; set; } = default!;

	private string Description => Item?.Description ?? Snapshot.Description;
	private IReadOnlyList<PlanItem> SelectionPath { get; set; } = [];
	private IReadOnlyList<ResourceProfile> VisibleResources { get; set; } = [];

	protected override void OnParametersSet()
	{
		Dictionary<Guid, PlanItem> index = Snapshot.Items.ToDictionary(item => item.Id);
		List<PlanItem> path = [];
		Guid? current = Item?.Id;
		HashSet<Guid> seen = [];
		while (current is Guid id && seen.Add(id) && index.TryGetValue(id, out PlanItem? node))
		{
			path.Insert(0, node);
			current = node.ParentId;
		}
		SelectionPath = path;
		scope = path.ToDictionary(node => node.Id, node => new ResourceOrigin(node.Id, node.Kind, node.Title));
		scope[Snapshot.Id] = new(Snapshot.Id, "Project", Snapshot.Name);
		VisibleResources = Resources.DistinctBy(resource => resource.Id).Where(resource => Origins(resource).Count > 0).ToList();
	}

	private string AgentName(Guid id) => Agents.FirstOrDefault(agent => agent.Id == id) is AgentProfile agent ? agent.Name + (agent.IsActive ? "" : " (" + T["Inactive"].Value + ")") : T["Unassigned/Inactive"].Value;

	private IReadOnlyList<ResourceOrigin> Origins(ResourceProfile resource)
	{
		List<ResourceOrigin> origins = [];
		if (resource.Projects.Any(link => link.ProjectId == Snapshot.Id))
			origins.Add(scope[Snapshot.Id]);
		IEnumerable<Guid> links = resource.Items.Where(link => link.ProjectId == Snapshot.Id).Select(link => link.ItemId)
			.Concat(resource.Phases.Where(link => link.ProjectId == Snapshot.Id).Select(link => link.PhaseId));
		foreach (Guid id in links.Distinct())
			if (scope.TryGetValue(id, out ResourceOrigin? origin))
				origins.Add(origin);
		return origins;
	}

	private sealed record ResourceOrigin(Guid Id, string Kind, string Title);
}
