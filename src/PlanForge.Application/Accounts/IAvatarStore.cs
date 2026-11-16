namespace PlanForge.Application.Accounts;

using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;

public interface IAvatarStore
{
	Task<AvatarAdmission> PrepareAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default);
	Task<bool> HasRetainedAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default);
	Task<AvatarState> AcceptAsync(Guid admissionId, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
	Task<AvatarState> DeleteAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default, Guid? admissionId = null);
}
