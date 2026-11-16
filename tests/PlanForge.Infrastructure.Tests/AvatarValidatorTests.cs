namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Infrastructure.Accounts;
using SkiaSharp;
using System.Buffers.Binary;

public sealed class AvatarValidatorTests
{
	private static readonly IAvatarImageValidator validator = new SkiaAvatarImageValidator();

	[Fact]
	public void Validate_WhenWebpAnimationContainsOneFrame_RejectsAnimationContainer()
	{
		byte[] content = ImageTestData.AnimatedWebp;
		int position = 12;
		int frames = 0;
		while (position < content.Length)
		{
			if (content.AsSpan(position, 4).SequenceEqual("ANMF"u8) && ++frames == 2)
				break;
			int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(position + 4));
			position += 8 + length + (length & 1);
		}
		content = content[..position];
		BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(4), (uint)content.Length - 8);
		using (SKData data = SKData.CreateCopy(content))
		using (SKCodec codec = SKCodec.Create(data))
			Assert.Equal(1, codec.FrameCount);
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, "image/webp")).StatusCode);
	}

	[Theory]
	[InlineData(8193, 1)]
	[InlineData(4001, 4000)]
	public void Validate_WhenHeaderAdvertisesBomb_RejectsByDimensionsNotPixelDecode(int width, int height)
	{
		PlanForgeException exception = Assert.Throws<PlanForgeException>(() => validator.Validate(ImageTestData.PngWithDimensions(width, height), "image/png"));
		Assert.Contains("dimensions", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData(8192, 1)]
	[InlineData(4000, 4000)]
	public void Validate_AtDimensionAndPixelBounds_Accepts(int width, int height)
		=> validator.Validate(ImageTestData.Raster(SKEncodedImageFormat.Png, width, height), "image/png");

	[Theory]
	[InlineData(20)]
	[InlineData(-4)]
	public void Validate_WhenWebpRiffLengthIsTruncated_RejectsRecoverablePixels(int offset)
	{
		byte[] content = ImageTestData.Raster(SKEncodedImageFormat.Webp);
		BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(4), (uint)(content.Length - 8 + offset));
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, "image/webp")).StatusCode);
	}

	[Fact]
	public void Validate_AboveFourMiB_RejectsBeforeOpeningCodec()
		=> Assert.Equal(413, Assert.Throws<PlanForgeException>(() => validator.Validate(new byte[AvatarValidator.MaximumBytes + 1], "image/png")).StatusCode);

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/jpeg")]
	[InlineData(SKEncodedImageFormat.Jpeg, "image/webp")]
	[InlineData(SKEncodedImageFormat.Webp, "image/png")]

	public void Validate_WhenMimeSpoofedOrUnsupported_Rejects(SKEncodedImageFormat format, string mime)
		=> Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(ImageTestData.Raster(format), mime)).StatusCode);

	[Fact]
	public void Validate_WhenGif_RejectsUnsupportedRaster()
		=> Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(Convert.FromBase64String("R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw=="), "image/gif")).StatusCode);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("text/html")]
	public void Validate_WhenMimeMissingOrInvalid_Rejects(string? mime)
		=> Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(ImageTestData.Raster(SKEncodedImageFormat.Png), mime!)).StatusCode);

	[Fact]
	public void Validate_WhenEmptyOrNull_Rejects()
	{
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate([], "image/png")).StatusCode);
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(null!, "image/png")).StatusCode);
	}

	[Fact]
	public void Validate_WhenPngChecksumIsCorrupt_RejectsRecoverablePixels()
	{
		byte[] content = ImageTestData.Raster(SKEncodedImageFormat.Png);
		content[^1] ^= 1; // IEND checksum, not decoded pixel data.
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, "image/png")).StatusCode);
	}

	[Theory]
	[InlineData("image/png")]
	[InlineData("image/webp")]
	public void Validate_WhenAnimated_RejectsRatherThanSilentlyChoosingFirstFrame(string mime)
	{
		byte[] content = mime == "image/png" ? ImageTestData.AnimatedPng : ImageTestData.AnimatedWebp;
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, mime)).StatusCode);
	}

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/png", 12)]
	[InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg", 2)]
	[InlineData(SKEncodedImageFormat.Webp, "image/webp", 2)]
	public void Validate_WhenContainerIsTruncated_RejectsEvenIfCodecCanRecoverPixels(SKEncodedImageFormat format, string mime, int removed)
	{
		byte[] content = ImageTestData.Raster(format)[..^removed];
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, mime)).StatusCode);
	}

	[Theory]
	[InlineData(8193, 1)]
	[InlineData(1, 8193)]
	[InlineData(4001, 4000)]
	public void Validate_WhenDimensionsExceedBound_RejectsBeforePixelDecode(int width, int height)
	{
		byte[] content = ImageTestData.Raster(SKEncodedImageFormat.Png, width, height);
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, "image/png")).StatusCode);
	}

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/png", 8)]
	[InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg", 3)]
	[InlineData(SKEncodedImageFormat.Webp, "image/webp", 12)]
	public void Validate_WhenOnlySignatureRemains_RejectsUndecodableImage(SKEncodedImageFormat format, string mime, int length)
	{
		byte[] content = ImageTestData.Raster(format)[..length];
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => validator.Validate(content, mime)).StatusCode);
	}

	[Theory]
	[InlineData(SKEncodedImageFormat.Png, "image/png")]
	[InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
	[InlineData(SKEncodedImageFormat.Webp, "image/webp")]
	public void Validate_AtExactlyFourMiB_AcceptsCompleteRasterWithTrailingPadding(SKEncodedImageFormat format, string mime)
	{
		byte[] content = ImageTestData.Raster(format);
		Array.Resize(ref content, 4 * 1024 * 1024);
		validator.Validate(content, mime);
	}
}
