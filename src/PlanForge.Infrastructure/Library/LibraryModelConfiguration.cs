namespace PlanForge.Infrastructure.Library;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Library;
using PlanForge.Domain.Planning;

public static class LibraryModelConfiguration
{
	public static void Configure(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Resource>(entity =>
		{
			entity.HasKey(resource => resource.Id);
			entity.Property(resource => resource.Content).IsRequired();
			entity.Property(resource => resource.ContentType).HasMaxLength(120).IsRequired();
			entity.Property(resource => resource.FileName).HasMaxLength(180).IsRequired();
			entity.Property(resource => resource.Title).HasMaxLength(240).IsRequired();
			entity.Property(resource => resource.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
			entity.Property(resource => resource.Revision).IsConcurrencyToken();
			entity.HasIndex(resource => new { resource.OwnerUserId, resource.Sha256 });
			entity.HasOne<UserAccount>().WithMany().HasForeignKey(resource => resource.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity<ProjectResource>(entity =>
		{
			entity.HasKey(link => new { link.ProjectId, link.ResourceId });
			entity.HasOne<Project>().WithMany().HasForeignKey(link => link.ProjectId).OnDelete(DeleteBehavior.Restrict);
			entity.HasOne<Resource>().WithMany().HasForeignKey(link => link.ResourceId).OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity<PhaseResource>(entity =>
		{
			entity.HasKey(link => new { link.PhaseId, link.ResourceId });
			entity.HasOne<WorkItem>().WithMany().HasForeignKey(link => link.PhaseId).OnDelete(DeleteBehavior.Restrict);
			entity.HasOne<Resource>().WithMany().HasForeignKey(link => link.ResourceId).OnDelete(DeleteBehavior.Restrict);
		});
	}
}
