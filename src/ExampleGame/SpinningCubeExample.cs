using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace ExampleGame;

internal sealed class SpinningCubeExample : ExampleBase {
	private Shader<CubeShaderBinding>? _shader;
	private VertexBuffer<PositionColorVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;
	private float _rotationRadians;

	public override string Id => "cube";

	public override string DisplayName => "Spinning Cube";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		PositionColorVertex[] vertices = [
			new PositionColorVertex(-0.75f, -0.75f, -0.75f, 1.0f, 0.2f, 0.2f),
			new PositionColorVertex( 0.75f, -0.75f, -0.75f, 0.2f, 1.0f, 0.2f),
			new PositionColorVertex( 0.75f,  0.75f, -0.75f, 0.2f, 0.6f, 1.0f),
			new PositionColorVertex(-0.75f,  0.75f, -0.75f, 1.0f, 0.9f, 0.2f),
			new PositionColorVertex(-0.75f, -0.75f,  0.75f, 1.0f, 0.2f, 0.9f),
			new PositionColorVertex( 0.75f, -0.75f,  0.75f, 0.2f, 1.0f, 1.0f),
			new PositionColorVertex( 0.75f,  0.75f,  0.75f, 1.0f, 0.4f, 0.8f),
			new PositionColorVertex(-0.75f,  0.75f,  0.75f, 0.9f, 0.9f, 0.9f)
		];

		uint[] indices = [
			0, 1, 2, 0, 2, 3,
			4, 6, 5, 4, 7, 6,
			0, 4, 5, 0, 5, 1,
			1, 5, 6, 1, 6, 2,
			2, 6, 7, 2, 7, 3,
			3, 7, 4, 3, 4, 0
		];

		var vertexBufferResult = context.Device.CreateVertexBuffer(
			vertices,
			BufferUsage.StaticDraw,
			"SpinningCubeVertices"
		);
		if (vertexBufferResult.IsErr) {
			return vertexBufferResult.Error;
		}

		var indexBufferResult = context.Device.CreateIndexBuffer(
			indices,
			BufferUsage.StaticDraw,
			"SpinningCubeIndices"
		);
		if (indexBufferResult.IsErr) {
			return indexBufferResult.Error;
		}

		var shaderResult = LoadShader<CubeShaderBinding>(context);
		if (shaderResult.IsErr) {
			return shaderResult.Error;
		}

		_vertexBuffer = vertexBufferResult.Value;
		_indexBuffer = indexBufferResult.Value;
		_shader = shaderResult.Value;
		_rotationRadians = 0.0f;
		return Unit.Value;
	}

	public override Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		_rotationRadians += (float)deltaTimeSeconds;
		return Unit.Value;
	}

	public override Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_shader is null || _vertexBuffer is null || _indexBuffer is null) {
			return GraphicsError.InvalidState("Cannot render the spinning cube before resources are created.");
		}

		float aspectRatio = context.Height > 0
			? context.Width / (float)context.Height
			: 1.0f;

		Matrix4x4 model = Matrix4x4.CreateFromYawPitchRoll(
			_rotationRadians * 0.9f,
			_rotationRadians * 0.7f,
			_rotationRadians * 0.35f
		);

		Matrix4x4 view = Matrix4x4.CreateLookAt(
			new Vector3(1.9f, 1.5f, 2.8f),
			Vector3.Zero,
			Vector3.UnitY
		);

		Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
			MathF.PI / 3.0f,
			aspectRatio,
			0.1f,
			100.0f
		);

		_shader.Inner.ModelViewProjection = model * view * projection;

		var passResult = context.BeginRenderPass("Spinning Cube");
		if (passResult.IsErr) {
			return passResult.Error;
		}

		var pass = passResult.Value;
		using (pass) {
			pass.SetDepthTestEnabled(true);
			pass.Clear(ClearTargets.ColorAndDepth, new Vector4(0.03f, 0.04f, 0.08f, 1.0f));
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
