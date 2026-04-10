using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public sealed class VertexBuffer<TVertex> : IDisposable
	where TVertex : unmanaged {

	internal delegate Result<Unit, GraphicsError> SetDataAction(ReadOnlySpan<TVertex> vertices);

	private readonly Func<IRenderPassContext, Result<Unit, GraphicsError>> _bindAction;
	private readonly SetDataAction _setDataAction;
	private readonly Func<Result<Unit, GraphicsError>> _disposeAction;
	private bool _disposed;

	internal VertexBuffer(
		int vertexCount,
		int strideBytes,
		Func<IRenderPassContext, Result<Unit, GraphicsError>> bindAction,
		SetDataAction setDataAction,
		Func<Result<Unit, GraphicsError>> disposeAction
	) {
		VertexCount = vertexCount;
		StrideBytes = strideBytes;
		_bindAction = bindAction;
		_setDataAction = setDataAction;
		_disposeAction = disposeAction;
	}

	public int VertexCount { get; private set; }

	public int StrideBytes { get; }

	public Result<Unit, GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed vertex buffer.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return _bindAction(context);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding vertex buffer: {exception.Message}");
		}
	}

	public Result<Unit, GraphicsError> SetData(ReadOnlySpan<TVertex> vertices) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed vertex buffer.");
		}

		try {
			Result<Unit, GraphicsError> result = _setDataAction(vertices);
			if (result.IsOk) {
				VertexCount = vertices.Length;
			}

			return result;
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while updating vertex buffer: {exception.Message}");
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
			return GraphicsError.Unexpected($"Unexpected error while disposing vertex buffer: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}
