namespace PlanForge.Web.State;

using PlanForge.Contracts.Planning;

public sealed class ProjectChangeCursor
{
	private readonly HashSet<long> localRevisions = [];
	private readonly HashSet<long> localEvents = [];
	private bool initialized;
	public long After { get; private set; }

	public void AcknowledgeEvent(long eventId) { if (eventId > After) localEvents.Add(eventId); }

	public void AcknowledgeMutation(long revision) => localRevisions.Add(revision);

	public IReadOnlyList<string> Observe(ProjectChangePage page) => ObserveEvents(page).Select(change => change.Action).Distinct().ToArray();

	public IReadOnlyList<ProjectChangeInfo> ObserveEvents(ProjectChangePage page)
	{
		ProjectChangeInfo[] events = !initialized || page.ResetRequired ? [] : page.Changes.Where(change => change.Id > After && !localEvents.Contains(change.Id))
			.OrderBy(change => change.Id).Where(change => change.Operation == ProjectChangeOperation.Export || !localRevisions.Contains(change.Revision)).ToArray();
		After = page.LatestChangeId;
		localEvents.RemoveWhere(id => id <= After);
		foreach (long revision in page.Changes.Select(change => change.Revision)) localRevisions.Remove(revision);
		initialized = true;
		return events;
	}
}
