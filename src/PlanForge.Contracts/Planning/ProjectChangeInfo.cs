namespace PlanForge.Contracts.Planning;

public sealed record ProjectChangeInfo(long Id, long Revision, Guid ActorId, string ActorKind, string ActorName, string Action, DateTimeOffset CreatedAt);
