namespace Engine.Graphics.Shaders;

public interface IRenderPassContext : IDisposable {
	IGraphicsDevice Device { get; }
}
