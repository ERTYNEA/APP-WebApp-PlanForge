namespace PlanForge.Contracts.Library;

public sealed record ResourcePage(IReadOnlyList<ResourceProfile> Items, int Total, int Skip, int Take)
{
	public bool HasMore => Skip + Items.Count < Total;
}
