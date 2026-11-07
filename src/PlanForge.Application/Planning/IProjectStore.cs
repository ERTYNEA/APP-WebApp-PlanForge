namespace PlanForge.Application.Planning;

using PlanForge.Domain.Planning;
using PlanForge.Contracts.Planning;

public interface IProjectStore
{
	Task<ProjectSummaryPage> ListSummariesAsync(Guid ownerId, int skip, int take, CancellationToken cancellationToken);
	Task<ProjectChangePage?> GetChangesAsync(Guid ownerId, Guid projectId, long after, CancellationToken cancellationToken);
	Task RecordEventAsync(Guid ownerId, Guid projectId, long revision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken, string? actorNameSnapshot = null);
	Task<bool> IsValidAgentAsync(Guid ownerId, Guid agentId, bool requireActive, CancellationToken cancellationToken);
	Task CreateAsync(Project project, CancellationToken cancellationToken);
	Task<ProjectAggregate?> GetAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ProjectAggregate>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
	Task SaveAsync(ProjectAggregate aggregate, long expectedRevision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken, string? actorNameSnapshot = null);
	Task DeleteAsync(Guid ownerId, Guid projectId, long expectedRevision, CancellationToken cancellationToken);
}
