namespace PlanForge.Infrastructure.Accounts;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PlanForge.Application.Accounts;
using System.Security.Cryptography;
using System.Text;

public sealed class SecretProtector : ISecretProtector
{
	private readonly PasswordHasher<object> passwordHasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

	public string CreateToken(bool isAgent)
		=> (isAgent ? "pf_a_" : "pf_h_") + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

	public string HashPassword(string password) => passwordHasher.HashPassword(this, password);
	public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
	public bool VerifyPassword(string hash, string password) => passwordHasher.VerifyHashedPassword(this, hash, password) != PasswordVerificationResult.Failed;
}
