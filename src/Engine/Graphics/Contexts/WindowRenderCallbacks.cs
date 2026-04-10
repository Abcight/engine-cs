using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public sealed record WindowRenderCallbacks {
	public Func<IWindowRenderContext, Result<GraphicsError>>? OnLoad { get; init; }
	public Func<IWindowRenderContext, double, Result<GraphicsError>>? OnUpdate { get; init; }
	public Func<IWindowRenderContext, double, Result<GraphicsError>>? OnRender { get; init; }
	public Func<IWindowRenderContext, int, int, Result<GraphicsError>>? OnResize { get; init; }
	public Func<IWindowRenderContext, Result<GraphicsError>>? OnUnload { get; init; }
}
