namespace PlanForge.Contracts.Library;

/// <summary>A direct resource association, never a materialized inherited link.</summary>
public sealed record PlanItemResourceAssociation(Guid ItemId, string ItemTitle, string Kind, Guid ProjectId, string ProjectName);
