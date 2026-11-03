namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;

public sealed class LibraryMappingTests
{
	[Fact]
	public async Task LibraryModel_WhenInspected_UsesIndependentLinksAndNeverCascadesBinaryDeletionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var resource = fixture.Database.Model.FindEntityType("PlanForge.Domain.Library.Resource");
		Assert.NotNull(resource);
		Assert.NotNull(resource.FindProperty("Content"));
		Assert.True(resource.FindProperty("Revision")!.IsConcurrencyToken);
		var projectLink = fixture.Database.Model.FindEntityType("PlanForge.Domain.Library.ProjectResource");
		var phaseLink = fixture.Database.Model.FindEntityType("PlanForge.Domain.Library.PhaseResource");
		Assert.NotNull(projectLink);
		Assert.NotNull(phaseLink);
		Assert.Equal(2, projectLink.FindPrimaryKey()!.Properties.Count);
		Assert.Equal(2, phaseLink.FindPrimaryKey()!.Properties.Count);
		Assert.All(resource.GetForeignKeys().Concat(projectLink.GetForeignKeys()).Concat(phaseLink.GetForeignKeys()), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
		Assert.DoesNotContain(phaseLink.GetForeignKeys(), key => key.PrincipalEntityType == projectLink);
	}
}
