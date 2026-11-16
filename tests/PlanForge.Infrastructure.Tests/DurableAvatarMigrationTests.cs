namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PlanForge.Infrastructure.Database;

public sealed class DurableAvatarMigrationTests
{
	[Fact]
	public void SqlServerMigration_AddsQueueAndGenerationsWithoutChangingExistingPhotos()
	{
		using var database = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlServer("Server=unused.invalid;Database=design-only;Integrated Security=true;Encrypt=true").Options);
		var assembly = database.GetService<IMigrationsAssembly>();
		var entry = Assert.Single(assembly.Migrations, item => item.Key.EndsWith("DurableAvatars", StringComparison.Ordinal));
		Migration migration = assembly.CreateMigration(entry.Value, database.Database.ProviderName!);
		Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), table => table.Name == "AvatarOptimizationJobs");
		Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), table => table.Name == "AvatarAdmissions");
		Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), table => table.Name == "AvatarQueueStates");
		Assert.Equal(4, migration.UpOperations.OfType<AddColumnOperation>().Count());
		Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DropColumnOperation or AlterColumnOperation or DeleteDataOperation);
		Assert.Throws<NotSupportedException>(() => migration.DownOperations);
		string previous = assembly.Migrations.Keys.TakeWhile(key => key != entry.Key).Last();
		string sql = database.GetService<IMigrator>().GenerateScript(previous, entry.Key);
		Assert.Contains("WHERE [IsCurrent] = 1", sql);
		Assert.DoesNotContain("DROP ", sql);
		Assert.DoesNotContain("UPDATE [Users]", sql);
		Assert.DoesNotContain("UPDATE [Agents]", sql);
		Assert.False(database.Database.HasPendingModelChanges());
	}
}
