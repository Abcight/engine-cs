using Engine.Graphics.Shaders;

namespace Engine.Rendering;

public interface IRenderFeature {
	Result<GraphicsError> BeforeScene(RenderFeatureContext context);
	Result<GraphicsError> AfterScene(RenderFeatureContext context);
}

public abstract class RenderFeature : IRenderFeature {
	public virtual Result<GraphicsError> BeforeScene(RenderFeatureContext context) {
		return Unit.Value;
	}

	public virtual Result<GraphicsError> AfterScene(RenderFeatureContext context) {
		return Unit.Value;
	}
}