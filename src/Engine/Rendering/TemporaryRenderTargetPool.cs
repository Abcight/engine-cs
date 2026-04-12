using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

internal sealed class TemporaryRenderTargetPool : ITemporaryRenderTargetPool {
	private readonly IRenderContext _ownerContext;
	private readonly Dictionary<RenderTargetPoolKey, Stack<IRenderTargetContext>> _available = new();
	private readonly Dictionary<IRenderTargetContext, RenderTargetPoolKey> _leased = new();
	private bool _disposed;

	public TemporaryRenderTargetPool(IRenderContext ownerContext) {
		_ownerContext = ownerContext;
	}

	public Result<IRenderTargetContext, GraphicsError> Rent(
		RenderTargetContextDescriptor descriptor,
		string? label = null
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot rent a render target from a disposed pool.");
		}

		if (descriptor.Width <= 0 || descriptor.Height <= 0) {
			return GraphicsError.InvalidArgument("Render target descriptor dimensions must be greater than zero.");
		}

		var key = new RenderTargetPoolKey(
			descriptor.Width,
			descriptor.Height,
			descriptor.ColorFormat,
			descriptor.HasDepthAttachment
		);

		if (_available.TryGetValue(key, out Stack<IRenderTargetContext>? stack) && stack.Count > 0) {
			IRenderTargetContext context = stack.Pop();
			_leased[context] = key;
			return new Result<IRenderTargetContext, GraphicsError>.Ok(context);
		}

		Result<IRenderTargetContext, GraphicsError> createResult = GraphicsContextFactory.CreateRenderTarget(
			_ownerContext,
			descriptor,
			label
		);
		if (createResult.IsErr) {
			return createResult.Error;
		}

		IRenderTargetContext created = createResult.Value;
		_leased[created] = key;
		return new Result<IRenderTargetContext, GraphicsError>.Ok(created);
	}

	public Result<GraphicsError> Return(IRenderTargetContext context) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot return a render target to a disposed pool.");
		}

		if (context is null) {
			return GraphicsError.InvalidArgument("Render target context cannot be null.");
		}

		if (!_leased.TryGetValue(context, out RenderTargetPoolKey key)) {
			return GraphicsError.InvalidState("Render target context is not leased from this pool.");
		}

		_leased.Remove(context);
		if (!_available.TryGetValue(key, out Stack<IRenderTargetContext>? stack)) {
			stack = new Stack<IRenderTargetContext>();
			_available[key] = stack;
		}

		stack.Push(context);
		return Unit.Value;
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		var disposed = new HashSet<IRenderTargetContext>();
		foreach (KeyValuePair<IRenderTargetContext, RenderTargetPoolKey> pair in _leased) {
			if (disposed.Add(pair.Key)) {
				pair.Key.Dispose();
			}
		}

		foreach ((RenderTargetPoolKey _, Stack<IRenderTargetContext> stack) in _available) {
			while (stack.Count > 0) {
				IRenderTargetContext context = stack.Pop();
				if (disposed.Add(context)) {
					context.Dispose();
				}
			}
		}

		_leased.Clear();
		_available.Clear();
		_disposed = true;
	}

	private readonly record struct RenderTargetPoolKey(
		int Width,
		int Height,
		TextureFormat Format,
		bool HasDepth
	);
}