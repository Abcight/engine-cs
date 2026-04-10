using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public sealed record WindowRenderCallbacks {
	public Func<IWindowRenderContext, Result<Unit, GraphicsError>>? OnLoad { get; init; }
	public Func<IWindowRenderContext, double, Result<Unit, GraphicsError>>? OnUpdate { get; init; }
	public Func<IWindowRenderContext, double, Result<Unit, GraphicsError>>? OnRender { get; init; }
	public Func<IWindowRenderContext, int, int, Result<Unit, GraphicsError>>? OnResize { get; init; }
	public Func<IWindowRenderContext, Result<Unit, GraphicsError>>? OnUnload { get; init; }
}
