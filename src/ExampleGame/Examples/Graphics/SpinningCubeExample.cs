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

internal sealed class SpinningCubeExample : ExampleBase {
	private Shader<CubeShaderBinding>? _shader;
	private VertexBuffer<PositionColorVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;
	private float _rotationRadians;

	public override string Id => "cube";

	public override string DisplayName => "Spinning Cube";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		var vertices = new[] {
			new PositionColorVertex(-0.75f, -0.75f, -0.75f, 1.0f, 0.2f, 0.2f),
			new PositionColorVertex( 0.75f, -0.75f, -0.75f, 0.2f, 1.0f, 0.2f),
			new PositionColorVertex( 0.75f,  0.75f, -0.75f, 0.2f, 0.6f, 1.0f),
			new PositionColorVertex(-0.75f,  0.75f, -0.75f, 1.0f, 0.9f, 0.2f),
			new PositionColorVertex(-0.75f, -0.75f,  0.75f, 1.0f, 0.2f, 0.9f),
			new PositionColorVertex( 0.75f, -0.75f,  0.75f, 0.2f, 1.0f, 1.0f),
			new PositionColorVertex( 0.75f,  0.75f,  0.75f, 1.0f, 0.4f, 0.8f),
			new PositionColorVertex(-0.75f,  0.75f,  0.75f, 0.9f, 0.9f, 0.9f)
		};

		var indices = new uint[] {
			0, 1, 2, 0, 2, 3,
			4, 6, 5, 4, 7, 6,
			0, 4, 5, 0, 5, 1,
			1, 5, 6, 1, 6, 2,
			2, 6, 7, 2, 7, 3,
			3, 7, 4, 3, 4, 0
		};

		_vertexBuffer = context.Device.CreateVertexBuffer(
			vertices,
			BufferUsage.StaticDraw,
			"SpinningCubeVertices"
		).Expect("Failed to create spinning cube vertex buffer.");

		_indexBuffer = context.Device.CreateIndexBuffer(
			indices,
			BufferUsage.StaticDraw,
			"SpinningCubeIndices"
		).Expect("Failed to create spinning cube index buffer.");

		_shader = context.LoadShader<CubeShaderBinding>(
			static warning => Console.WriteLine($"[shader warning] {warning}")
		).Expect("Failed to load spinning cube shader.");
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

		var model = Matrix4x4.CreateFromYawPitchRoll(
			_rotationRadians * 0.9f,
			_rotationRadians * 0.7f,
			_rotationRadians * 0.35f
		);

		var view = Matrix4x4.CreateLookAt(
			new(1.9f, 1.5f, 2.8f),
			Vector3.Zero,
			Vector3.UnitY
		);

		var projection = Matrix4x4.CreatePerspectiveFieldOfView(
			MathF.PI / 3.0f,
			aspectRatio,
			0.1f,
			100.0f
		);

		_shader.Inner.ModelViewProjection = model * view * projection;

		using RenderPass pass = context.BeginPass("Spinning Cube");
		pass
			.SetDepthTestEnabled(true)
			.Clear(ClearTargets.ColorAndDepth, new Vector4(0.03f, 0.04f, 0.08f, 1.0f))
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