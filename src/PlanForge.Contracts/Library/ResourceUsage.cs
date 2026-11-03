namespace PlanForge.Contracts.Library;

public sealed record ResourceUsage(long OwnerBytes, long OwnerBudgetBytes, int MaximumFileBytes, bool NearLimit);
