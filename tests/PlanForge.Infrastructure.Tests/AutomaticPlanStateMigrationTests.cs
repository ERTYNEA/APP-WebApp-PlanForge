namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Database;

public sealed class AutomaticPlanStateMigrationTests
{
	[Fact]
	public void Expansion_FencesLegacyWritersInTheSameTransactionalCutover()
	{
		Type type = Assert.Single(typeof(PlanForgeDbContext).Assembly.GetTypes(), type => type.Name == "AutomaticPlanStateAndCreationOrder");
		var migration = (Migration)Activator.CreateInstance(type)!;
		AddColumnOperation column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "WriteRevision");
		Assert.Equal("Projects", column.Table);
		Assert.Equal(0L, column.DefaultValue);
		AddCheckConstraintOperation check = Assert.Single(migration.UpOperations.OfType<AddCheckConstraintOperation>());
		Assert.Equal("[WriteRevision] = [Revision]", check.Sql);
		Assert.Equal("CK_Projects_WriteRevision", check.Name);
		Assert.Contains(migration.UpOperations, operation => operation is SqlOperation sql && sql.Sql.Contains("WriteRevision = Revision", StringComparison.Ordinal));
		Assert.All(migration.UpOperations.OfType<SqlOperation>(), sql => Assert.False(sql.SuppressTransaction));
	}

	[Fact]
	public void Expansion_AddsCreationOrderAndOnlyReconcilesDerivedData()
	{
		Type type = Assert.Single(typeof(PlanForgeDbContext).Assembly.GetTypes(), type => type.Name == "AutomaticPlanStateAndCreationOrder");
		var migration = (Migration)Activator.CreateInstance(type)!;
		AddColumnOperation column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "CreationOrder");
		Assert.Equal("CreationOrder", column.Name);
		Assert.Equal("WorkItems", column.Table);
		Assert.Equal(0L, column.DefaultValue);
		Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DropColumnOperation or DeleteDataOperation);
		Assert.Contains(migration.UpOperations, operation => operation is CreateIndexOperation);
		Assert.Contains(migration.UpOperations, operation => operation is SqlOperation sql && sql.Sql.Contains("ROW_NUMBER", StringComparison.Ordinal));
		Assert.Throws<NotSupportedException>(() => _ = migration.DownOperations);
	}
}
