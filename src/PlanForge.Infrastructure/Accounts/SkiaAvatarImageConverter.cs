namespace PlanForge.Infrastructure.Accounts;

using PlanForge.Application.Accounts;
using SkiaSharp;

/// <summary>Rasterizes into a fresh WebP without copying source metadata; cancellation is cooperative between native stages.</summary>
public sealed class SkiaAvatarImageConverter(IAvatarImageValidator validator) : IAvatarConverter
{
	public Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		validator.Validate(bytes, contentType);
		cancellationToken.ThrowIfCancellationRequested();
		using SKData data = SKData.CreateCopy(bytes);
		using SKCodec codec = SKCodec.Create(data);
		using SKBitmap bitmap = SKBitmap.Decode(codec);
		cancellationToken.ThrowIfCancellationRequested();
		bool swapAxes = (int)codec.EncodedOrigin >= 5;
		int orientedWidth = swapAxes ? bitmap.Height : bitmap.Width;
		int orientedHeight = swapAxes ? bitmap.Width : bitmap.Height;
		double scale = Math.Min(1, 512d / Math.Max(orientedWidth, orientedHeight));
		int width = Math.Max(1, (int)Math.Round(orientedWidth * scale));
		int height = Math.Max(1, (int)Math.Round(orientedHeight * scale));
		using SKBitmap resized = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
		using (SKCanvas canvas = new(resized))
		using (SKImage source = SKImage.FromBitmap(bitmap))
		{
			canvas.Clear(SKColors.Transparent);
			canvas.Scale((float)width / orientedWidth, (float)height / orientedHeight);
			SKMatrix orientation = GetOrientation(codec.EncodedOrigin, bitmap.Width, bitmap.Height);
			canvas.Concat(orientation);
			canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
		}
		using SKImage image = SKImage.FromBitmap(resized);
		cancellationToken.ThrowIfCancellationRequested();
		using SKData encoded = image.Encode(SKEncodedImageFormat.Webp, 90);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(encoded.ToArray());
	}

	private static SKMatrix GetOrientation(SKEncodedOrigin origin, int width, int height)
		=> (int)origin switch
		{
			2 => new(-1, 0, width, 0, 1, 0, 0, 0, 1),
			3 => new(-1, 0, width, 0, -1, height, 0, 0, 1),
			4 => new(1, 0, 0, 0, -1, height, 0, 0, 1),
			5 => new(0, 1, 0, 1, 0, 0, 0, 0, 1),
			6 => new(0, -1, height, 1, 0, 0, 0, 0, 1),
			7 => new(0, -1, height, -1, 0, width, 0, 0, 1),
			8 => new(0, 1, 0, -1, 0, width, 0, 0, 1),
			_ => SKMatrix.Identity
		};
}
