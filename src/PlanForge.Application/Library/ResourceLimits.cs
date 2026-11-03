namespace PlanForge.Application.Library;

public sealed record ResourceLimits(int MaximumFileBytes = 8 * 1024 * 1024, long OwnerBudgetBytes = 100 * 1024 * 1024, long GlobalBudgetBytes = 1024L * 1024 * 1024);
