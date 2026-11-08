namespace PlanForge.Web.Components.Planning;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.State;

public partial class WorkPlanTree
{
	private Guid initializedProject;
	private bool inspectSelection;
	private long observedSelectionEpoch;
	private bool revealSelection;
	private Guid? requestedStepSeen;
	private PlanExplorerState.SelectionState state = new();

	[Parameter] public EventCallback<(string Kind, Guid? ParentId)> Add { get; set; }
	[Parameter] public IReadOnlyList<AgentProfile> Agents { get; set; } = [];
	[Parameter] public EventCallback<Guid> AttachResources { get; set; }
	[Parameter] public Guid? CompletedItemId { get; set; }
	[Parameter] public EventCallback<PlanItem> Delete { get; set; }
	[Parameter] public bool Disabled { get; set; }
	[Parameter] public EventCallback<PlanItem> Edit { get; set; }
	[Parameter] public EventCallback ProjectSettings { get; set; }
	[Parameter] public Guid? RequestedStepId { get; set; }
	[Parameter] public IReadOnlyList<ResourceProfile> Resources { get; set; } = [];
	[Parameter] public long SelectionEpoch { get; set; }
	[Parameter, EditorRequired] public ProjectSnapshot Snapshot { get; set; } = default!;

	private HashSet<Guid> Expanded => state.Expanded;
	private PlanItem? Selected => Snapshot.Items.FirstOrDefault(item => item.Id == SelectedId);
	private Guid? SelectedId { get => state.SelectedId; set => state.SelectedId = value; }

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await JavaScript.InvokeVoidAsync("planForge.layoutInspector");
		if (!revealSelection)
			return;
		revealSelection = false;
		await JavaScript.InvokeVoidAsync("planForge.revealCurrentWork", (SelectedId ?? Snapshot.Id).ToString());
		if (inspectSelection)
		{
			inspectSelection = false;
			await JavaScript.InvokeVoidAsync("planForge.revealInspector");
		}
	}

	protected override void OnParametersSet()
	{
		if (initializedProject != Snapshot.Id)
		{
			initializedProject = Snapshot.Id;
			state = NavigationState.Get(Snapshot.Id);
			observedSelectionEpoch = SelectionEpoch;
			requestedStepSeen = null;
			if (RequestedStepId is null && state.Initialized && (SelectedId is null || Selected is not null))
			{
				OnSelect(SelectedId);
				return;
			}
		}
		if (observedSelectionEpoch != SelectionEpoch)
		{
			observedSelectionEpoch = SelectionEpoch;
			if (CompletedItemId is null || CompletedItemId == SelectedId && Selected?.Status is not ("Pending" or "InProgress"))
			{
				Expanded.RemoveWhere(id => !state.ManuallyToggled.Contains(id));
				OnSelect(DefaultSelection());
				return;
			}
		}
		if (RequestedStepId != requestedStepSeen)
		{
			requestedStepSeen = RequestedStepId;
			if (Snapshot.Items.Any(item => item.Kind == "Step" && item.Id == RequestedStepId))
			{
				OnSelect(RequestedStepId);
				return;
			}
		}
		if (state.Initialized && (SelectedId is null || Selected is not null))
			return;
		OnSelect(DefaultSelection());
	}

	private Guid? DefaultSelection() => Snapshot.Items.Where(item => item.Kind == "Step" && item.Status is "InProgress" or "Pending")
		.OrderBy(item => item.Status == "InProgress" ? 0 : 1).ThenBy(item => item.CreationOrder).ThenBy(item => item.Id).FirstOrDefault()?.Id;

	private void OnManualSelect(Guid? id)
	{
		OnSelect(id);
		inspectSelection = true;
	}

	private void OnSelect(Guid? id)
	{
		SelectedId = id;
		state.Initialized = true;
		revealSelection = true;
		Guid? ancestor = Selected?.ParentId;
		for (int depth = 0; depth < Snapshot.Items.Count && ancestor.HasValue; depth++)
		{
			Expanded.Add(ancestor.Value);
			ancestor = Snapshot.Items.FirstOrDefault(item => item.Id == ancestor)?.ParentId;
		}
	}

	private void OnToggle(Guid id)
	{
		state.ManuallyToggled.Add(id);
		if (!Expanded.Add(id))
			Expanded.Remove(id);
	}
}
