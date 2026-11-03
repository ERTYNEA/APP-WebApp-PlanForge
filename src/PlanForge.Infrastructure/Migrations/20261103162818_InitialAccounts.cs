using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class InitialAccounts : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Users",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
					Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
					IsActive = table.Column<bool>(type: "bit", nullable: false),
					NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
					PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Users", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "AccessCredentials",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
					Kind = table.Column<int>(type: "int", nullable: false),
					LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
					Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
					TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AccessCredentials", x => x.Id);
					table.ForeignKey(
						name: "FK_AccessCredentials_Users_OwnerUserId",
						column: x => x.OwnerUserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AccessCredentials_OwnerUserId_Kind",
				table: "AccessCredentials",
				columns: new[] { "OwnerUserId", "Kind" });

			migrationBuilder.CreateIndex(
				name: "IX_AccessCredentials_TokenHash",
				table: "AccessCredentials",
				column: "TokenHash",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_Users_NormalizedEmail",
				table: "Users",
				column: "NormalizedEmail",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new NotSupportedException("Destructive rollback is disabled. Use a reviewed forward migration or owner-managed PITR recovery.");
		}
	}
}
