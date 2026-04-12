using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

public readonly record struct RenderFeatureContext(
	Renderer Renderer,
	IRenderContext Context,
	IGraphicsDevice Device,
	RenderScene Scene,
	CameraHandle CameraHandle,
	CameraMatrices Camera,
	ITemporaryRenderTargetPool TemporaryRenderTargets
);