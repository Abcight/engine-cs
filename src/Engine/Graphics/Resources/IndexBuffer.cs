using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public sealed class IndexBuffer<TIndex> : IDisposable
	where TIndex : unmanaged {
	internal delegate Result<Unit, GraphicsError> SetDataAction(ReadOnlySpan<TIndex> indices);
	private readonly Func<IRenderPassContext, Result<Unit, GraphicsError>> _bindAction;
	private readonly SetDataAction _setDataAction;
	private readonly Func<Result<Unit, GraphicsError>> _disposeAction;
	private bool _disposed;

	internal IndexBuffer(
		int indexCount,
		int elementSizeInBytes,
		IndexElementType elementType,
		Func<IRenderPassContext, Result<Unit, GraphicsError>> bindAction,
		SetDataAction setDataAction,
		Func<Result<Unit, GraphicsError>> disposeAction
	) {
		IndexCount = indexCount;
		ElementSizeInBytes = elementSizeInBytes;
		ElementType = elementType;
		_bindAction = bindAction;
		_setDataAction = setDataAction;
		_disposeAction = disposeAction;
	}

	public int IndexCount { get; private set; }

	public IndexElementType ElementType { get; }

	internal int ElementSizeInBytes { get; }

	public Result<Unit, GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed index buffer.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return _bindAction(context);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding index buffer: {exception.Message}");
		}
	}

	public Result<Unit, GraphicsError> SetData(ReadOnlySpan<TIndex> indices) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed index buffer.");
		}

		try {
			Result<Unit, GraphicsError> result = _setDataAction(indices);
			if (result.IsOk) {
				IndexCount = indices.Length;
			}

			return result;
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while updating index buffer: {exception.Message}");
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
			return GraphicsError.Unexpected($"Unexpected error while disposing index buffer: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}
