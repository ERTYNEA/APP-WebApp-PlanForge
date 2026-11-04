using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class AccountAvatars : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<byte[]>(
				name: "AvatarBytes",
				table: "Users",
				type: "varbinary(max)",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AvatarContentType",
				table: "Users",
				type: "nvarchar(80)",
				maxLength: 80,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			throw new System.NotSupportedException("Shared SQL migrations are forward-only. Use a reviewed additive recovery migration.");
		}
	}
}
