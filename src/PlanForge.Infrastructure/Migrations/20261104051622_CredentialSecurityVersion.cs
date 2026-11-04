using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class CredentialSecurityVersion : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<long>(
				name: "SecurityVersion",
				table: "Users",
				type: "bigint",
				nullable: false,
				defaultValue: 0L);

			migrationBuilder.AddColumn<long>(
				name: "SecurityVersion",
				table: "AccessCredentials",
				type: "bigint",
				nullable: false,
				defaultValue: 0L);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new System.NotSupportedException("Shared SQL migrations are forward-only. Use a reviewed additive recovery migration.");
		}
	}
}
