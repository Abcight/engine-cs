using Engine.Graphics.Resources;

namespace Engine.Graphics.Shaders;

public interface IGraphicsDevice : IDisposable {
	Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null);

	Result<VertexBuffer<TVertex>, GraphicsError> CreateVertexBuffer<TVertex>(
		ReadOnlySpan<TVertex> vertices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TVertex : unmanaged;

	Result<IndexBuffer<TIndex>, GraphicsError> CreateIndexBuffer<TIndex>(
		ReadOnlySpan<TIndex> indices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TIndex : unmanaged;

	Result<Texture2D, GraphicsError> CreateTexture2D(
		Texture2DDescriptor descriptor,
		ReadOnlySpan<byte> pixels,
		string? label = null
	);

	Result<ShaderLoadSuccess<TBinding>, ShaderLoadReport> LoadShader<TBinding>()
		where TBinding : class, IGeneratedShaderBinding, new();

	Result<TBackend, GraphicsError> GetBackend<TBackend>()
		where TBackend : class;
}
