namespace PlanForge.Infrastructure.Tests;

using SkiaSharp;
using System.Buffers.Binary;

internal static class ImageTestData
{
	public static byte[] PngWithDimensions(int width, int height)
	{
		byte[] content = Raster(SKEncodedImageFormat.Png);
		BinaryPrimitives.WriteInt32BigEndian(content.AsSpan(16), width);
		BinaryPrimitives.WriteInt32BigEndian(content.AsSpan(20), height);
		BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(29), Crc32(content.AsSpan(12, 17)));
		return content;
	}

	public static byte[] PngWithPrivateText()
	{
		byte[] content = Raster(SKEncodedImageFormat.Png);
		byte[] text = "Comment\0private-avatar-test"u8.ToArray();
		byte[] chunk = new byte[text.Length + 12];
		BinaryPrimitives.WriteInt32BigEndian(chunk, text.Length);
		"tEXt"u8.CopyTo(chunk.AsSpan(4));
		text.CopyTo(chunk.AsSpan(8));
		BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunk.Length - 4), Crc32(chunk.AsSpan(4, chunk.Length - 8)));
		return [.. content[..^12], .. chunk, .. content[^12..]];
	}

	public static byte[] OrientedJpeg(byte orientation)
	{
		using SKBitmap bitmap = new(40, 20, SKColorType.Rgba8888, SKAlphaType.Premul);
		for (int y = 0; y < bitmap.Height; y++)
			for (int x = 0; x < bitmap.Width; x++)
				bitmap.SetPixel(x, y, y < 10 ? (x < 20 ? SKColors.Red : SKColors.Lime) : (x < 20 ? SKColors.Blue : SKColors.Yellow));
		using SKImage image = SKImage.FromBitmap(bitmap);
		using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
		byte[] jpeg = data.ToArray();
		// APP1 EXIF, little-endian TIFF, one SHORT Orientation tag.
		byte[] exif = [0xff, 0xe1, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, orientation, 0, 0, 0, 0, 0, 0, 0];
		return [.. jpeg[..2], .. exif, .. jpeg[2..]];
	}

	// Real two-frame files encoded by Pillow 13.0.0 from 2x2 RGBA frames.
	public static byte[] AnimatedPng => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAACAAAAAgAAAAAAAAAAAAEACgAA6FTcAAAAABRJREFUeJxj/M/A8J+BgYGBiQEKAB8XAgJPlM6+AAAAGmZjVEwAAAABAAAAAgAAAAIAAAAAAAAAAAABAAoAAHMnNtQAAAAYZmRBVAAAAAJ4nGNkYPjfwMDAwMDEAAUAFicBg6M283QAAAAASUVORK5CYII=");
	public static byte[] AnimatedWebp => Convert.FromBase64String("UklGRtIAAABXRUJQVlA4WAoAAAASAAAAAQAAAQAAQU5JTQYAAAAAAAAAAABBTk1GSgAAAAAAAAAAAAEAAAEAAGQAAAJWUDggMgAAADABAJ0BKgIAAgABQCYloAADcAD+8ut///mwP/bz/wR6Af//0uD//pcH//S4P/SkAAAAQU5NRlQAAAAAAAAAAAABAAABAABkAAACQUxQSAUAAAAAgICAgABWUDggLgAAADABAJ0BKgIAAgABQCYloAADcAD++1Xj//9Lg//6XB//0uD/0uD/+tXlV6yroAA=");

	public static byte[] Raster(SKEncodedImageFormat format, int width = 32, int height = 16, bool transparent = false)
	{
		using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
		bitmap.Erase(transparent ? SKColors.Transparent : SKColors.CornflowerBlue);
		using SKImage image = SKImage.FromBitmap(bitmap);
		using SKData data = image.Encode(format, 95);
		return data.ToArray();
	}

	private static uint Crc32(ReadOnlySpan<byte> bytes)
	{
		uint crc = uint.MaxValue;
		foreach (byte value in bytes)
		{
			crc ^= value;
			for (int bit = 0; bit < 8; bit++)
				crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
		}
		return ~crc;
	}
}
