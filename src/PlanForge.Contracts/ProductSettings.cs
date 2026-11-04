namespace PlanForge.Contracts;

public sealed record ProductSettings(string Version, int MaximumAvatarBytes, int MaximumFileBytes, long OwnerBudgetBytes, long GlobalBudgetBytes, IReadOnlyList<string> DevelopmentWorkflows);
