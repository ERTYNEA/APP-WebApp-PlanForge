namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;

public sealed class AvatarValidatorTests
{
	[Fact]
	public void Validate_WhenRasterSignatureAndSizeValid_AcceptsAndRejectsSpoofedOrOversizedContent()
	{
		byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		AvatarValidator.Validate(png, "image/png");
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => AvatarValidator.Validate(png, "text/html")).StatusCode);
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => AvatarValidator.Validate([1, 2, 3], "image/png")).StatusCode);
		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => AvatarValidator.Validate(new byte[AvatarValidator.MaximumBytes + 1], "image/png")).StatusCode);
	}
}
