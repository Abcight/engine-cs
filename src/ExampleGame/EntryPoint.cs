using System.Runtime.InteropServices;
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

internal sealed class RainbowTriangleApp {
	private Shader<DemoShaderBinding>? _shader;
	private VertexBuffer<RainbowVertex>? _vertexBuffer;
	private IndexBuffer<uint>? _indexBuffer;
	private bool _vertexLayoutConfigured;

	public Result<Unit, GraphicsError> OnLoad(IWindowRenderContext context) {
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
		_vertexLayoutConfigured = false;

		return Unit.Value;
	}

	public Result<Unit, GraphicsError> OnRender(IWindowRenderContext context, double _) {
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

			if (!_vertexLayoutConfigured) {
				pass.SetVertexLayout(VertexLayoutDescription.Create<RainbowVertex>(
					new VertexElementDescription(0, VertexElementType.Float32, 3, 0),
					new VertexElementDescription(1, VertexElementType.Float32, 3, sizeof(float) * 3)
				));

				_vertexLayoutConfigured = true;
			}

			pass.DrawIndexed(
				PrimitiveTopology.Triangles,
				_indexBuffer.IndexCount
			);
		}

		return context.Present();
	}

	public Result<Unit, GraphicsError> OnResize(IWindowRenderContext _, int __, int ___) {
		return Unit.Value;
	}

	public Result<Unit, GraphicsError> OnUnload(IWindowRenderContext _) {
		_indexBuffer?.DisposeChecked();
		_vertexBuffer?.DisposeChecked();
		_shader?.DisposeChecked();

		return Unit.Value;
	}

	[StructLayout(LayoutKind.Sequential)]
	private readonly struct RainbowVertex {
		public readonly float Px;
		public readonly float Py;
		public readonly float Pz;
		public readonly float R;
		public readonly float G;
		public readonly float B;
		public RainbowVertex(float px, float py, float pz, float r, float g, float b) {
			Px = px;
			Py = py;
			Pz = pz;
			R = r;
			G = g;
			B = b;
		}
	}
}
