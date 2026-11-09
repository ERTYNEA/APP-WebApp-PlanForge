namespace PlanForge.Web.Components.Planning;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.State;

public partial class WorkPlanTree
{
	private HashSet<Guid> branches = [];
	private bool hadSelection;
	private List<Guid> selectionAncestors = [];
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
	[Parameter] public EventCallback Reload { get; set; }
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
		HashSet<Guid> parents = Snapshot.Items.Where(item => item.Kind != "Decision" && item.ParentId.HasValue).Select(item => item.ParentId!.Value).ToHashSet();
		branches = Snapshot.Items.Where(item => item.Kind is "Phase" or "Subphase" && parents.Contains(item.Id)).Select(item => item.Id).ToHashSet();
		if (initializedProject != Snapshot.Id)
		{
			initializedProject = Snapshot.Id;
			hadSelection = false;
			selectionAncestors = [];
			state = NavigationState.Get(Snapshot.Id);
			observedSelectionEpoch = SelectionEpoch;
			requestedStepSeen = null;
			if (RequestedStepId is null && state.Initialized && (SelectedId is null || Selected is not null))
			{
				RememberSelectionPath();
				return;
			}
		}
		if (observedSelectionEpoch != SelectionEpoch)
		{
			observedSelectionEpoch = SelectionEpoch;
			if (CompletedItemId is null || CompletedItemId == SelectedId && Selected?.EffectiveStatus is not ("Pending" or "InProgress"))
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
		{
			RememberSelectionPath();
			return;
		}
		if (state.Initialized && hadSelection)
		{
			SelectedId = selectionAncestors.Where(id => Snapshot.Items.Any(item => item.Id == id)).Select(id => (Guid?)id).FirstOrDefault();
			revealSelection = false;
			inspectSelection = false;
			RememberSelectionPath();
			return;
		}
		OnSelect(DefaultSelection());
	}

	private Guid? DefaultSelection() => Snapshot.GetEligibleSteps().FirstOrDefault()?.Id;

	private void OnExpandAll()
	{
		Expanded.UnionWith(branches);
		state.ManuallyToggled.UnionWith(branches);
	}

	private void OnCollapseAll()
	{
		Expanded.ExceptWith(branches);
		state.ManuallyToggled.UnionWith(branches);
	}

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
		RememberSelectionPath();
		Expanded.UnionWith(selectionAncestors);
	}

	private void RememberSelectionPath()
	{
		hadSelection = Selected is not null;
		selectionAncestors = [];
		Guid? ancestor = Selected?.ParentId;
		for (int depth = 0; depth < Snapshot.Items.Count && ancestor.HasValue; depth++)
		{
			selectionAncestors.Add(ancestor.Value);
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
