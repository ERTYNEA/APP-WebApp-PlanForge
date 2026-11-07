namespace PlanForge.Application.Accounts;

using PlanForge.Domain.Accounts;

public interface IAccountStore
{
	Task AddCredentialAsync(AccessCredential credential, CancellationToken cancellationToken);
	Task AddUserAsync(UserAccount user, CancellationToken cancellationToken);
	Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
	Task<AccessCredential?> FindCredentialAsync(string tokenHash, CancellationToken cancellationToken);
	Task<AccessCredential?> FindCredentialByIdAsync(Guid credentialId, Guid ownerUserId, CancellationToken cancellationToken);
	Task<UserAccount?> FindUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<string?> GetActiveAgentNameAsync(Guid ownerId, Guid agentId, long credentialVersion, CancellationToken cancellationToken);
	Task<UserAccount?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<AccessCredential>> ListCredentialsAsync(Guid ownerId, CancellationToken cancellationToken);
	Task SaveAsync(CancellationToken cancellationToken);
}
