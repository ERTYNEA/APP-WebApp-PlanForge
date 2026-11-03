namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;

public sealed class AccountMappingTests
{
	[Fact]
	public async Task SaveAsync_WhenEmailDuplicates_DatabaseRejectsDuplicateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		fixture.Database.Users.AddRange(
			new UserAccount { Email = "one@example.invalid", NormalizedEmail = "ONE@EXAMPLE.INVALID", DisplayName = "one", PasswordHash = "synthetic-hash" },
			new UserAccount { Email = "ONE@example.invalid", NormalizedEmail = "ONE@EXAMPLE.INVALID", DisplayName = "two", PasswordHash = "synthetic-hash" });

		await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Database.SaveChangesAsync());
	}

	[Fact]
	public async Task SaveAsync_WhenCredentialOwnerMissing_DatabaseRejectsOrphanCredentialAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		fixture.Database.AccessCredentials.Add(new AccessCredential { OwnerUserId = Guid.NewGuid(), TokenHash = new string('A', 64) });

		await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Database.SaveChangesAsync());
	}
}
