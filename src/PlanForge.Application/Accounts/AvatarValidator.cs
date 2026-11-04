namespace PlanForge.Application.Accounts;

using PlanForge.Application.Common;

public static class AvatarValidator
{
	public const int MaximumBytes = 256 * 1024;
	public static void Validate(byte[] content, string contentType)
	{
		if (content is null || content.Length == 0)
			throw new PlanForgeException(400, "Choose a PNG, JPEG or WebP avatar image.");
		if (content.Length > MaximumBytes)
			throw new PlanForgeException(413, "The avatar exceeds the 256 KiB limit.");
		bool valid = contentType?.ToLowerInvariant() switch
		{
			"image/png" => content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
			"image/jpeg" => content.Length >= 3 && content[0] == 255 && content[1] == 216 && content[2] == 255,
			"image/webp" => content.Length >= 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
			_ => false
		};
		if (!valid)
			throw new PlanForgeException(400, "The avatar MIME type and raster image signature do not match.");
	}
}
