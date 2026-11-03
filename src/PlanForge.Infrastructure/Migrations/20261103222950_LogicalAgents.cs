using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class LogicalAgents : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Agents",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					AvatarBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
					AvatarContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
					CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
					IsActive = table.Column<bool>(type: "bit", nullable: false),
					Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Agents", x => x.Id);
					table.UniqueConstraint("AK_Agents_OwnerUserId_Id", x => new { x.OwnerUserId, x.Id });
					table.ForeignKey(
						name: "FK_Agents_Users_OwnerUserId",
						column: x => x.OwnerUserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_WorkItems_AssignedAgentId",
				table: "WorkItems",
				column: "AssignedAgentId");

			migrationBuilder.CreateIndex(
				name: "IX_AccessCredentials_OwnerUserId_AgentId",
				table: "AccessCredentials",
				columns: new[] { "OwnerUserId", "AgentId" });

			migrationBuilder.AddForeignKey(
				name: "FK_AccessCredentials_Agents_OwnerUserId_AgentId",
				table: "AccessCredentials",
				columns: new[] { "OwnerUserId", "AgentId" },
				principalTable: "Agents",
				principalColumns: new[] { "OwnerUserId", "Id" },
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_WorkItems_Agents_AssignedAgentId",
				table: "WorkItems",
				column: "AssignedAgentId",
				principalTable: "Agents",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new NotSupportedException("Shared SQL migrations are forward-only. Use a reviewed additive recovery migration.");
		}
	}
}
