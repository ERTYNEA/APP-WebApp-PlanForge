namespace PlanForge.Application.Planning;

using PlanForge.Domain.Planning;

public interface IProjectStore
{
	Task RecordEventAsync(Guid ownerId, Guid projectId, long revision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken);
	Task<bool> IsValidAgentAsync(Guid ownerId, Guid agentId, bool requireActive, CancellationToken cancellationToken);
	Task CreateAsync(Project project, CancellationToken cancellationToken);
	Task<ProjectAggregate?> GetAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ProjectAggregate>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
	Task SaveAsync(ProjectAggregate aggregate, long expectedRevision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken);
	Task DeleteAsync(Guid ownerId, Guid projectId, long expectedRevision, CancellationToken cancellationToken);
}
