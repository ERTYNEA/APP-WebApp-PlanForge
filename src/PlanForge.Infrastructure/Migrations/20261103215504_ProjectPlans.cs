using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class ProjectPlans : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Projects",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
					Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Revision = table.Column<long>(type: "bigint", nullable: false),
					SortOrder = table.Column<int>(type: "int", nullable: false),
					Status = table.Column<int>(type: "int", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					Workflow = table.Column<int>(type: "int", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Projects", x => x.Id);
					table.ForeignKey(
						name: "FK_Projects_Users_OwnerUserId",
						column: x => x.OwnerUserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "ProjectChanges",
				columns: table => new
				{
					Id = table.Column<long>(type: "bigint", nullable: false)
						.Annotation("SqlServer:Identity", "1, 1"),
					Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
					ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					ActorKind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Revision = table.Column<long>(type: "bigint", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ProjectChanges", x => x.Id);
					table.ForeignKey(
						name: "FK_ProjectChanges_Projects_ProjectId",
						column: x => x.ProjectId,
						principalTable: "Projects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "WorkItems",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					AssignedAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					AssignmentMode = table.Column<int>(type: "int", nullable: false),
					CompletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
					Description = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
					Evidence = table.Column<int>(type: "int", nullable: true),
					Kind = table.Column<int>(type: "int", nullable: false),
					ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Resolution = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
					SortOrder = table.Column<int>(type: "int", nullable: false),
					Status = table.Column<int>(type: "int", nullable: false),
					Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
					UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_WorkItems", x => x.Id);
					table.UniqueConstraint("AK_WorkItems_ProjectId_Id", x => new { x.ProjectId, x.Id });
					table.ForeignKey(
						name: "FK_WorkItems_Projects_ProjectId",
						column: x => x.ProjectId,
						principalTable: "Projects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_WorkItems_WorkItems_ProjectId_ParentId",
						columns: x => new { x.ProjectId, x.ParentId },
						principalTable: "WorkItems",
						principalColumns: new[] { "ProjectId", "Id" },
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ProjectChanges_ProjectId_Id",
				table: "ProjectChanges",
				columns: new[] { "ProjectId", "Id" });

			migrationBuilder.CreateIndex(
				name: "IX_Projects_OwnerUserId_UpdatedAt",
				table: "Projects",
				columns: new[] { "OwnerUserId", "UpdatedAt" });

			migrationBuilder.CreateIndex(
				name: "IX_WorkItems_ProjectId_ParentId",
				table: "WorkItems",
				columns: new[] { "ProjectId", "ParentId" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new NotSupportedException("Shared SQL migrations are forward-only. Use a reviewed additive recovery migration.");
		}
	}
}
