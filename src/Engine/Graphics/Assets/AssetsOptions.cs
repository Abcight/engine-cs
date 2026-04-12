namespace Engine.Graphics.Assets;

public sealed record AssetsOptions(
	Action<string>? Logger = null,
	bool LogFallbacksOncePerAsset = true
);
