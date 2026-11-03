namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;

public sealed class ProjectMappingTests
{
	[Fact]
	public async Task ProjectModel_WhenInspected_HasOwnerScopedRevisionAndRelationalHierarchyAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var project = fixture.Database.Model.FindEntityType("PlanForge.Domain.Planning.Project");
		Assert.NotNull(project);
		Assert.True(project.FindProperty("Revision")!.IsConcurrencyToken);
		Assert.Contains(project.GetForeignKeys(), key => key.Properties.Single().Name == "OwnerUserId");
		var item = fixture.Database.Model.FindEntityType("PlanForge.Domain.Planning.WorkItem");
		Assert.NotNull(item);
		Assert.Contains(item.GetForeignKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(new[] { "ProjectId", "ParentId" }));
		Assert.All(item.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
		Assert.NotNull(fixture.Database.Model.FindEntityType("PlanForge.Domain.Planning.ProjectChange"));
	}
}
