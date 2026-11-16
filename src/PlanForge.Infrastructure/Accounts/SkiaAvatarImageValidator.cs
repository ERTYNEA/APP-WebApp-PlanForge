namespace PlanForge.Infrastructure.Accounts;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using SkiaSharp;
using System.Buffers.Binary;

public sealed class SkiaAvatarImageValidator : IAvatarImageValidator
{
	public const int MaximumBytes = AvatarValidator.MaximumBytes;

	public void Validate(byte[] content, string contentType)
	{
		if (content is null || content.Length == 0)
			throw InvalidImage();
		if (content.Length > MaximumBytes)
			throw new PlanForgeException(413, "The avatar exceeds the 4 MiB limit.");

		using SKData data = SKData.CreateCopy(content);
		using SKCodec? codec = SKCodec.Create(data);
		if (codec is null)
			throw InvalidImage();
		string? actualMime = codec.EncodedFormat switch
		{
			SKEncodedImageFormat.Png => "image/png",
			SKEncodedImageFormat.Jpeg => "image/jpeg",
			SKEncodedImageFormat.Webp => "image/webp",
			_ => null
		};
		if (actualMime is null || !string.Equals(actualMime, contentType, StringComparison.OrdinalIgnoreCase))
			throw InvalidImage();

		if (codec.FrameCount > 1)
			throw new PlanForgeException(400, "Animated avatar images are not supported.");
		if (codec.EncodedFormat == SKEncodedImageFormat.Png)
			ValidatePngContainer(content);
		if (codec.EncodedFormat == SKEncodedImageFormat.Webp)
			ValidateWebpContainer(content);

		// Bound decoded RGBA pixels to 64 MB before allocating any pixel buffer.
		if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 8192 || codec.Info.Height > 8192
			|| (long)codec.Info.Width * codec.Info.Height > 16_000_000)
			throw new PlanForgeException(400, "Avatar dimensions must not exceed 8192 per axis or 16 megapixels.");

		using SKBitmap bitmap = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
		if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
			throw InvalidImage();
	}

	private static void ValidatePngContainer(ReadOnlySpan<byte> content)
	{
		int position = 8;
		while (position <= content.Length - 12)
		{
			uint length = BinaryPrimitives.ReadUInt32BigEndian(content[position..]);
			if (length > content.Length - position - 12)
				throw InvalidImage();
			uint checksum = uint.MaxValue;
			foreach (byte value in content.Slice(position + 4, (int)length + 4))
			{
				checksum ^= value;
				for (int bit = 0; bit < 8; bit++)
					checksum = (checksum >> 1) ^ ((checksum & 1) == 0 ? 0 : 0xEDB88320u);
			}
			if (~checksum != BinaryPrimitives.ReadUInt32BigEndian(content[(position + 8 + (int)length)..]))
				throw InvalidImage();

			ReadOnlySpan<byte> type = content.Slice(position + 4, 4);
			// APNG may otherwise be decoded as an ordinary PNG by the native codec.
			if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
				throw new PlanForgeException(400, "Animated avatar images are not supported.");
			if (type.SequenceEqual("IEND"u8))
			{
				if (length != 0)
					throw InvalidImage();
				return;
			}
			position += (int)length + 12;
		}
		throw InvalidImage();
	}

	private static void ValidateWebpContainer(ReadOnlySpan<byte> content)
	{
		uint length = BinaryPrimitives.ReadUInt32LittleEndian(content[4..]);
		if (length > content.Length - 8 || length < 4)
			throw InvalidImage();
		int end = (int)length + 8;
		int position = 12;
		while (position < end)
		{
			if (position > end - 8)
				throw InvalidImage();
			uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(content[(position + 4)..]);
			if (chunkLength > end - position - 8)
				throw InvalidImage();
			ReadOnlySpan<byte> type = content.Slice(position, 4);
			if (type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8)
				|| (type.SequenceEqual("VP8X"u8) && chunkLength > 0 && (content[position + 8] & 2) != 0))
				throw new PlanForgeException(400, "Animated avatar images are not supported.");
			position += 8 + (int)chunkLength + ((int)chunkLength & 1);
		}
		if (position != end)
			throw InvalidImage();
	}

	private static PlanForgeException InvalidImage()
		=> new(400, "Choose a complete, valid PNG, JPEG or WebP avatar image matching its MIME type.");
}
