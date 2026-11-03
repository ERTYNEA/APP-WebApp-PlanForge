namespace PlanForge.Infrastructure.Tests;

using PlanForge.Infrastructure.Database;

public sealed class SqlDeploymentPolicyTests
{
	[Theory]
	[InlineData("Encrypt=False;TrustServerCertificate=False;User ID=planforge_migrator")]
	[InlineData("Encrypt=True;TrustServerCertificate=True;User ID=planforge_migrator")]
	[InlineData("Encrypt=True;TrustServerCertificate=False;User ID=planforge_app")]
	public void ValidateDeployment_WhenTlsOrIdentityUnsafe_Rejects(string options)
	{
		Assert.Throws<InvalidOperationException>(() => SqlConnectionSettings.ValidateDeployment("Server=planforge-sql.database.windows.net;Database=PlanForge-DB;" + options));
	}
}
