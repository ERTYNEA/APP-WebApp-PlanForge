namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Migrations;

public sealed class CredentialSecurityMigrationTests
{
	[Fact]
	public void SecurityVersionMigration_WhenInspected_IsAdditiveAndRefusesDestructiveDowngrade()
	{
		var migration = new InspectableMigration();
		Assert.Equal(2, migration.Operations().Count);
		Assert.All(migration.Operations(), operation =>
		{
			var column = Assert.IsType<AddColumnOperation>(operation);
			Assert.Equal("SecurityVersion", column.Name);
			Assert.Equal(0L, column.DefaultValue);
			Assert.Contains(column.Table, new[] { "Users", "AccessCredentials" });
		});
		Assert.Throws<NotSupportedException>(migration.Downgrade);
	}

	private sealed class InspectableMigration : CredentialSecurityVersion
	{
		public IReadOnlyList<MigrationOperation> Operations()
		{
			var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
			Up(builder);
			return builder.Operations;
		}
		public void Downgrade() => Down(new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"));
	}
}
