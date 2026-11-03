namespace PlanForge.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Planning;

public sealed class PlanForgeDbContext(DbContextOptions<PlanForgeDbContext> options) : DbContext(options)
{
	public DbSet<AccessCredential> AccessCredentials => Set<AccessCredential>();
	public DbSet<UserAccount> Users => Set<UserAccount>();
	public DbSet<Project> Projects => Set<Project>();
	public DbSet<ProjectChange> ProjectChanges => Set<ProjectChange>();
	public DbSet<WorkItem> WorkItems => Set<WorkItem>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ProjectChange>(entity =>
		{
			entity.HasKey(change => change.Id);
			entity.Property(change => change.Action).HasMaxLength(100).IsRequired();
			entity.Property(change => change.ActorKind).HasMaxLength(10).IsRequired();
			entity.HasIndex(change => new { change.ProjectId, change.Id });
			entity.HasOne<Project>().WithMany().HasForeignKey(change => change.ProjectId).OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity<Project>(entity =>
		{
			entity.HasKey(project => project.Id);
			entity.Property(project => project.Name).HasMaxLength(160).IsRequired();
			entity.Property(project => project.Description).HasMaxLength(8000).IsRequired();
			entity.Property(project => project.Revision).IsConcurrencyToken();
			entity.HasIndex(project => new { project.OwnerUserId, project.UpdatedAt });
			entity.HasOne<UserAccount>().WithMany().HasForeignKey(project => project.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity<WorkItem>(entity =>
		{
			entity.HasKey(item => item.Id);
			entity.HasAlternateKey(item => new { item.ProjectId, item.Id });
			entity.Property(item => item.Title).HasMaxLength(240).IsRequired();
			entity.Property(item => item.Description).HasMaxLength(16000).IsRequired();
			entity.Property(item => item.Resolution).HasMaxLength(8000).IsRequired();
			entity.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
			entity.HasOne<WorkItem>().WithMany().HasForeignKey(item => new { item.ProjectId, item.ParentId }).HasPrincipalKey(item => new { item.ProjectId, item.Id }).OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity<UserAccount>(entity =>
		{
			entity.HasKey(user => user.Id);
			entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
			entity.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
			entity.Property(user => user.DisplayName).HasMaxLength(80).IsRequired();
			entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
			entity.HasIndex(user => user.NormalizedEmail).IsUnique();
		});
		modelBuilder.Entity<AccessCredential>(entity =>
		{
			entity.HasKey(credential => credential.Id);
			entity.Property(credential => credential.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
			entity.Property(credential => credential.Name).HasMaxLength(80).IsRequired();
			entity.HasIndex(credential => credential.TokenHash).IsUnique();
			entity.HasIndex(credential => new { credential.OwnerUserId, credential.Kind });
			entity.HasOne<UserAccount>().WithMany().HasForeignKey(credential => credential.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
		});
	}

}
