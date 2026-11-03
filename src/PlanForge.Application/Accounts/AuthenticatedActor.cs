namespace PlanForge.Application.Accounts;

using PlanForge.Domain.Accounts;

public sealed record AuthenticatedActor(Guid UserId, CredentialKind Kind, Guid CredentialId, Guid? AgentId)
{
	public bool IsHuman => Kind == CredentialKind.HumanSession;
}
