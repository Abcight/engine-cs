using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public abstract class Texture2D : IDisposable {

	private bool _disposed;

	protected Texture2D(Texture2DDescriptor descriptor) {
		Descriptor = descriptor;
	}

	public Texture2DDescriptor Descriptor { get; }

	public int Width => Descriptor.Width;

	public int Height => Descriptor.Height;

	public TextureFormat Format => Descriptor.Format;

	protected abstract Result<GraphicsError> BindCore(IRenderPassContext context, int textureUnit);
	protected abstract Result<GraphicsError> SetPixelsCore(ReadOnlySpan<byte> pixels);
	protected abstract Result<GraphicsError> DisposeCore();

	public Result<GraphicsError> Bind(IRenderPassContext? context, int textureUnit = 0) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed texture.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		if (textureUnit < 0) {
			return GraphicsError.InvalidArgument("Texture unit cannot be negative.");
		}

		try {
			return BindCore(context, textureUnit);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding texture: {exception.Message}");
		}
	}

	public Result<GraphicsError> SetPixels(ReadOnlySpan<byte> pixels) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed texture.");
		}

		try {
			return SetPixelsCore(pixels);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while updating texture: {exception.Message}");
		}
	}

	public void Dispose() {
		_ = DisposeChecked();
	}

	public Result<GraphicsError> DisposeChecked() {
		if (_disposed) {
			return Unit.Value;
		}

		try {
			Result<GraphicsError> disposeResult = DisposeCore();
			if (disposeResult.IsErr) {
				return disposeResult;
			}
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while disposing texture: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}