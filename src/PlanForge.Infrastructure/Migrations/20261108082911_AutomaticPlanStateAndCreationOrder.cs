namespace PlanForge.Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <summary>Adds immutable creation order and reconciles existing calculated plan states without deleting work.</summary>
public partial class AutomaticPlanStateAndCreationOrder : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(name: "WriteRevision", table: "Projects", type: "bigint", nullable: false, defaultValue: 0L);
		migrationBuilder.AddColumn<long>(name: "CreationOrder", table: "WorkItems", type: "bigint", nullable: false, defaultValue: 0L);
		// Historical negative ranks are reconstructed, not certified original timestamps.
		migrationBuilder.Sql("""
			WITH Ranked AS (
				SELECT item.Id, ROW_NUMBER() OVER (ORDER BY project.CreatedAt, project.Id,
					COALESCE(grandparent.SortOrder, parent.SortOrder, item.SortOrder),
					COALESCE(parent.SortOrder, item.SortOrder), item.SortOrder, item.Id)
					- COUNT_BIG(*) OVER () - 1 AS ReconstructedOrder
				FROM WorkItems item INNER JOIN Projects project ON project.Id = item.ProjectId
				LEFT JOIN WorkItems parent ON parent.ProjectId = item.ProjectId AND parent.Id = item.ParentId
				LEFT JOIN WorkItems grandparent ON grandparent.ProjectId = parent.ProjectId AND grandparent.Id = parent.ParentId
			)
			UPDATE item SET CreationOrder = ranked.ReconstructedOrder
			FROM WorkItems item INNER JOIN Ranked ranked ON ranked.Id = item.Id;
			""");
		foreach (int kind in new[] { 1, 0 })
			migrationBuilder.Sql($"""
				WITH DecisionAncestors AS (
					SELECT ProjectId, ParentId AS Id FROM WorkItems WHERE Kind = 3 AND Status = 0
					UNION ALL
					SELECT ancestor.ProjectId, parent.ParentId FROM DecisionAncestors ancestor
					INNER JOIN WorkItems parent ON parent.ProjectId = ancestor.ProjectId AND parent.Id = ancestor.Id
					WHERE parent.ParentId IS NOT NULL
				)
				UPDATE parent SET Status = CASE
					WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = parent.ProjectId AND child.ParentId = parent.Id AND child.Kind <> 3 AND child.Status = 2) THEN 2
					WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = parent.ProjectId AND child.ParentId = parent.Id AND child.Kind <> 3)
						AND NOT EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = parent.ProjectId AND child.ParentId = parent.Id AND child.Kind <> 3 AND child.Status <> 3)
						AND NOT EXISTS (SELECT 1 FROM DecisionAncestors decision WHERE decision.ProjectId = parent.ProjectId AND decision.Id = parent.Id) THEN 3
					WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = parent.ProjectId AND child.ParentId = parent.Id AND child.Kind <> 3 AND child.Status IN (1,3)) THEN 1
					ELSE 0 END
				FROM WorkItems parent WHERE parent.Kind = {kind};
				""");
		migrationBuilder.Sql("""
			UPDATE project SET Status = CASE
				WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = project.Id AND child.Kind = 0 AND child.Status = 2) THEN 2
				WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = project.Id AND child.Kind = 0)
					AND NOT EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = project.Id AND child.Kind = 0 AND child.Status <> 3)
					AND NOT EXISTS (SELECT 1 FROM WorkItems decision WHERE decision.ProjectId = project.Id AND decision.Kind = 3 AND decision.Status = 0) THEN 3
				WHEN EXISTS (SELECT 1 FROM WorkItems child WHERE child.ProjectId = project.Id AND child.Kind = 0 AND child.Status IN (1,3)) THEN 1
				ELSE 0 END,
				Revision = Revision + 1
			FROM Projects project;
			""");
		// DDL, reconciliation and fence commit together: legacy readers remain compatible,
		// but an old revision writer fails closed until the ordered API publication completes.
		migrationBuilder.Sql("UPDATE Projects SET WriteRevision = Revision;");
		migrationBuilder.AddCheckConstraint(name: "CK_Projects_WriteRevision", table: "Projects", sql: "[WriteRevision] = [Revision]");
		migrationBuilder.CreateIndex(name: "IX_WorkItems_Kind_Status_CreationOrder_Id", table: "WorkItems", columns: ["Kind", "Status", "CreationOrder", "Id"]);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
		=> throw new NotSupportedException("Creation history is immutable. Use a reviewed forward correction, not a destructive downgrade.");
}
