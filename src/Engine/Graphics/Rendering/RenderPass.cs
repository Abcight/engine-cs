using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Graphics.VertexInput;

namespace Engine.Graphics.Rendering;

public sealed class RenderPass : IDisposable {
	private readonly IRenderPassContext? _context;
	private GraphicsError? _fault;
	private bool _disposed;
	private bool _ended;

	private RenderPass(IRenderPassContext? context, GraphicsError? fault) {
		_context = context;
		_fault = fault is { IsNone: false } ? fault : null;
	}

	public bool IsFaulted => _fault is not null;

	public GraphicsError? Fault => _fault;

	public static RenderPass FromContext(IRenderPassContext context) {
		if (context is null) {
			return FromError(GraphicsError.InvalidArgument("Render pass context cannot be null."));
		}

		return new RenderPass(context, null);
	}

	public static RenderPass FromError(GraphicsError error) {
		return new RenderPass(null, error);
	}

	public RenderPass BindShader<TBinding>(Shader<TBinding> shader)
		where TBinding : class, IGeneratedShaderBinding {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.BindShader(shader));
		}

		return this;
	}

	public RenderPass BindVertexBuffer<TVertex>(VertexBuffer<TVertex> buffer)
		where TVertex : unmanaged {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.BindVertexBuffer(buffer));
		}

		return this;
	}

	public RenderPass BindIndexBuffer<TIndex>(IndexBuffer<TIndex> buffer)
		where TIndex : unmanaged {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.BindIndexBuffer(buffer));
		}

		return this;
	}

	public RenderPass BindTexture2D(Texture2D texture, int textureUnit = 0) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.BindTexture2D(texture, textureUnit));
		}

		return this;
	}

	public RenderPass SetVertexLayout(VertexLayoutDescription layout) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.SetVertexLayout(layout));
		}

		return this;
	}

	public RenderPass SetDepthTestEnabled(bool enabled) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.SetDepthTestEnabled(enabled));
		}

		return this;
	}

	public RenderPass SetDepthWriteEnabled(bool enabled) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.SetDepthWriteEnabled(enabled));
		}

		return this;
	}

	public RenderPass SetCullMode(CullMode mode) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.SetCullMode(mode));
		}

		return this;
	}

	public RenderPass Clear(
		ClearTargets targets,
		Vector4 color,
		float depth = 1.0f,
		int stencil = 0
	) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.Clear(targets, color, depth, stencil));
		}

		return this;
	}

	public RenderPass DrawArrays(
		PrimitiveTopology topology,
		int vertexCount,
		int firstVertex = 0
	) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.DrawArrays(topology, vertexCount, firstVertex));
		}

		return this;
	}

	public RenderPass DrawIndexed(
		PrimitiveTopology topology,
		int indexCount,
		int firstIndex = 0,
		int baseVertex = 0
	) {
		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(context.DrawIndexed(topology, indexCount, firstIndex, baseVertex));
		}

		return this;
	}

	public RenderPass Run(Func<IRenderPassContext, Result<GraphicsError>> operation) {
		if (operation is null) {
			Capture(GraphicsError.InvalidArgument("Render pass operation cannot be null."));
			return this;
		}

		if (TryGetContext(out IRenderPassContext? context)) {
			Capture(operation(context));
		}

		return this;
	}

	public Result<GraphicsError> End() {
		if (_ended) {
			return _fault is { } error ? error : Unit.Value;
		}

		_ended = true;
		Dispose();
		return _fault is { } fault ? fault : Unit.Value;
	}

	public Result<GraphicsError> EndAndPresent(IRenderContext context) {
		if (context is null) {
			return GraphicsError.InvalidArgument("Render context cannot be null.");
		}

		Result<GraphicsError> endResult = End();
		if (endResult.IsErr) {
			return endResult;
		}

		return context.Present();
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_context?.Dispose();
		_disposed = true;
	}

	private bool TryGetContext([NotNullWhen(true)] out IRenderPassContext? context) {
		context = _context;
		if (_disposed || _ended) {
			if (_fault is null) {
				_fault = GraphicsError.InvalidState("Render pass has already ended.");
			}

			return false;
		}

		if (_fault is not null) {
			return false;
		}

		if (context is null) {
			if (_fault is null) {
				_fault = GraphicsError.InvalidState("Render pass failed to begin.");
			}

			return false;
		}

		return true;
	}

	private void Capture(Result<GraphicsError> result) {
		if (_fault is null && result.TryErr() is { Error: var error }) {
			_fault = error;
		}
	}
}
