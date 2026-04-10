using System.Runtime.InteropServices;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.OpenGL;

internal sealed class OpenGlVertexBuffer<TVertex> : VertexBuffer<TVertex>
	where TVertex : unmanaged {

	private readonly OpenGlGraphicsDevice _device;
	private readonly int _handle;
	private readonly BufferUsageHint _usageHint;

	internal OpenGlVertexBuffer(
		OpenGlGraphicsDevice device,
		int handle,
		int vertexCount,
		int strideBytes,
		BufferUsageHint usageHint
	) : base(vertexCount, strideBytes) {
		_device = device;
		_handle = handle;
		_usageHint = usageHint;
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		if (!GL.IsBuffer(_handle)) {
			return GraphicsError.InvalidState("Cannot bind a deleted vertex buffer.");
		}

		GL.BindBuffer(BufferTarget.ArrayBuffer, _handle);
		return Unit.Value;
	}

	protected override Result<GraphicsError> SetDataCore(ReadOnlySpan<TVertex> vertices) {
		if (!GL.IsBuffer(_handle)) {
			return GraphicsError.InvalidState("Cannot update a deleted vertex buffer.");
		}

		GL.BindBuffer(BufferTarget.ArrayBuffer, _handle);
		OpenGlGraphicsDevice.UploadBufferData(BufferTarget.ArrayBuffer, vertices, _usageHint);
		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		if (GL.IsBuffer(_handle)) {
			GL.DeleteBuffer(_handle);
		}

		return Unit.Value;
	}
}
