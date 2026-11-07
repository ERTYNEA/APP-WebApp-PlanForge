namespace PlanForge.Application.Agents;

using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;

public interface IAgentStore
{
	Task<int> CleanTokensAsync(Guid ownerId, DateTimeOffset now, CancellationToken cancellationToken);
	Task DeleteAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken);
	void Add(Agent agent);
	void Add(AccessCredential credential);
	Task<Agent?> GetAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Agent>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
	Task<AccessCredential?> GetTokenAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken);
	Task<IReadOnlyList<AccessCredential>> ListTokensAsync(Guid ownerId, CancellationToken cancellationToken);
	Task SaveAsync(CancellationToken cancellationToken);
}
