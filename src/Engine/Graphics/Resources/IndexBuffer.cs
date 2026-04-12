using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public abstract class IndexBuffer<TIndex> : IDisposable
	where TIndex : unmanaged {

	private bool _disposed;

	protected IndexBuffer(int indexCount, int elementSizeInBytes, IndexElementType elementType) {
		IndexCount = indexCount;
		ElementSizeInBytes = elementSizeInBytes;
		ElementType = elementType;
	}

	public int IndexCount { get; protected set; }

	public IndexElementType ElementType { get; }

	internal int ElementSizeInBytes { get; }

	protected abstract Result<GraphicsError> BindCore(IRenderPassContext context);
	protected abstract Result<GraphicsError> SetDataCore(ReadOnlySpan<TIndex> indices);
	protected abstract Result<GraphicsError> DisposeCore();

	public Result<GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed index buffer.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return BindCore(context);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding index buffer: {exception.Message}");
		}
	}

	public Result<GraphicsError> SetData(ReadOnlySpan<TIndex> indices) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed index buffer.");
		}

		try {
			Result<GraphicsError> result = SetDataCore(indices);
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
			return GraphicsError.Unexpected($"Unexpected error while disposing index buffer: {exception.Message}");
		}

		_disposed = true;
		return Unit.Value;
	}
}