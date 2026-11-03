namespace PlanForge.Contracts.Library;

public sealed record CleanOrphansRequest(bool Confirmed, IReadOnlyList<Guid> ResourceIds);
