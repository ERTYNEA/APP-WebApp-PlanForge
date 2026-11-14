namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using PlanForge.Infrastructure.Database;

public sealed class EffectiveStatusMigrationTests
{
	[Fact]
	public void Migration_AddsOnlyNullableUnknownDatesAndRefusesDestructiveDowngrade()
	{
		using var database = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlServer("Server=unused.invalid;Database=isolated-provider-script;Integrated Security=true;Encrypt=true").Options);
		var assembly = database.GetService<IMigrationsAssembly>();
		var migrationType = assembly.Migrations.Last().Value;
		Assert.EndsWith("EffectiveStatusDates", migrationType.Name);
		var migration = assembly.CreateMigration(migrationType, database.Database.ProviderName!);
		Assert.Equal(2, migration.UpOperations.Count);
		Assert.All(migration.UpOperations, operation =>
		{
			var addition = Assert.IsType<AddColumnOperation>(operation);
			Assert.Equal("EffectiveStatusChangedAt", addition.Name);
			Assert.True(addition.IsNullable);
			Assert.Null(addition.DefaultValue);
			Assert.Null(addition.DefaultValueSql);
			Assert.Equal("datetimeoffset", addition.ColumnType);
		});
		Assert.Throws<NotSupportedException>(() => migration.DownOperations);
		string script = database.GetService<IMigrator>().GenerateScript(assembly.Migrations.Keys.Reverse().Skip(1).First(), assembly.Migrations.Last().Key);
		Assert.Contains("ALTER TABLE [Projects] ADD [EffectiveStatusChangedAt] datetimeoffset NULL", script);
		Assert.Contains("ALTER TABLE [WorkItems] ADD [EffectiveStatusChangedAt] datetimeoffset NULL", script);
		Assert.DoesNotContain("UPDATE ", script);
		Assert.DoesNotContain("DROP ", script);
		Assert.False(database.Database.HasPendingModelChanges());
	}
	[Fact]
	public async Task NullableExpansion_PreservesLegacyRowsAndUnknownDatesInRelationalStorageAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new PlanForge.Contracts.Accounts.RegisterRequest("legacy-migration@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new PlanForge.Application.Planning.ProjectService(new PlanForge.Infrastructure.Planning.EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await service.CreateAsync(actor, new("Preserved legacy", "Unicode 文本"));
		var editor = new PlanForge.Application.Planning.PlanEditor(service);
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Preserved phase"));
		await fixture.Database.Database.ExecuteSqlRawAsync("ALTER TABLE Projects DROP COLUMN EffectiveStatusChangedAt");
		await fixture.Database.Database.ExecuteSqlRawAsync("ALTER TABLE WorkItems DROP COLUMN EffectiveStatusChangedAt");
		var migration = new PlanForge.Infrastructure.Migrations.EffectiveStatusDates();
		foreach (var command in fixture.Database.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, fixture.Database.Model))
			await fixture.Database.Database.ExecuteSqlRawAsync(command.CommandText);
		var restored = await service.GetAsync(actor, project.Id);
		Assert.Equal(project.Revision, restored.Revision);
		Assert.Equal(project.Name, restored.Name);
		Assert.Equal(project.Description, restored.Description);
		Assert.Equal(project.Items.Select(item => item.Id).Order(), restored.Items.Select(item => item.Id).Order());
		Assert.Null(restored.EffectiveStatusChangedAt);
		Assert.All(restored.Items, item => Assert.Null(item.EffectiveStatusChangedAt));
		Assert.Equal(1, fixture.Database.Users.Count());
	}
}
