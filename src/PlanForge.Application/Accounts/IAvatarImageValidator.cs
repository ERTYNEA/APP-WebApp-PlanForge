namespace PlanForge.Application.Accounts;

/// <summary>Validates a complete, bounded, nonanimated PNG, JPEG or WebP before admission.</summary>
public interface IAvatarImageValidator
{
	void Validate(byte[] content, string contentType);
}
