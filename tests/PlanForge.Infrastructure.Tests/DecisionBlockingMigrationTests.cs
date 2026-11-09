namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Database;

public sealed class DecisionBlockingMigrationTests
{
	[Fact]
	public void Migration_PreservesProtectedIdentityAndBinaryTablesAndFencesOlderWriters()
	{
		Type type = Assert.Single(typeof(PlanForgeDbContext).Assembly.GetTypes(), type => type.Name == "DecisionBlockingAndManualStatus");
		var migration = (Migration)Activator.CreateInstance(type)!;
		AddColumnOperation manual = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
		Assert.Equal("WorkItems", manual.Table);
		Assert.Equal("ManualStatus", manual.Name);
		Assert.True(manual.IsNullable);
		Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DropColumnOperation or DeleteDataOperation);
		Assert.Contains(migration.UpOperations.OfType<AddCheckConstraintOperation>(), check => check.Name == "CK_Projects_WriteRevision" && check.Sql == "[WriteRevision] = [Revision] + 1");
		Assert.All(migration.UpOperations.OfType<SqlOperation>(), operation =>
		{
			Assert.False(operation.SuppressTransaction);
			Assert.DoesNotContain("UPDATE Users", operation.Sql, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("UPDATE Agents", operation.Sql, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("AccessCredentials", operation.Sql, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("DELETE", operation.Sql, StringComparison.OrdinalIgnoreCase);
		});
		Assert.Throws<NotSupportedException>(() => _ = migration.DownOperations);
	}
}
