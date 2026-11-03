using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class ResourceLibrary : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Resources",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
					ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
					CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Revision = table.Column<long>(type: "bigint", nullable: false),
					Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
					Size = table.Column<long>(type: "bigint", nullable: false),
					Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
					UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Resources", x => x.Id);
					table.ForeignKey(
						name: "FK_Resources_Users_OwnerUserId",
						column: x => x.OwnerUserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "PhaseResources",
				columns: table => new
				{
					PhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_PhaseResources", x => new { x.PhaseId, x.ResourceId });
					table.ForeignKey(
						name: "FK_PhaseResources_Resources_ResourceId",
						column: x => x.ResourceId,
						principalTable: "Resources",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_PhaseResources_WorkItems_PhaseId",
						column: x => x.PhaseId,
						principalTable: "WorkItems",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "ProjectResources",
				columns: table => new
				{
					ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ProjectResources", x => new { x.ProjectId, x.ResourceId });
					table.ForeignKey(
						name: "FK_ProjectResources_Projects_ProjectId",
						column: x => x.ProjectId,
						principalTable: "Projects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_ProjectResources_Resources_ResourceId",
						column: x => x.ResourceId,
						principalTable: "Resources",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_PhaseResources_ResourceId",
				table: "PhaseResources",
				column: "ResourceId");

			migrationBuilder.CreateIndex(
				name: "IX_ProjectResources_ResourceId",
				table: "ProjectResources",
				column: "ResourceId");

			migrationBuilder.CreateIndex(
				name: "IX_Resources_OwnerUserId_Sha256",
				table: "Resources",
				columns: new[] { "OwnerUserId", "Sha256" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new NotSupportedException("Shared SQL migrations are forward-only. Use a reviewed additive recovery migration.");
		}
	}
}
