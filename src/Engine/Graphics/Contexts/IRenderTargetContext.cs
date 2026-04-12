using Engine.Graphics.Resources;

namespace Engine.Graphics.Contexts;

public interface IRenderTargetContext : IRenderContext {
	Texture2D ColorTexture { get; }
}