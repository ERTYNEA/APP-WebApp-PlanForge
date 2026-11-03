namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Migrations;

public sealed class MigrationSafetyTests
{
	[Fact]
	public void ProjectPlans_WhenDowngradeRequested_RefusesDestructiveRollback()
	{
		var migration = new ProjectPlans();
		Assert.Throws<NotSupportedException>(() => migration.DownOperations);
		Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
	}

	[Fact]
	public void InitialAccounts_WhenDowngradeRequested_RefusesDestructiveRollback()
	{
		var migration = new InitialAccounts();

		Assert.Throws<NotSupportedException>(() => migration.DownOperations);
	}

	[Fact]
	public void InitialAccounts_WhenApplied_ContainsOnlyAdditiveOperations()
	{
		var migration = new InitialAccounts();

		Assert.NotEmpty(migration.UpOperations);
		Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
	}
}
