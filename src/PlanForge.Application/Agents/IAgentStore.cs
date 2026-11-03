namespace PlanForge.Application.Agents;

using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;

public interface IAgentStore
{
	void Add(Agent agent);
	void Add(AccessCredential credential);
	Task<Agent?> GetAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Agent>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
	Task<AccessCredential?> GetTokenAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken);
	Task<IReadOnlyList<AccessCredential>> ListTokensAsync(Guid ownerId, CancellationToken cancellationToken);
	Task SaveAsync(CancellationToken cancellationToken);
}
