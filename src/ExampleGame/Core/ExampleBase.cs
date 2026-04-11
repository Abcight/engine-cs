using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace ExampleGame.Core;

internal abstract class ExampleBase : IExample {
	public abstract string Id { get; }
	public abstract string DisplayName { get; }

	public abstract Result<GraphicsError> OnLoad(IWindowRenderContext context);

	public virtual Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		return Unit.Value;
	}

	public abstract Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds);

	public virtual Result<GraphicsError> OnResize(IWindowRenderContext context, int width, int height) {
		return Unit.Value;
	}

	public virtual Result<GraphicsError> OnUnload(IWindowRenderContext context) {
		return Unit.Value;
	}
}
