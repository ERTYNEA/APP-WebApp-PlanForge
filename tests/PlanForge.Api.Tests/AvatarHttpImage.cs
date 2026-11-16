namespace PlanForge.Api.Tests;

using SkiaSharp;

internal static class AvatarHttpImage
{
	public static byte[] Create()
	{
		using var bitmap = new SKBitmap(20, 10);
		bitmap.Erase(SKColors.CornflowerBlue);
		using var raster = SKImage.FromBitmap(bitmap);
		using var encoded = raster.Encode(SKEncodedImageFormat.Png, 100);
		return encoded.ToArray();
	}
}
