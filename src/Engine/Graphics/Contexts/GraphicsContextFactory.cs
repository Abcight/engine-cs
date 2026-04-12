using Engine.Graphics.Backend.OpenGL;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public static class GraphicsContextFactory {
	public static Result<IWindowRenderContext, GraphicsError> CreateWindow(WindowRenderContextOptions options) {
		return OpenGlContextFactory.CreateWindow(options);
	}

	public static Result<IRenderTargetContext, GraphicsError> CreateRenderTarget(
		IRenderContext parentContext,
		RenderTargetContextDescriptor descriptor,
		string? label = null
	) {
		return OpenGlContextFactory.CreateRenderTarget(parentContext, descriptor, label);
	}
}