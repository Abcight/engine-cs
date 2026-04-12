using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace ExampleGame.Core;

internal interface IExample {
	string Id { get; }
	string DisplayName { get; }

	Result<GraphicsError> OnLoad(IWindowRenderContext context);
	Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds);
	Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds);
	Result<GraphicsError> OnResize(IWindowRenderContext context, int width, int height);
	Result<GraphicsError> OnUnload(IWindowRenderContext context);
}