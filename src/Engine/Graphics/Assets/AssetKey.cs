namespace Engine.Graphics.Assets;

public readonly record struct AssetKey(string Kind, string NormalizedPath, ulong OptionsHash) {
	public const string Texture2DKind = "Texture2D";

	public static AssetKey Unbound { get; } = new("Unbound", string.Empty, 0UL);

	public bool IsUnbound => string.Equals(Kind, "Unbound", StringComparison.Ordinal);

	public bool IsTexture2D => string.Equals(Kind, Texture2DKind, StringComparison.Ordinal);

	public static AssetKey Texture2D(string normalizedPath, ulong optionsHash) {
		string path = normalizedPath ?? string.Empty;
		return new AssetKey(Texture2DKind, path, optionsHash);
	}
}
