namespace Engine.Graphics.Shaders;

public interface IGraphicsDevice : IDisposable {
	Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null);

	Result<ShaderLoadSuccess<TBinding>, ShaderLoadReport> LoadShader<TBinding>()
		where TBinding : class, IGeneratedShaderBinding, new();

	Result<TBackend, GraphicsError> GetBackend<TBackend>()
		where TBackend : class;
}
