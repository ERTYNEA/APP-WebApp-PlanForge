namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Infrastructure.Accounts;
using SkiaSharp;

public sealed class AvatarImageConverterTests
{
	private readonly IAvatarConverter converter = new SkiaAvatarImageConverter(new SkiaAvatarImageValidator());

	[Fact]
	public async Task ConvertAsync_WhenPngContainsPrivateText_StripsMetadataAsync()
	{
		byte[] content = ImageTestData.PngWithPrivateText();
		Assert.True(content.AsSpan().IndexOf("private-avatar-test"u8) >= 0);
		byte[] result = await converter.ConvertAsync(content, "image/png");
		Assert.False(result.AsSpan().IndexOf("private-avatar-test"u8) >= 0);
		Assert.False(result.AsSpan().IndexOf("EXIF"u8) >= 0);
		Assert.False(result.AsSpan().IndexOf("XMP "u8) >= 0);
		new SkiaAvatarImageValidator().Validate(result, "image/webp");
	}

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/png")]
	[InlineData(SKEncodedImageFormat.Webp, "image/webp")]
	public async Task ConvertAsync_WhenAlphaPresent_PreservesTransparentPartialAndOpaquePixelsAsync(SKEncodedImageFormat format, string mime)
	{
		using SKBitmap source = new(1024, 512, SKColorType.Rgba8888, SKAlphaType.Premul);
		source.Erase(new SKColor(200, 30, 100, 128));
		using (SKCanvas canvas = new(source))
		{
			using SKPaint paint = new() { Color = SKColors.Transparent, BlendMode = SKBlendMode.Src };
			canvas.DrawRect(0, 0, 256, 512, paint);
			paint.Color = SKColors.Blue;
			canvas.DrawRect(768, 0, 256, 512, paint);
		}
		using SKImage image = SKImage.FromBitmap(source);
		using SKData input = image.Encode(format, 100);
		byte[] result = await converter.ConvertAsync(input.ToArray(), mime);
		using SKBitmap bitmap = SKBitmap.Decode(result);
		Assert.Equal(512, bitmap.Width);
		Assert.Equal(0, bitmap.GetPixel(30, 50).Alpha);
		Assert.InRange(bitmap.GetPixel(250, 50).Alpha, (byte)127, (byte)129);
		Assert.Equal(255, bitmap.GetPixel(480, 50).Alpha);
		Assert.InRange(bitmap.GetPixel(250, 50).Red, (byte)185, (byte)215);
	}

	[Fact]
	public async Task ConvertAsync_WhenCancelled_ThrowsBeforeReadingInputAsync()
	{
		using CancellationTokenSource source = new();
		source.Cancel();
		OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.ConvertAsync([], "image/png", source.Token));
		Assert.Equal(source.Token, exception.CancellationToken);
	}

	[Theory]
	[InlineData(1, "RGBY")]
	[InlineData(2, "GRYB")]
	[InlineData(3, "YBGR")]
	[InlineData(4, "BYRG")]
	[InlineData(5, "RBGY")]
	[InlineData(6, "BRYG")]
	[InlineData(7, "YGBR")]
	[InlineData(8, "GYRB")]
	public async Task ConvertAsync_WhenExifOriented_TransformsAllCornersAndStripsOrientationAsync(byte orientation, string corners)
	{
		byte[] input = ImageTestData.OrientedJpeg(orientation);
		using (SKData source = SKData.CreateCopy(input))
		using (SKCodec codec = SKCodec.Create(source))
			Assert.Equal((SKEncodedOrigin)orientation, codec.EncodedOrigin);
		byte[] result = await converter.ConvertAsync(input, "image/jpeg");
		using SKBitmap bitmap = SKBitmap.Decode(result);
		Assert.Equal(orientation >= 5 ? 20 : 40, bitmap.Width);
		Assert.Equal(orientation >= 5 ? 40 : 20, bitmap.Height);
		SKColor[] actual = [bitmap.GetPixel(3, 3), bitmap.GetPixel(bitmap.Width - 4, 3), bitmap.GetPixel(3, bitmap.Height - 4), bitmap.GetPixel(bitmap.Width - 4, bitmap.Height - 4)];
		for (int index = 0; index < corners.Length; index++)
		{
			SKColor expected = corners[index] switch { 'R' => SKColors.Red, 'G' => SKColors.Lime, 'B' => SKColors.Blue, _ => SKColors.Yellow };
			Assert.InRange(Math.Abs(expected.Red - actual[index].Red), 0, 35);
			Assert.InRange(Math.Abs(expected.Green - actual[index].Green), 0, 35);
			Assert.InRange(Math.Abs(expected.Blue - actual[index].Blue), 0, 35);
		}
		using SKData data = SKData.CreateCopy(result);
		using SKCodec output = SKCodec.Create(data);
		Assert.Equal(SKEncodedOrigin.TopLeft, output.EncodedOrigin);
		Assert.False(result.AsSpan().IndexOf("EXIF"u8) >= 0);
	}

	[Theory]
	[InlineData("spoofed")]
	[InlineData("corrupt")]
	[InlineData("animated")]
	public async Task ConvertAsync_WhenInputIsInvalid_RejectsBeforeDecodingAsync(string kind)
	{
		byte[] content = kind switch
		{
			"corrupt" => ImageTestData.Raster(SKEncodedImageFormat.Png)[..8],
			"animated" => ImageTestData.AnimatedPng,
			_ => ImageTestData.Raster(SKEncodedImageFormat.Png)
		};
		string mime = kind == "spoofed" ? "image/jpeg" : "image/png";
		PlanForgeException exception = await Assert.ThrowsAsync<PlanForgeException>(() => converter.ConvertAsync(content, mime));
		Assert.Equal(400, exception.StatusCode);
	}

	[Theory]
	[InlineData(1600, 800, 512, 256)]
	[InlineData(800, 1600, 256, 512)]
	[InlineData(1600, 1600, 512, 512)]
	[InlineData(17, 9, 17, 9)]
	[InlineData(1, 8192, 1, 512)]
	public async Task ConvertAsync_WhenResizing_PreservesProportionWithin512WithoutUpscaleAsync(int width, int height, int expectedWidth, int expectedHeight)
	{
		byte[] result = await converter.ConvertAsync(ImageTestData.Raster(SKEncodedImageFormat.Png, width, height), "image/png");
		using SKBitmap bitmap = SKBitmap.Decode(result);
		Assert.Equal(expectedWidth, bitmap.Width);
		Assert.Equal(expectedHeight, bitmap.Height);
	}

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/png")]
	[InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
	[InlineData(SKEncodedImageFormat.Webp, "image/webp")]
	public async Task ConvertAsync_WhenValid_ProducesDecodableWebpAsync(SKEncodedImageFormat format, string mime)
	{
		byte[] result = await converter.ConvertAsync(ImageTestData.Raster(format), mime);
		using SKData data = SKData.CreateCopy(result);
		using SKCodec codec = SKCodec.Create(data);
		Assert.NotNull(codec);
		Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
		using SKBitmap bitmap = SKBitmap.Decode(result);
		Assert.NotNull(bitmap);
		Assert.Equal(SKColors.CornflowerBlue.Red, bitmap.GetPixel(0, 0).Red, tolerance: 12);
	}
}
