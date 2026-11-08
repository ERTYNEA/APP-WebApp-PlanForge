namespace PlanForge.Web.State;

/// <summary>Retains explorer navigation only inside the authenticated Blazor circuit, never browser storage.</summary>
public sealed class PlanExplorerState
{
	private readonly Dictionary<Guid, SelectionState> projects = [];

	public SelectionState Get(Guid projectId)
	{
		if (!projects.TryGetValue(projectId, out SelectionState? state))
		{
			state = new();
			projects.Add(projectId, state);
		}
		return state;
	}

	public sealed class SelectionState
	{
		public HashSet<Guid> Expanded { get; } = [];
		public HashSet<Guid> ManuallyToggled { get; } = [];
		public bool Initialized { get; set; }
		public Guid? SelectedId { get; set; }
	}
}
