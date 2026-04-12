using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Backend.OpenGL;

public static class OpenGlContextFactory {
	public static Result<IWindowRenderContext, GraphicsError> CreateWindow(WindowRenderContextOptions options) {
		if (string.IsNullOrWhiteSpace(options.Title)) {
			return GraphicsError.InvalidArgument("Window title cannot be null or whitespace.");
		}

		if (options.Width <= 0 || options.Height <= 0) {
			return GraphicsError.InvalidArgument("Window dimensions must be greater than zero.");
		}

		try {
			return new OpenGlWindowRenderContext(options);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Failed to create OpenGL window context: {exception.Message}");
		}
	}

	public static Result<IRenderTargetContext, GraphicsError> CreateRenderTarget(
		IRenderContext parentContext,
		RenderTargetContextDescriptor descriptor,
		string? label = null
	) {
		if (parentContext is null) {
			return GraphicsError.InvalidArgument("Parent context cannot be null.");
		}

		if (descriptor.Width <= 0 || descriptor.Height <= 0) {
			return GraphicsError.InvalidArgument("Render target dimensions must be greater than zero.");
		}

		if (parentContext.Device is not OpenGlGraphicsDevice openGlDevice) {
			return GraphicsError.InvalidContext("Render target creation requires an OpenGL-backed render context.");
		}

		return OpenGlRenderTargetContext.TryCreate(openGlDevice, descriptor, label);
	}
}