namespace PlanForge.Infrastructure.Tests;

public sealed class AccountAvatarMappingTests
{
	[Fact]
	public async Task AccountModel_WhenInspected_KeepsAvatarSeparateFromLibraryAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var user = fixture.Database.Model.FindEntityType("PlanForge.Domain.Accounts.UserAccount")!;
		Assert.NotNull(user.FindProperty("AvatarBytes"));
		Assert.NotNull(user.FindProperty("AvatarContentType"));
		Assert.DoesNotContain(user.GetForeignKeys(), key => key.PrincipalEntityType.Name.Contains("Resource", StringComparison.Ordinal));
	}
}
