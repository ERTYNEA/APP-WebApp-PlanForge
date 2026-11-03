namespace PlanForge.Infrastructure.Tests;

public sealed class AgentMappingTests
{
	[Fact]
	public async Task AgentModel_WhenInspected_HasOwnerKeyAndIndependentAvatarStorageAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var agent = fixture.Database.Model.FindEntityType("PlanForge.Domain.Agents.Agent");
		Assert.NotNull(agent);
		Assert.NotNull(agent.FindProperty("AvatarBytes"));
		Assert.Contains(agent.GetKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(new[] { "OwnerUserId", "Id" }));
		var credential = fixture.Database.Model.FindEntityType("PlanForge.Domain.Accounts.AccessCredential")!;
		Assert.Contains(credential.GetForeignKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(new[] { "OwnerUserId", "AgentId" }));
	}
}
