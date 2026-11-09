namespace PlanForge.Contracts.Library;

public sealed record ResourceProfile(Guid Id, string Title, string FileName, string ContentType, long Size, string Sha256, long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<ProjectResourceAssociation> Projects, IReadOnlyList<PhaseResourceAssociation> Phases)
{
	public IReadOnlyList<PlanItemResourceAssociation> Items { get; init; } = [];
	public bool IsAssigned => Projects.Count > 0 || Phases.Count > 0 || Items.Count > 0;
}
