namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.SqlClient;
using PlanForge.Infrastructure.Database;

public sealed class SqlConnectionSettingsTests
{
	[Fact]
	public void FromCredentialLines_WhenPasswordContainsDelimiters_PreservesPasswordAndEnforcesTls()
	{
		const string syntheticPassword = "test-only;=\"$value'";
		string result = SqlConnectionSettings.FromCredentialLines(["SQL_USERNAME=planforge_app", "SQL_PASSWORD=" + syntheticPassword]);
		var settings = new SqlConnectionStringBuilder(result);

		Assert.Equal(syntheticPassword, settings.Password);
		Assert.Equal("planforge_app", settings.UserID);
		Assert.Equal("PlanForge-DB", settings.InitialCatalog);
		Assert.False(settings.TrustServerCertificate);
		Assert.NotEqual(SqlConnectionEncryptOption.Optional, settings.Encrypt);
	}
}
