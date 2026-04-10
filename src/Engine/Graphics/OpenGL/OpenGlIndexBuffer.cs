using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.OpenGL;

internal sealed class OpenGlIndexBuffer<TIndex> : IndexBuffer<TIndex>
	where TIndex : unmanaged {

	private readonly OpenGlGraphicsDevice _device;
	private readonly int _handle;
	private readonly BufferUsageHint _usageHint;

	internal OpenGlIndexBuffer(
		OpenGlGraphicsDevice device,
		int handle,
		int indexCount,
		int elementSizeInBytes,
		IndexElementType elementType,
		BufferUsageHint usageHint
	) : base(indexCount, elementSizeInBytes, elementType) {
		_device = device;
		_handle = handle;
		_usageHint = usageHint;
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		if (!GL.IsBuffer(_handle)) {
			return GraphicsError.InvalidState("Cannot bind a deleted index buffer.");
		}

		GL.BindBuffer(BufferTarget.ElementArrayBuffer, _handle);
		return Unit.Value;
	}

	protected override Result<GraphicsError> SetDataCore(ReadOnlySpan<TIndex> indices) {
		if (!GL.IsBuffer(_handle)) {
			return GraphicsError.InvalidState("Cannot update a deleted index buffer.");
		}

		GL.BindBuffer(BufferTarget.ElementArrayBuffer, _handle);
		OpenGlGraphicsDevice.UploadBufferData(BufferTarget.ElementArrayBuffer, indices, _usageHint);
		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		if (GL.IsBuffer(_handle)) {
			GL.DeleteBuffer(_handle);
		}

		return Unit.Value;
	}
}
