namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;

public sealed class AccountStoreFailureTests
{
	[Fact]
	public async Task SaveAsync_WhenDatabaseUnavailable_DoesNotInventAnAccountConflictAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var store = new EfAccountStore(fixture.Database);
		fixture.Database.Users.Add(new UserAccount { Email = "a@example.test", NormalizedEmail = "A@EXAMPLE.TEST", DisplayName = "test", PasswordHash = "test-only hash" });
		await fixture.Database.Database.GetDbConnection().CloseAsync();
		await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(CancellationToken.None));
	}
}
