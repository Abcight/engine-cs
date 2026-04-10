using System.Numerics;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.VertexInput;

namespace Engine.Graphics.Shaders;

public interface IRenderPassContext : IDisposable {
	IGraphicsDevice Device { get; }

	Result<Unit, GraphicsError> BindShader<TBinding>(Shader<TBinding> shader)
		where TBinding : class, IGeneratedShaderBinding;

	Result<Unit, GraphicsError> BindVertexBuffer<TVertex>(VertexBuffer<TVertex> buffer)
		where TVertex : unmanaged;

	Result<Unit, GraphicsError> BindIndexBuffer<TIndex>(IndexBuffer<TIndex> buffer)
		where TIndex : unmanaged;

	Result<Unit, GraphicsError> BindTexture2D(Texture2D texture, int textureUnit = 0);

	Result<Unit, GraphicsError> SetVertexLayout(VertexLayoutDescription layout);

	Result<Unit, GraphicsError> Clear(
		ClearTargets targets,
		Vector4 color,
		float depth = 1.0f,
		int stencil = 0
	);

	Result<Unit, GraphicsError> DrawArrays(
		PrimitiveTopology topology,
		int vertexCount,
		int firstVertex = 0
	);

	Result<Unit, GraphicsError> DrawIndexed(
		PrimitiveTopology topology,
		int indexCount,
		int firstIndex = 0,
		int baseVertex = 0
	);
}
