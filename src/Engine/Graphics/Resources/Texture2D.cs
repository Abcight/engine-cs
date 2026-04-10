using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public sealed class Texture2D : IDisposable {
	internal delegate Result<Unit, GraphicsError> SetPixelsAction(ReadOnlySpan<byte> pixels);

	private readonly Func<IRenderPassContext, int, Result<Unit, GraphicsError>> _bindAction;
	private readonly SetPixelsAction _setPixelsAction;
	private readonly Func<Result<Unit, GraphicsError>> _disposeAction;
	private bool _disposed;

	internal Texture2D(
		Texture2DDescriptor descriptor,
		Func<IRenderPassContext, int, Result<Unit, GraphicsError>> bindAction,
		SetPixelsAction setPixelsAction,
		Func<Result<Unit, GraphicsError>> disposeAction
	) {
		Descriptor = descriptor;
		_bindAction = bindAction;
		_setPixelsAction = setPixelsAction;
		_disposeAction = disposeAction;
	}

	public Texture2DDescriptor Descriptor { get; }

	public int Width => Descriptor.Width;

	public int Height => Descriptor.Height;

	public TextureFormat Format => Descriptor.Format;

	public Result<Unit, GraphicsError> Bind(IRenderPassContext? context, int textureUnit = 0) {
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
			return _bindAction(context, textureUnit);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding texture: {exception.Message}");
		}
	}

	public Result<Unit, GraphicsError> SetPixels(ReadOnlySpan<byte> pixels) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed texture.");
		}

		try {
			return _setPixelsAction(pixels);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while updating texture: {exception.Message}");
		}
	}

	public void Dispose() {
		_ = DisposeChecked();
	}

	public Result<Unit, GraphicsError> DisposeChecked() {
		if (_disposed) {
			return Unit.Value;
		}

		try {
			Result<Unit, GraphicsError> disposeResult = _disposeAction();
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
