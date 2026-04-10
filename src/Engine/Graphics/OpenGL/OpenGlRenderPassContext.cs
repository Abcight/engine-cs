using Engine.Graphics.Shaders;

namespace Engine.Graphics.OpenGL;

public sealed class OpenGlRenderPassContext : IRenderPassContext {
	private bool _disposed;

	internal OpenGlRenderPassContext(OpenGlGraphicsDevice device, string? label) {
		Device = device;
		Label = label;
	}

	public IGraphicsDevice Device { get; }

	public string? Label { get; }

	internal OpenGlGraphicsDevice Owner => (OpenGlGraphicsDevice)Device;

	public void Dispose() {
		if (_disposed) {
			return;
		}

		if (!string.IsNullOrWhiteSpace(Label)) {
			Owner.PopDebugGroup();
		}

		_disposed = true;
	}
}
