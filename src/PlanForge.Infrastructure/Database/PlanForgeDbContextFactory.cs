namespace PlanForge.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class PlanForgeDbContextFactory : IDesignTimeDbContextFactory<PlanForgeDbContext>
{
	public PlanForgeDbContext CreateDbContext(string[] args)
	{
		string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlanForgeDb")
			?? "Server=localhost;Database=PlanForge-DesignOnly;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
		return new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlServer(connectionString).Options);
	}
}