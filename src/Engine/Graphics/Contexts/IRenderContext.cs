using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public interface IRenderContext : IDisposable {
	IGraphicsDevice Device { get; }
	int Width { get; }
	int Height { get; }
	Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null);
	Result<Unit, GraphicsError> Present();
}
