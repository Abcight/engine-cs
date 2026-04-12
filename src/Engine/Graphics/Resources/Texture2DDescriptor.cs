namespace Engine.Graphics.Resources;

public enum TextureFormat {
	R8,
	RG8,
	RGB8,
	RGBA8
}

public enum TextureMinFilter {
	Nearest,
	Linear,
	NearestMipmapNearest,
	LinearMipmapLinear
}

public enum TextureMagFilter {
	Nearest,
	Linear
}

public enum TextureWrap {
	Repeat,
	ClampToEdge,
	MirroredRepeat
}

public readonly record struct Texture2DDescriptor {
	public Texture2DDescriptor(
		int width,
		int height,
		TextureFormat format
	) {
		Width = width;
		Height = height;
		Format = format;
		MinFilter = TextureMinFilter.Linear;
		MagFilter = TextureMagFilter.Linear;
		WrapU = TextureWrap.Repeat;
		WrapV = TextureWrap.Repeat;
		GenerateMipmaps = false;
	}
	public int Width { get; init; }
	public int Height { get; init; }
	public TextureFormat Format { get; init; }
	public TextureMinFilter MinFilter { get; init; }
	public TextureMagFilter MagFilter { get; init; }
	public TextureWrap WrapU { get; init; }
	public TextureWrap WrapV { get; init; }
	public bool GenerateMipmaps { get; init; }
}