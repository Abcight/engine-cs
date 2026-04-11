using Engine;
using Engine.Graphics.Shaders;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Engine.Graphics.Resources;

public readonly record struct DecodedImage2D(
	int Width,
	int Height,
	TextureFormat Format,
	byte[] Pixels
);

public interface IImageDecoder {
	Result<DecodedImage2D, GraphicsError> Decode(
		ReadOnlySpan<byte> encodedBytes,
		bool flipVertically = false
	);
}

public static class ImageDecoders {
	private static IImageDecoder _current = new ImageSharpImageDecoder();

	public static IImageDecoder Current => _current;

	public static Result<GraphicsError> SetDecoder(IImageDecoder decoder) {
		if (decoder is null) {
			return GraphicsError.InvalidArgument("Image decoder cannot be null.");
		}

		_current = decoder;
		return Unit.Value;
	}

	public static Result<DecodedImage2D, GraphicsError> DecodeFile(
		string path,
		bool flipVertically = false
	) {
		if (string.IsNullOrWhiteSpace(path)) {
			return GraphicsError.InvalidArgument("Image path cannot be null or empty.");
		}

		string resolvedPath = ResolvePath(path);
		if (!File.Exists(resolvedPath)) {
			return GraphicsError.InvalidArgument($"Image file does not exist: '{path}'.");
		}

		byte[] encodedBytes;
		try {
			encodedBytes = File.ReadAllBytes(resolvedPath);
		}
		catch (Exception ex) {
			return GraphicsError.BackendFailure($"Failed to read image file '{path}': {ex.Message}");
		}

		return _current.Decode(encodedBytes, flipVertically);
	}

	private static string ResolvePath(string path) {
		if (Path.IsPathRooted(path)) {
			return path;
		}

		string fromAppBase = Path.Combine(AppContext.BaseDirectory, path);
		if (File.Exists(fromAppBase)) {
			return fromAppBase;
		}

		return Path.GetFullPath(path);
	}
}

internal sealed class ImageSharpImageDecoder : IImageDecoder {
	public Result<DecodedImage2D, GraphicsError> Decode(
		ReadOnlySpan<byte> encodedBytes,
		bool flipVertically = false
	) {
		if (encodedBytes.IsEmpty) {
			return GraphicsError.InvalidArgument("Encoded image payload cannot be empty.");
		}

		try {
			using Image<Rgba32> image = Image.Load<Rgba32>(encodedBytes);
			if (flipVertically) {
				image.Mutate(static operation => operation.Flip(FlipMode.Vertical));
			}

			int byteCount = checked(image.Width * image.Height * 4);
			var pixels = new byte[byteCount];
			image.CopyPixelDataTo(pixels);

			return new DecodedImage2D(image.Width, image.Height, TextureFormat.RGBA8, pixels);
		}
		catch (UnknownImageFormatException ex) {
			return GraphicsError.InvalidArgument($"Unsupported image format: {ex.Message}");
		}
		catch (ImageFormatException ex) {
			return GraphicsError.InvalidArgument($"Image decode failed: {ex.Message}");
		}
		catch (Exception ex) {
			return GraphicsError.BackendFailure($"Image decode failed unexpectedly: {ex.Message}");
		}
	}
}
