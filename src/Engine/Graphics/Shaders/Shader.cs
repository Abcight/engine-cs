namespace Engine.Graphics.Shaders;

public abstract class Shader<TBinding> : IDisposable
	where TBinding : class, IGeneratedShaderBinding {

	private bool _disposed;

	protected Shader(TBinding inner) {
		Inner = inner;
	}

	public TBinding Inner { get; }

	protected abstract Result<GraphicsError> BindCore(IRenderPassContext context);
	protected abstract Result<GraphicsError> DisposeCore();

	public Result<GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed shader.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return BindCore(context);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding shader: {exception.Message}");
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
			return GraphicsError.Unexpected($"Unexpected error while disposing shader: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}