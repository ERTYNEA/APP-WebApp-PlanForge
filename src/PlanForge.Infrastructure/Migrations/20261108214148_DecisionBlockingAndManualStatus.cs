namespace PlanForge.Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Preserves Step work state and atomically switches decision-derived blocking and revision writers.</summary>
public partial class DecisionBlockingAndManualStatus : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropCheckConstraint(name: "CK_Projects_WriteRevision", table: "Projects");
		migrationBuilder.AddColumn<int>(name: "ManualStatus", table: "WorkItems", type: "int", nullable: true);
		// Legacy manually blocked test work has no recoverable base state; normalize it to Pending.
		migrationBuilder.Sql("UPDATE WorkItems SET ManualStatus = CASE WHEN Status = 2 THEN 0 ELSE Status END WHERE Kind = 2;");
		migrationBuilder.Sql("UPDATE WorkItems SET Status = CASE WHEN EXISTS (SELECT 1 FROM WorkItems d WHERE d.ProjectId = WorkItems.ProjectId AND d.ParentId = WorkItems.Id AND d.Kind = 3 AND d.Status = 0) THEN 2 ELSE ManualStatus END WHERE Kind = 2;");
		foreach (int kind in new[] { 1, 0 })
			migrationBuilder.Sql($"""
				WITH PendingScopes AS (
					SELECT p.ProjectId, p.Id, p.ParentId FROM WorkItems d
					JOIN WorkItems p ON p.ProjectId = d.ProjectId AND p.Id = d.ParentId
					WHERE d.Kind = 3 AND d.Status = 0
					UNION ALL
					SELECT p.ProjectId, p.Id, p.ParentId FROM PendingScopes s
					JOIN WorkItems p ON p.ProjectId = s.ProjectId AND p.Id = s.ParentId
				)
				UPDATE target SET Status = CASE
					WHEN EXISTS (SELECT 1 FROM PendingScopes p WHERE p.ProjectId = target.ProjectId AND p.Id = target.Id)
						OR EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = target.ProjectId AND c.ParentId = target.Id AND c.Kind <> 3 AND c.Status = 2) THEN 2
					WHEN EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = target.ProjectId AND c.ParentId = target.Id AND c.Kind <> 3)
						AND NOT EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = target.ProjectId AND c.ParentId = target.Id AND c.Kind <> 3 AND c.Status <> 3) THEN 3
					WHEN EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = target.ProjectId AND c.ParentId = target.Id AND c.Kind <> 3 AND c.Status IN (1,3)) THEN 1
					ELSE 0 END
				FROM WorkItems target WHERE target.Kind = {kind} OPTION (MAXRECURSION 8);
				""");
		migrationBuilder.Sql("""
			UPDATE p SET Status = CASE
				WHEN EXISTS (SELECT 1 FROM WorkItems d WHERE d.ProjectId = p.Id AND d.Kind = 3 AND d.Status = 0)
					OR EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = p.Id AND c.Kind = 0 AND c.Status = 2) THEN 2
				WHEN EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = p.Id AND c.Kind = 0)
					AND NOT EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = p.Id AND c.Kind = 0 AND c.Status <> 3) THEN 3
				WHEN EXISTS (SELECT 1 FROM WorkItems c WHERE c.ProjectId = p.Id AND c.Kind = 0 AND c.Status IN (1,3)) THEN 1 ELSE 0 END,
				Revision = Revision + 1, WriteRevision = Revision + 2
			FROM Projects p;
			""");
		migrationBuilder.AddCheckConstraint(name: "CK_Projects_WriteRevision", table: "Projects", sql: "[WriteRevision] = [Revision] + 1");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
		=> throw new NotSupportedException("Decision state migration requires a reviewed forward correction, not destructive downgrade.");
}
