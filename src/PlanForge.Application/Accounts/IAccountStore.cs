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
	Task SaveAsync(CancellationToken cancellationToken);
}
