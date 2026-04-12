using Engine;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Assets;

public sealed class AssetHandle<T> : IDisposable
	where T : class, IDisposable {

	private readonly Assets? _owner;
	private readonly bool _ownsLease;
	private bool _disposed;

	internal AssetHandle(Assets owner, AssetKey key) {
		_owner = owner;
		Key = key;
		_ownsLease = true;
	}

	private AssetHandle() {
		_owner = null;
		Key = AssetKey.Unbound;
		_ownsLease = false;
	}

	public static AssetHandle<T> Unbound => new();

	public AssetKey Key { get; }

	public bool IsDisposed => _disposed;

	public Result<T, GraphicsError> TryGet() {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot resolve an asset from a disposed handle.");
		}

		if (_owner is null) {
			return GraphicsError.InvalidState("Asset handle is not associated with an assets registry.");
		}

		return _owner.TryResolveAsset<T>(Key);
	}

	public T Get() {
		Result<T, GraphicsError> resolveResult = TryGet();
		if (resolveResult.TryOk() is { Value: var value }) {
			return value;
		}

		if (_owner is null) {
			if (typeof(T) == typeof(Texture2D)) {
				return (T)(object)MissingTexture2D.Instance;
			}

			throw new InvalidOperationException(
				$"No fallback asset is available for unbound handle type '{typeof(T).Name}'."
			);
		}

		if (resolveResult.TryErr() is { Error: var error }) {
			return _owner.ResolveFallbackAsset<T>(Key, error);
		}

		throw new InvalidOperationException("Asset handle resolve returned an invalid result state.");
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_disposed = true;
		if (_ownsLease && _owner is not null) {
			_owner.ReleaseLease(Key);
		}
	}
}
