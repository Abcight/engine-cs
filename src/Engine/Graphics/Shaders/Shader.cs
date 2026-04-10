namespace Engine.Graphics.Shaders;

public sealed class Shader<TBinding> : IDisposable
	where TBinding : class, IGeneratedShaderBinding {

	private readonly Func<IRenderPassContext, TBinding, Result<Unit, GraphicsError>> _bindAction;
	private readonly Func<Result<Unit, GraphicsError>> _disposeAction;
	private bool _disposed;

	internal Shader(
		TBinding inner,
		Func<IRenderPassContext, TBinding, Result<Unit, GraphicsError>> bindAction,
		Func<Result<Unit, GraphicsError>> disposeAction
	) {
		Inner = inner;
		_bindAction = bindAction;
		_disposeAction = disposeAction;
	}

	public TBinding Inner { get; }

	public Result<Unit, GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed shader.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return _bindAction(context, Inner);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding shader: {exception.Message}");
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
			return GraphicsError.Unexpected($"Unexpected error while disposing shader: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}
