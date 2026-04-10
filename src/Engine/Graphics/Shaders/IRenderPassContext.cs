using System.Numerics;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.VertexInput;

namespace Engine.Graphics.Shaders;

public interface IRenderPassContext : IDisposable {
	IGraphicsDevice Device { get; }

	Result<GraphicsError> BindShader<TBinding>(Shader<TBinding> shader)
		where TBinding : class, IGeneratedShaderBinding;

	Result<GraphicsError> BindVertexBuffer<TVertex>(VertexBuffer<TVertex> buffer)
		where TVertex : unmanaged;

	Result<GraphicsError> BindIndexBuffer<TIndex>(IndexBuffer<TIndex> buffer)
		where TIndex : unmanaged;

	Result<GraphicsError> BindTexture2D(Texture2D texture, int textureUnit = 0);

	Result<GraphicsError> SetVertexLayout(VertexLayoutDescription layout);

	Result<GraphicsError> Clear(
		ClearTargets targets,
		Vector4 color,
		float depth = 1.0f,
		int stencil = 0
	);

	Result<GraphicsError> DrawArrays(
		PrimitiveTopology topology,
		int vertexCount,
		int firstVertex = 0
	);

	Result<GraphicsError> DrawIndexed(
		PrimitiveTopology topology,
		int indexCount,
		int firstIndex = 0,
		int baseVertex = 0
	);
}
