namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Database;

public sealed class AgentLifecycleMigrationTests
{
	[Fact]
	public void ExpansionMigration_WhenDowngraded_RefusesDataLoss()
	{
		var migration = new InspectableMigration();
		Assert.Throws<NotSupportedException>(migration.Downgrade);
	}

	private sealed class InspectableMigration : PlanForge.Infrastructure.Migrations.AccountPreferencesAndAgentLifecycle
	{
		public void Downgrade() => Down(new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"));
	}

	[Fact]
	public void ExpansionMigration_WhenGenerated_AddsOnlySafeDefaultsAndNullableHistoricalName()
	{
		Type type = Assert.Single(typeof(PlanForgeDbContext).Assembly.GetTypes(), type => type.Name == "AccountPreferencesAndAgentLifecycle");
		var migration = (Migration)Activator.CreateInstance(type)!;
		Assert.Equal(4, migration.UpOperations.Count);
		Assert.All(migration.UpOperations, operation => Assert.IsType<AddColumnOperation>(operation));
		var language = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), operation => operation.Table == "Users" && operation.Name == "Language");
		Assert.False(language.IsNullable);
		Assert.Equal("en", language.DefaultValue);
		var history = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), operation => operation.Table == "ProjectChanges");
		Assert.Equal("ActorNameSnapshot", history.Name);
		Assert.True(history.IsNullable);
		Assert.All(migration.UpOperations.OfType<AddColumnOperation>().Where(operation => operation.ClrType == typeof(long)), operation => Assert.Equal(0L, operation.DefaultValue));
	}
}
