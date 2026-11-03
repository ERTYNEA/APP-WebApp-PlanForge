namespace PlanForge.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;

public sealed class PlanForgeDbContext(DbContextOptions<PlanForgeDbContext> options) : DbContext(options)
{
	public DbSet<AccessCredential> AccessCredentials => Set<AccessCredential>();
	public DbSet<UserAccount> Users => Set<UserAccount>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
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