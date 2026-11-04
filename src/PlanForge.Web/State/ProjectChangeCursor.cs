namespace PlanForge.Web.State;

using PlanForge.Contracts.Planning;

public sealed class ProjectChangeCursor
{
	public long After { get; private set; }
	private bool initialized;
	public IReadOnlyList<string> Observe(ProjectChangePage page)
	{
		string[] messages = !initialized ? [] : page.Changes.Where(change => change.Id > After).OrderBy(change => change.Id).Select(change => change.Action).Distinct().ToArray();
		After = page.LatestChangeId;
		initialized = true;
		return messages;
	}
}
