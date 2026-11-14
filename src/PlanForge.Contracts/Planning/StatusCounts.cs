namespace PlanForge.Contracts.Planning;

/// <summary>Effective-state category counts, including visible zero values and an exact total invariant.</summary>
public sealed record StatusCounts(int Pending, int InProgress, int Blocked, int Completed)
{
	public int Total => Pending + InProgress + Blocked + Completed;
}
