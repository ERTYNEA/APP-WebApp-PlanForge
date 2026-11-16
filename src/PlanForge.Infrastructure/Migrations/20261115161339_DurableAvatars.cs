using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class DurableAvatars : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<Guid>(
				name: "AvatarGeneration",
				table: "Users",
				type: "uniqueidentifier",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

			migrationBuilder.AddColumn<bool>(
				name: "AvatarTemporary",
				table: "Users",
				type: "bit",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<Guid>(
				name: "AvatarGeneration",
				table: "Agents",
				type: "uniqueidentifier",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

			migrationBuilder.AddColumn<bool>(
				name: "AvatarTemporary",
				table: "Agents",
				type: "bit",
				nullable: false,
				defaultValue: false);

			migrationBuilder.CreateTable(
				name: "AvatarAdmissions",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
					Accepted = table.Column<bool>(type: "bit", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AvatarAdmissions", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "AvatarOptimizationJobs",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					IsAgent = table.Column<bool>(type: "bit", nullable: false),
					Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Sequence = table.Column<long>(type: "bigint", nullable: false),
					Status = table.Column<int>(type: "int", nullable: false),
					IsCurrent = table.Column<bool>(type: "bit", nullable: false),
					Attempts = table.Column<int>(type: "int", nullable: false),
					AvailableAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
					ReaderUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
					WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					Fence = table.Column<long>(type: "bigint", nullable: false),
					RetainedBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AvatarOptimizationJobs", x => x.Id);
					table.ForeignKey(
						name: "FK_AvatarOptimizationJobs_Users_OwnerUserId",
						column: x => x.OwnerUserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "AvatarQueueStates",
				columns: table => new
				{
					Id = table.Column<int>(type: "int", nullable: false)
						.Annotation("SqlServer:Identity", "1, 1"),
					NextSequence = table.Column<long>(type: "bigint", nullable: false),
					LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
					LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
					Fence = table.Column<long>(type: "bigint", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AvatarQueueStates", x => x.Id);
				});

			migrationBuilder.InsertData(
				table: "AvatarQueueStates",
				columns: new[] { "Id", "Fence", "LeaseOwner", "LeaseUntilUtc", "NextSequence" },
				values: new object[] { 1, 0L, null, null, 0L });

			migrationBuilder.CreateIndex(
				name: "IX_AvatarAdmissions_ExpiresAtUtc",
				table: "AvatarAdmissions",
				column: "ExpiresAtUtc");

			migrationBuilder.CreateIndex(
				name: "IX_AvatarOptimizationJobs_OwnerUserId_TargetId_IsAgent",
				table: "AvatarOptimizationJobs",
				columns: new[] { "OwnerUserId", "TargetId", "IsAgent" },
				unique: true,
				filter: "[IsCurrent] = 1");

			migrationBuilder.CreateIndex(
				name: "IX_AvatarOptimizationJobs_Status_Sequence",
				table: "AvatarOptimizationJobs",
				columns: new[] { "Status", "Sequence" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
			=> throw new NotSupportedException("Avatar data cannot be safely downgraded. Use a reviewed forward migration.");
	}
}
