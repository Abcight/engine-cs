using Engine.Graphics.Resources;

namespace Engine.Graphics.Assets;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class AssetAttribute : Attribute {
	public AssetAttribute(string path) {
		Path = path;
	}

	public string Path { get; }
	public bool GenerateMipmaps { get; init; } = true;
	public bool FlipVertically { get; init; } = false;
	public TextureMinFilter MinFilter { get; init; } = TextureMinFilter.LinearMipmapLinear;
	public TextureMagFilter MagFilter { get; init; } = TextureMagFilter.Linear;
	public TextureWrap WrapU { get; init; } = TextureWrap.Repeat;
	public TextureWrap WrapV { get; init; } = TextureWrap.Repeat;
	public string? Label { get; init; }
}