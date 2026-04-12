using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

public interface ITemporaryRenderTargetPool : IDisposable {
	Result<IRenderTargetContext, GraphicsError> Rent(
		RenderTargetContextDescriptor descriptor,
		string? label = null
	);

	Result<GraphicsError> Return(IRenderTargetContext context);
}