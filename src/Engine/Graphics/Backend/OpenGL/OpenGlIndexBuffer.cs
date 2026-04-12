using System.Threading;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlIndexBuffer<TIndex> : IndexBuffer<TIndex>
	where TIndex : unmanaged {

	private readonly OpenGlGraphicsDevice _device;
	private int _handle;
	private readonly BufferUsageHint _usageHint;
	private readonly int _estimatedByteCount;

	internal OpenGlIndexBuffer(
		OpenGlGraphicsDevice device,
		int handle,
		int indexCount,
		int elementSizeInBytes,
		IndexElementType elementType,
		BufferUsageHint usageHint,
		int estimatedByteCount
	) : base(indexCount, elementSizeInBytes, elementType) {
		_device = device;
		_handle = handle;
		_usageHint = usageHint;
		_estimatedByteCount = estimatedByteCount;
	}

	~OpenGlIndexBuffer() {
		EnqueueBufferForDisposal();
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		int bufferHandle = _handle;
		if (bufferHandle == 0 || !GL.IsBuffer(bufferHandle)) {
			return GraphicsError.InvalidState("Cannot bind a deleted index buffer.");
		}

		GL.BindBuffer(BufferTarget.ElementArrayBuffer, bufferHandle);
		return Unit.Value;
	}

	protected override Result<GraphicsError> SetDataCore(ReadOnlySpan<TIndex> indices) {
		int bufferHandle = _handle;
		if (bufferHandle == 0 || !GL.IsBuffer(bufferHandle)) {
			return GraphicsError.InvalidState("Cannot update a deleted index buffer.");
		}

		GL.BindBuffer(BufferTarget.ElementArrayBuffer, bufferHandle);
		OpenGlGraphicsDevice.UploadBufferData(BufferTarget.ElementArrayBuffer, indices, _usageHint);
		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		EnqueueBufferForDisposal();
		GC.SuppressFinalize(this);
		return Unit.Value;
	}

	private void EnqueueBufferForDisposal() {
		int bufferHandle = Interlocked.Exchange(ref _handle, 0);
		if (bufferHandle == 0) {
			return;
		}

		GLGC.Enqueue(
			_device.GarbageCollectorBucketId,
			GLGC.DeletionKind.Buffer,
			bufferHandle,
			_estimatedByteCount
		);
	}
}
