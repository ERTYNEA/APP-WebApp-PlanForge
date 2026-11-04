namespace PlanForge.Contracts.Planning;

public sealed record ProjectSummaryPage(IReadOnlyList<ProjectSummary> Items, int Total, int Skip, int Take, bool HasMore);
