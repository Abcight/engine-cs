using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace ExampleGame;

internal sealed class RainbowTriangleExample : ExampleBase {
	private Shader<TriangleShaderBinding>? _shader;
	private VertexBuffer<PositionColorVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;

	public override string Id => "triangle";

	public override string DisplayName => "Rainbow Triangle";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		PositionColorVertex[] vertices = [
			new PositionColorVertex( 0.0f,  0.65f, 0.0f, 1.0f, 0.0f, 0.0f),
			new PositionColorVertex( 0.7f, -0.55f, 0.0f, 0.0f, 1.0f, 0.0f),
			new PositionColorVertex(-0.7f, -0.55f, 0.0f, 0.0f, 0.0f, 1.0f)
		];

		var vertexBufferResult = context.Device.CreateVertexBuffer(
			vertices,
			BufferUsage.StaticDraw,
			"RainbowTriangleVertices"
		);
		if (vertexBufferResult.IsErr) {
			return vertexBufferResult.Error;
		}

		var indexBufferResult = context.Device.CreateIndexBuffer<uint>(
			[0, 1, 2],
			BufferUsage.StaticDraw,
			"RainbowTriangleIndices"
		);
		if (indexBufferResult.IsErr) {
			return indexBufferResult.Error;
		}

		var shaderResult = LoadShader<TriangleShaderBinding>(context);
		if (shaderResult.IsErr) {
			return shaderResult.Error;
		}

		_vertexBuffer = vertexBufferResult.Value;
		_indexBuffer = indexBufferResult.Value;
		_shader = shaderResult.Value;
		return Unit.Value;
	}

	public override Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_shader is null || _vertexBuffer is null || _indexBuffer is null) {
			return GraphicsError.InvalidState("Cannot render the rainbow triangle before resources are created.");
		}

		var passResult = context.BeginRenderPass("Rainbow Triangle");
		if (passResult.IsErr) {
			return passResult.Error;
		}

		var pass = passResult.Value;
		using (pass) {
			pass.SetDepthTestEnabled(false);
			pass.Clear(ClearTargets.Color, new Vector4(0.07f, 0.07f, 0.09f, 1.0f));
			pass.BindShader(_shader);
			pass.BindVertexBuffer(_vertexBuffer);
			pass.BindIndexBuffer(_indexBuffer);
			pass.SetVertexLayout(PositionColorVertex.Layout);
			pass.DrawIndexed(PrimitiveTopology.Triangles, _indexBuffer.IndexCount);
		}

		return context.Present();
	}

	public override Result<GraphicsError> OnUnload(IWindowRenderContext context) {
		_indexBuffer?.DisposeChecked();
		_vertexBuffer?.DisposeChecked();
		_shader?.DisposeChecked();
		return Unit.Value;
	}
}
