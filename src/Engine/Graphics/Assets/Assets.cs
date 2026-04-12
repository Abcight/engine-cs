using System.Collections.Generic;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Assets;

public sealed class Assets : IDisposable {
	private readonly IRenderContext _context;
	private readonly AssetsOptions _options;
	private readonly Dictionary<AssetKey, Texture2D> _textures = new();
	private readonly Dictionary<AssetKey, TextureSource> _textureSources = new();
	private readonly Dictionary<AssetKey, int> _leaseCounts = new();
	private readonly HashSet<AssetKey> _fallbackLoggedKeys = new();

	private Texture2D? _fallbackTexture;
	private bool _disposed;

	public Assets(IRenderContext context, AssetsOptions? options = null) {
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_options = options ?? new AssetsOptions();
	}

	public Result<GraphicsError> SetAllowDisposal(bool allow) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot configure disposal on a disposed assets registry.");
		}

		if (_context.Device is IDeferredDisposalController disposalController) {
			return disposalController.SetAllowDisposal(allow);
		}

		return Unit.Value;
	}

	public AssetHandle<Texture2D> FromTexture2D(
		string path,
		Texture2DLoadOptions? options = null,
		string? label = null
	) {
		Texture2DLoadOptions loadOptions = options ?? new Texture2DLoadOptions();
		Result<AssetHandle<Texture2D>, GraphicsError> loadResult = TryFromTexture2D(path, loadOptions, label);
		if (loadResult.TryOk() is { Value: var handle }) {
			_ = handle.Get();
			return handle;
		}

		AssetKey fallbackKey = BuildTextureKeyUnchecked(path, loadOptions);
		RegisterTextureSource(fallbackKey, path, loadOptions, label);
		AssetHandle<Texture2D> fallbackHandle = AcquireTextureHandle(fallbackKey);
		_ = fallbackHandle.Get();
		return fallbackHandle;
	}

	public Result<AssetHandle<Texture2D>, GraphicsError> TryFromTexture2D(
		string path,
		Texture2DLoadOptions? options = null,
		string? label = null
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot load textures through a disposed assets registry.");
		}

		if (string.IsNullOrWhiteSpace(path)) {
			return GraphicsError.InvalidArgument("Texture path cannot be null or empty.");
		}

		Texture2DLoadOptions loadOptions = options ?? new Texture2DLoadOptions();
		AssetKey key;
		try {
			key = BuildTextureKey(path, loadOptions);
		} catch (Exception exception) {
			return GraphicsError.InvalidArgument(
				$"Texture path '{path}' could not be normalized: {exception.Message}"
			);
		}
		RegisterTextureSource(key, path, loadOptions, label);

		if (!_textures.ContainsKey(key)) {
			Result<GraphicsError> loadTextureResult = LoadTextureFromRegisteredSource(key);
			if (loadTextureResult.TryErr() is { Error: var loadError }) {
				return loadError;
			}
		}

		AssetHandle<Texture2D> handle = AcquireTextureHandle(key);
		return handle;
	}

	public Result<GraphicsError> AddTexture2D(AssetKey key, Texture2D texture) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add textures to a disposed assets registry.");
		}

		if (!key.IsTexture2D) {
			return GraphicsError.InvalidArgument("Asset key kind must be 'Texture2D'.");
		}

		if (texture is null) {
			return GraphicsError.InvalidArgument("Texture cannot be null.");
		}

		_textures[key] = texture;
		_fallbackLoggedKeys.Remove(key);
		return Unit.Value;
	}

	public Result<AssetHandle<Texture2D>, GraphicsError> TryGetTexture2D(AssetKey key) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot resolve textures from a disposed assets registry.");
		}

		if (!key.IsTexture2D) {
			return GraphicsError.InvalidArgument("Asset key kind must be 'Texture2D'.");
		}

		if (!_textures.ContainsKey(key) && !_textureSources.ContainsKey(key)) {
			return GraphicsError.InvalidState(
				$"Texture key '{key.NormalizedPath}' with options hash '{key.OptionsHash}' is not registered."
			);
		}

		return AcquireTextureHandle(key);
	}

	public AssetHandle<Texture2D> GetTexture2D(AssetKey key) {
		if (_disposed || !key.IsTexture2D) {
			return AssetHandle<Texture2D>.Unbound;
		}

		return AcquireTextureHandle(key);
	}

	internal Result<T, GraphicsError> TryResolveAsset<T>(AssetKey key)
		where T : class, IDisposable {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot resolve assets from a disposed assets registry.");
		}

		if (typeof(T) == typeof(Texture2D)) {
			Result<Texture2D, GraphicsError> textureResult = TryResolveTexture(key);
			if (textureResult.TryErr() is { Error: var textureError }) {
				return textureError;
			}

			if (textureResult.TryOk() is { Value: var texture }) {
				return (T)(object)texture;
			}

			return GraphicsError.Unexpected("Texture resolve returned an invalid result state.");
		}

		return GraphicsError.Unsupported(
			$"Assets registry does not yet support resolving assets of type '{typeof(T).Name}'."
		);
	}

	internal T ResolveFallbackAsset<T>(AssetKey key, GraphicsError error)
		where T : class, IDisposable {
		if (typeof(T) == typeof(Texture2D)) {
			Texture2D fallbackTexture = GetTextureFallback(key, error);
			return (T)(object)fallbackTexture;
		}

		throw new InvalidOperationException(
			$"No fallback implementation exists for asset type '{typeof(T).Name}'."
		);
	}

	internal void ReleaseLease(AssetKey key) {
		if (!_leaseCounts.TryGetValue(key, out int count)) {
			return;
		}

		if (count <= 1) {
			_leaseCounts.Remove(key);
			return;
		}

		_leaseCounts[key] = count - 1;
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		var visited = new HashSet<Texture2D>(ReferenceEqualityComparer.Instance);
		foreach ((AssetKey _, Texture2D texture) in _textures) {
			if (visited.Add(texture)) {
				_ = texture.DisposeChecked();
			}
		}

		if (_fallbackTexture is { } fallbackTexture && visited.Add(fallbackTexture)) {
			_ = fallbackTexture.DisposeChecked();
		}

		_textures.Clear();
		_textureSources.Clear();
		_leaseCounts.Clear();
		_fallbackLoggedKeys.Clear();
		_fallbackTexture = null;
		_disposed = true;
	}

	private Result<Texture2D, GraphicsError> TryResolveTexture(AssetKey key) {
		if (!key.IsTexture2D) {
			return GraphicsError.InvalidArgument("Asset key kind must be 'Texture2D'.");
		}

		if (_textures.TryGetValue(key, out Texture2D? hotTexture)) {
			return hotTexture;
		}

		if (!_textureSources.ContainsKey(key)) {
			return GraphicsError.InvalidState(
				$"Texture key '{key.NormalizedPath}' with options hash '{key.OptionsHash}' is not registered."
			);
		}

		Result<GraphicsError> loadResult = LoadTextureFromRegisteredSource(key);
		if (loadResult.TryErr() is { Error: var loadError }) {
			return loadError;
		}

		if (_textures.TryGetValue(key, out Texture2D? coldTexture)) {
			return coldTexture;
		}

		return GraphicsError.Unexpected("Texture load succeeded but cache entry was not stored.");
	}

	private Result<GraphicsError> LoadTextureFromRegisteredSource(AssetKey key) {
		if (!_textureSources.TryGetValue(key, out TextureSource source)) {
			return GraphicsError.InvalidState(
				$"Texture key '{key.NormalizedPath}' with options hash '{key.OptionsHash}' is not registered."
			);
		}

		Result<Texture2D, GraphicsError> loadResult = _context.Device.LoadTexture2DFromFile(
			source.Path,
			source.Options,
			source.Label
		);
		if (loadResult.TryErr() is { Error: var loadError }) {
			return loadError;
		}

		if (loadResult.TryOk() is not { Value: var texture }) {
			return GraphicsError.Unexpected("Texture load returned an invalid result state.");
		}

		_textures[key] = texture;
		_fallbackLoggedKeys.Remove(key);
		return Unit.Value;
	}

	private AssetHandle<Texture2D> AcquireTextureHandle(AssetKey key) {
		if (_leaseCounts.TryGetValue(key, out int currentLeases)) {
			_leaseCounts[key] = currentLeases + 1;
		} else {
			_leaseCounts[key] = 1;
		}

		return new AssetHandle<Texture2D>(this, key);
	}

	private Texture2D GetTextureFallback(AssetKey key, GraphicsError error) {
		LogFallbackForKey(key, error);

		if (_fallbackTexture is { } existingFallback) {
			return existingFallback;
		}

		Texture2DDescriptor fallbackDescriptor = new(1, 1, TextureFormat.RGBA8);
		byte[] pixels = [255, 0, 255, 255];
		Result<Texture2D, GraphicsError> createFallbackResult = _context.Device.CreateTexture2D(
			fallbackDescriptor,
			pixels,
			"Assets.Fallback.Magenta"
		);
		if (createFallbackResult.TryOk() is { Value: var fallbackTexture }) {
			_fallbackTexture = fallbackTexture;
			return fallbackTexture;
		}

		if (createFallbackResult.TryErr() is { Error: var fallbackError }) {
			Log($"[assets:fallback] failed to create magenta fallback texture: {fallbackError.Code}: {fallbackError.Message}");
		}

		_fallbackTexture = MissingTexture2D.Instance;
		return _fallbackTexture;
	}

	private void LogFallbackForKey(AssetKey key, GraphicsError error) {
		if (_options.LogFallbacksOncePerAsset) {
			if (!_fallbackLoggedKeys.Add(key)) {
				return;
			}
		}

		Log(
			$"[assets:fallback] key='{key.Kind}:{key.NormalizedPath}:{key.OptionsHash}' "
			+ $"reason={error.Code}: {error.Message}"
		);
	}

	private void RegisterTextureSource(
		AssetKey key,
		string path,
		Texture2DLoadOptions options,
		string? label
	) {
		if (string.IsNullOrWhiteSpace(path)) {
			return;
		}

		string resolvedLabel = string.IsNullOrWhiteSpace(label)
			? Path.GetFileName(path)
			: label;

		_textureSources[key] = new TextureSource(path, options, resolvedLabel);
	}

	private void Log(string message) {
		if (_options.Logger is { } logger) {
			logger(message);
			return;
		}

		Console.Error.WriteLine(message);
	}

	private static AssetKey BuildTextureKey(string path, Texture2DLoadOptions options) {
		string normalizedPath = NormalizePath(path);
		ulong optionsHash = ComputeTextureOptionsHash(options);
		return AssetKey.Texture2D(normalizedPath, optionsHash);
	}

	private static AssetKey BuildTextureKeyUnchecked(string path, Texture2DLoadOptions options) {
		if (string.IsNullOrWhiteSpace(path)) {
			return AssetKey.Texture2D("<invalid>", ComputeTextureOptionsHash(options));
		}

		try {
			return BuildTextureKey(path, options);
		} catch (Exception) {
			return AssetKey.Texture2D(path, ComputeTextureOptionsHash(options));
		}
	}

	private static string NormalizePath(string path) {
		if (Path.IsPathRooted(path)) {
			return Path.GetFullPath(path);
		}

		string appBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
		if (File.Exists(appBasePath)) {
			return appBasePath;
		}

		return Path.GetFullPath(path);
	}

	private static ulong ComputeTextureOptionsHash(Texture2DLoadOptions options) {
		const ulong OffsetBasis = 1469598103934665603UL;
		const ulong Prime = 1099511628211UL;

		ulong hash = OffsetBasis;
		hash = HashComponent(hash, options.GenerateMipmaps ? 1UL : 0UL, Prime);
		hash = HashComponent(hash, options.FlipVertically ? 1UL : 0UL, Prime);
		hash = HashComponent(hash, (ulong)(int)options.MinFilter, Prime);
		hash = HashComponent(hash, (ulong)(int)options.MagFilter, Prime);
		hash = HashComponent(hash, (ulong)(int)options.WrapU, Prime);
		hash = HashComponent(hash, (ulong)(int)options.WrapV, Prime);
		return hash;
	}

	private static ulong HashComponent(ulong hash, ulong component, ulong prime) {
		hash ^= component;
		hash *= prime;
		return hash;
	}

	private readonly record struct TextureSource(
		string Path,
		Texture2DLoadOptions Options,
		string? Label
	);
}

internal sealed class MissingTexture2D : Texture2D {
	private static readonly Texture2DDescriptor FallbackDescriptor = new(1, 1, TextureFormat.RGBA8);

	private MissingTexture2D() : base(FallbackDescriptor) {
	}

	public static MissingTexture2D Instance { get; } = new();

	protected override Result<GraphicsError> BindCore(IRenderPassContext context, int textureUnit) {
		return GraphicsError.InvalidState(
			"Fallback texture stub cannot be bound because fallback texture creation failed."
		);
	}

	protected override Result<GraphicsError> SetPixelsCore(ReadOnlySpan<byte> pixels) {
		return GraphicsError.Unsupported("Fallback texture stub does not support pixel updates.");
	}

	protected override Result<GraphicsError> DisposeCore() {
		return Unit.Value;
	}
}
