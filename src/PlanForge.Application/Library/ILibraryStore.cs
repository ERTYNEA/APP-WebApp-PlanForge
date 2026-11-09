namespace PlanForge.Application.Library;

using PlanForge.Application.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Domain.Library;

public interface ILibraryStore
{
	Task ValidateTargetsAsync(Guid ownerId, Guid? projectId, Guid? phaseId, CancellationToken cancellationToken, Guid? subphaseId = null, Guid? stepId = null);
	Task<IReadOnlyList<ResourceProfile>> ListProjectAsync(Guid ownerId, Guid projectId, Guid? phaseId, CancellationToken cancellationToken, string itemKind = "Phase");
	Task<ResourceUsage> UsageAsync(Guid ownerId, ResourceLimits limits, CancellationToken cancellationToken);
	Task<int> CleanOrphansAsync(Guid ownerId, IReadOnlyList<Guid> resourceIds, CancellationToken cancellationToken);
	Task DeleteAsync(AuthenticatedActor actor, Guid resourceId, long revision, CancellationToken cancellationToken);
	Task<ResourceProfile> SetLinkAsync(AuthenticatedActor actor, Guid resourceId, ResourceLinkRequest request, CancellationToken cancellationToken);
	Task<ResourceProfile> UpdateAsync(AuthenticatedActor actor, Guid resourceId, ResourceMetadataRequest request, CancellationToken cancellationToken);
	Task<ResourceProfile> UploadAsync(AuthenticatedActor actor, Resource resource, Guid? projectId, Guid? phaseId, ResourceLimits limits, CancellationToken cancellationToken, Guid? subphaseId = null, Guid? stepId = null);
	Task<Resource?> DownloadAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken);
	Task<ResourceProfile?> GetAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ResourceProfile>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
}
