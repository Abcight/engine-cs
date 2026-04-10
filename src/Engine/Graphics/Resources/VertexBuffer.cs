using Engine.Graphics.Shaders;

namespace Engine.Graphics.Resources;

public abstract class VertexBuffer<TVertex> : IDisposable
	where TVertex : unmanaged {

	private bool _disposed;

	protected VertexBuffer(int vertexCount, int strideBytes) {
		VertexCount = vertexCount;
		StrideBytes = strideBytes;
	}

	public int VertexCount { get; protected set; }

	public int StrideBytes { get; }

	protected abstract Result<Unit, GraphicsError> BindCore(IRenderPassContext context);
	protected abstract Result<Unit, GraphicsError> SetDataCore(ReadOnlySpan<TVertex> vertices);
	protected abstract Result<Unit, GraphicsError> DisposeCore();

	public Result<Unit, GraphicsError> Bind(IRenderPassContext? context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind a disposed vertex buffer.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render pass context cannot be null.");
		}

		try {
			return BindCore(context);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Unexpected error while binding vertex buffer: {exception.Message}");
		}
	}

	public Result<Unit, GraphicsError> SetData(ReadOnlySpan<TVertex> vertices) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot update a disposed vertex buffer.");
		}

		try {
			Result<Unit, GraphicsError> result = SetDataCore(vertices);
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
			Result<Unit, GraphicsError> disposeResult = DisposeCore();
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
