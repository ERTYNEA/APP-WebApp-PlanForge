namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Text.Json;

public sealed class PlanWriteFenceTests
{
	[Fact]
	public async Task LegacyRevisionWriter_IsRejectedEvenAfterNewWriterAndRollsBackAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("fence@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		var snapshot = await projects.CreateAsync(actor, new("Fence fixture"));
		snapshot = await editor.SaveItemAsync(actor, snapshot.Id, "Phase", null, new(snapshot.Revision, "Phase"));
		snapshot = await editor.SaveItemAsync(actor, snapshot.Id, "Step", null, new(snapshot.Revision, "Step", snapshot.Items.Single(item => item.Kind == "Subphase").Id));
		string before = Canonical(await projects.GetAsync(actor, snapshot.Id));
		long[] events = await fixture.Database.ProjectChanges.Select(change => change.Id).ToArrayAsync();
		await using (var transaction = await fixture.Database.Database.BeginTransactionAsync())
		{
			// This is exactly the old binary's revision gate, deliberately omitting the new shadow field.
			var exception = await Assert.ThrowsAsync<SqliteException>(() => fixture.Database.Database.ExecuteSqlInterpolatedAsync($"UPDATE Projects SET Revision = Revision + 1, Status = 2 WHERE Id = {snapshot.Id} AND Revision = {snapshot.Revision}"));
			Assert.Equal(19, exception.SqliteErrorCode);
			await transaction.RollbackAsync();
		}
		Assert.Equal(before, Canonical(await projects.GetAsync(actor, snapshot.Id)));
		Assert.Equal(events, await fixture.Database.ProjectChanges.Select(change => change.Id).ToArrayAsync());
		await projects.DeleteAsync(actor, snapshot.Id, snapshot.Revision, true);
		Assert.Empty(await fixture.Database.Projects.ToListAsync());
	}

	[Fact]
	public async Task Version03Writer_CannotOverwriteVersion04CalculatedStatesAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("v04-fence@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var snapshot = await projects.CreateAsync(actor, new("Fence fixture"));
		await using var transaction = await fixture.Database.Database.BeginTransactionAsync();
		var exception = await Assert.ThrowsAsync<SqliteException>(() => fixture.Database.Database.ExecuteSqlInterpolatedAsync($"UPDATE Projects SET Revision = Revision + 1, WriteRevision = Revision + 1 WHERE Id = {snapshot.Id}"));
		Assert.Equal(19, exception.SqliteErrorCode);
		await transaction.RollbackAsync();
		Assert.Equal(snapshot.Revision, (await projects.GetAsync(actor, snapshot.Id)).Revision);
	}

	private static string Canonical(ProjectSnapshot snapshot)
		=> JsonSerializer.Serialize(snapshot with { Items = snapshot.Items.OrderBy(item => item.Id).ToList() });
}
