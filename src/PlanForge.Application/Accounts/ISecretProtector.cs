namespace PlanForge.Application.Accounts;

public interface ISecretProtector
{
	string CreateToken(bool isAgent);
	string HashPassword(string password);
	string HashToken(string token);
	bool VerifyPassword(string hash, string password);
}