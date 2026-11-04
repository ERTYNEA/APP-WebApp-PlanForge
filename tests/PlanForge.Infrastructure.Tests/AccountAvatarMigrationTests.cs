namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Migrations;

public sealed class AccountAvatarMigrationTests
{
	[Fact]
	public void AccountAvatars_WhenInspected_AddsOnlyTwoNullableColumnsAndRefusesDestructiveDowngrade()
	{
		var migration = new InspectableMigration();
		var operations = migration.GetUp();
		Assert.Equal(2, operations.Count);
		Assert.All(operations, operation =>
		{
			var column = Assert.IsType<AddColumnOperation>(operation);
			Assert.Equal("Users", column.Table);
			Assert.True(column.IsNullable);
		});
		Assert.Throws<NotSupportedException>(() => migration.GetDown());
	}

	private sealed class InspectableMigration : AccountAvatars
	{
		public IReadOnlyList<MigrationOperation> GetDown()
		{
			var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
			Down(builder);
			return builder.Operations;
		}
		public IReadOnlyList<MigrationOperation> GetUp()
		{
			var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
			Up(builder);
			return builder.Operations;
		}
	}
}
