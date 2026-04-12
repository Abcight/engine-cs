using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public interface IWindowRenderContext : IRenderContext {
	Result<GraphicsError> Run(WindowRenderCallbacks callbacks);
	void RequestClose();
}