namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;

public sealed class RegistrationValidationTests
{
	[Theory]
	[InlineData("not-an-email", "long-enough-password!", "test")]
	[InlineData("test@example.invalid", "short", "test")]
	[InlineData("test@example.invalid", "long-enough-password!", " ")]
	public async Task RegisterAsync_WhenInvalid_RejectsWithoutWritingAsync(string email, string password, string name)
	{
		await using var fixture = new AccountDatabaseFixture();

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.RegisterAsync(new RegisterRequest(email, password, name)));

		Assert.Equal(400, error.StatusCode);
		Assert.Empty(fixture.Database.Users);
		Assert.Empty(fixture.Database.AccessCredentials);
	}

	[Fact]
	public async Task RegisterAsync_WhenEmailAlreadyExists_RejectsCaseInsensitiveDuplicateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.RegisterAsync(new RegisterRequest("TEST@EXAMPLE.INVALID", "Test-only-password-739!", "second")));

		Assert.Equal(409, error.StatusCode);
		Assert.Single(fixture.Database.Users);
	}
}
