using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Graphics.VertexInput;

namespace ExampleGame;

public static class EntryPoint {
	public static void Main() {
		var contextResult = GraphicsContextFactory.CreateWindow(
			new WindowRenderContextOptions(
				title: "ExampleGame",
				width: 1280,
				height: 720
			)
		);

		if (contextResult.IsErr) {
			GraphicsError error = contextResult.Error;
			Console.Error.WriteLine($"[context] {error.Code}: {error.Message}");
			return;
		}

		var context = contextResult.Value;
		using (context) {
			var app = new RainbowTriangleApp();
			var callbacks = new WindowRenderCallbacks {
				OnLoad = app.OnLoad,
				OnRender = app.OnRender,
				OnResize = app.OnResize,
				OnUnload = app.OnUnload
			};

			var runResult = context.Run(callbacks);
			if (runResult.TryErr() is { Error: var runError }) {
				Console.Error.WriteLine($"[run] {runError.Code}: {runError.Message}");
			}
		}
	}
}

internal sealed partial class RainbowTriangleApp {
	private Shader<DemoShaderBinding>? _shader;
	private VertexBuffer<RainbowVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;

	public Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		RainbowVertex[] vertices = [
			new RainbowVertex( 0.0f,  0.65f, 0.0f, 1.0f, 0.0f, 0.0f),
			new RainbowVertex( 0.7f, -0.55f, 0.0f, 0.0f, 1.0f, 0.0f),
			new RainbowVertex(-0.7f, -0.55f, 0.0f, 0.0f, 0.0f, 1.0f)
		];

		var vertexBufferResult = context.Device.CreateVertexBuffer(
			vertices,
			BufferUsage.StaticDraw,
			"RainbowTriangleVertices"
		);

		if (vertexBufferResult.IsErr) {
			return vertexBufferResult.Error;
		}

		var vertexBuffer = vertexBufferResult.Value;

		var indexBufferResult = context.Device.CreateIndexBuffer<uint>(
			[0, 1, 2],
			BufferUsage.StaticDraw,
			"RainbowTriangleIndices"
		);

		if (indexBufferResult.IsErr) {
			return indexBufferResult.Error;
		}

		var indexBuffer = indexBufferResult.Value;

		var shaderLoadResult = context.Device.LoadShader<DemoShaderBinding>();
		if (shaderLoadResult.IsErr) {
			var report = shaderLoadResult.Error;
			return GraphicsError.BackendFailure(report.Error ?? "Shader load failed.");
		}

		var shaderLoad = shaderLoadResult.Value;
		foreach (string warning in shaderLoad.Warnings) {
			Console.WriteLine($"[shader warning] {warning}");
		}

		_vertexBuffer = vertexBuffer;
		_indexBuffer = indexBuffer;
		_shader = shaderLoad.Shader;

		return Unit.Value;
	}

	public Result<GraphicsError> OnRender(IWindowRenderContext context, double _) {
		if (_shader is null || _vertexBuffer is null || _indexBuffer is null) {
			return GraphicsError.InvalidState("Cannot render before graphics resources are created.");
		}

		var passResult = context.BeginRenderPass("Main");
		if (passResult.IsErr) {
			return passResult.Error;
		}

		var pass = passResult.Value;
		using (pass) {
			pass.Clear(ClearTargets.Color, new System.Numerics.Vector4(0.07f, 0.07f, 0.09f, 1.0f));
			pass.BindShader(_shader);
			pass.BindVertexBuffer(_vertexBuffer);
			pass.BindIndexBuffer(_indexBuffer);
			pass.SetVertexLayout(RainbowVertex.Layout);

			pass.DrawIndexed(
				PrimitiveTopology.Triangles,
				_indexBuffer.IndexCount
			);
		}

		return context.Present();
	}

	public Result<GraphicsError> OnResize(IWindowRenderContext _, int __, int ___) {
		return Unit.Value;
	}

	public Result<GraphicsError> OnUnload(IWindowRenderContext _) {
		_indexBuffer?.DisposeChecked();
		_vertexBuffer?.DisposeChecked();
		_shader?.DisposeChecked();

		return Unit.Value;
	}

	[VertexLayout]
	private readonly partial struct RainbowVertex {
		[VertexElement(0)] public readonly Vector3 Position;
		[VertexElement(1)] public readonly Vector3 Color;
		public RainbowVertex(float x, float y, float z, float r, float g, float b) {
			Position = new(x, y, z);
			Color = new(r, g, b);
		}
	}
}
