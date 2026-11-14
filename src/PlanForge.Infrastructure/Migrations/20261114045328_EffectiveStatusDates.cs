namespace PlanForge.Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Adds trustworthy nullable state-entry dates without fabricating legacy history.</summary>
public partial class EffectiveStatusDates : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<DateTimeOffset>(
			name: "EffectiveStatusChangedAt",
			table: "WorkItems",
			type: "datetimeoffset",
			nullable: true);

		migrationBuilder.AddColumn<DateTimeOffset>(
			name: "EffectiveStatusChangedAt",
			table: "Projects",
			type: "datetimeoffset",
			nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
		=> throw new NotSupportedException("Preserve state-entry history; use a reviewed forward migration instead.");
}
