using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using ExampleGame.Core;
using ExampleGame.Shaders;
using ExampleGame.Shared;

namespace ExampleGame.Examples.Graphics;

internal sealed class RainbowTriangleExample : ExampleBase {
	private Shader<TriangleShaderBinding>? _shader;
	private VertexBuffer<PositionColorVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;

	public override string Id => "triangle";

	public override string DisplayName => "Rainbow Triangle";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		var vertices = new[] {
			new PositionColorVertex( 0.0f,  0.65f, 0.0f, 1.0f, 0.0f, 0.0f),
			new PositionColorVertex( 0.7f, -0.55f, 0.0f, 0.0f, 1.0f, 0.0f),
			new PositionColorVertex(-0.7f, -0.55f, 0.0f, 0.0f, 0.0f, 1.0f)
		};

		_vertexBuffer = context.Device.CreateVertexBuffer(
			vertices,
			BufferUsage.StaticDraw,
			"RainbowTriangleVertices"
		).Expect("Failed to create rainbow triangle vertex buffer.");

		_indexBuffer = context.Device.CreateIndexBuffer(
			[0u, 1u, 2u],
			BufferUsage.StaticDraw,
			"RainbowTriangleIndices"
		).Expect("Failed to create rainbow triangle index buffer.");

		_shader = context.LoadShader<TriangleShaderBinding>(
			static warning => Console.WriteLine($"[shader warning] {warning}")
		).Expect("Failed to load rainbow triangle shader.");
		return Unit.Value;
	}

	public override Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_shader is null || _vertexBuffer is null || _indexBuffer is null) {
			return GraphicsError.InvalidState("Cannot render the rainbow triangle before resources are created.");
		}

		using RenderPass pass = context.BeginPass("Rainbow Triangle");
		pass
			.SetDepthTestEnabled(false)
			.Clear(ClearTargets.Color, new Vector4(0.07f, 0.07f, 0.09f, 1.0f))
			.BindShader(_shader)
			.BindVertexBuffer(_vertexBuffer)
			.BindIndexBuffer(_indexBuffer)
			.SetVertexLayout(PositionColorVertex.Layout)
			.DrawIndexed(PrimitiveTopology.Triangles, _indexBuffer.IndexCount);

		return pass.EndAndPresent(context);
	}

	public override Result<GraphicsError> OnUnload(IWindowRenderContext context) {
		_indexBuffer?.DisposeChecked();
		_vertexBuffer?.DisposeChecked();
		_shader?.DisposeChecked();
		return Unit.Value;
	}
}
