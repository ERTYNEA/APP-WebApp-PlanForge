namespace PlanForge.Contracts.Planning;

public sealed record ProjectChangePage(long Revision, long LatestChangeId, bool ResetRequired, IReadOnlyList<ProjectChangeInfo> Changes);
