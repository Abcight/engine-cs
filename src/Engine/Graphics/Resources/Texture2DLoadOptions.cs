namespace Engine.Graphics.Resources;

public readonly record struct Texture2DLoadOptions(
	bool GenerateMipmaps = true,
	bool FlipVertically = false,
	TextureMinFilter MinFilter = TextureMinFilter.LinearMipmapLinear,
	TextureMagFilter MagFilter = TextureMagFilter.Linear,
	TextureWrap WrapU = TextureWrap.Repeat,
	TextureWrap WrapV = TextureWrap.Repeat
);