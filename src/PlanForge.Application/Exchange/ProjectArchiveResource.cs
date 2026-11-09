namespace PlanForge.Application.Exchange;

internal sealed record ProjectArchiveResource(Guid ResourceId, string FileName, string ContentType, long Size, string Sha256, string ArchivePath, bool DirectProjectLink, IReadOnlyList<Guid> PhaseIds, IReadOnlyList<PlanForge.Contracts.Library.PlanItemResourceAssociation> ItemLinks);
