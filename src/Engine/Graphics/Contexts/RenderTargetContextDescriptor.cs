using Engine.Graphics.Resources;

namespace Engine.Graphics.Contexts;

public readonly record struct RenderTargetContextDescriptor {
	public RenderTargetContextDescriptor(int width, int height) {
		Width = width;
		Height = height;
		ColorFormat = TextureFormat.RGBA8;
		HasDepthAttachment = true;
	}
	public int Width { get; init; }
	public int Height { get; init; }
	public TextureFormat ColorFormat { get; init; }
	public bool HasDepthAttachment { get; init; }
}
