using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace ExampleGame;

internal abstract class ExampleBase : IExample {
	public abstract string Id { get; }
	public abstract string DisplayName { get; }

	public abstract Result<GraphicsError> OnLoad(IWindowRenderContext context);

	public virtual Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		return Unit.Value;
	}

	public abstract Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds);

	public virtual Result<GraphicsError> OnResize(IWindowRenderContext context, int width, int height) {
		return Unit.Value;
	}

	public virtual Result<GraphicsError> OnUnload(IWindowRenderContext context) {
		return Unit.Value;
	}

	protected static Result<Shader<TBinding>, GraphicsError> LoadShader<TBinding>(IWindowRenderContext context)
		where TBinding : class, IGeneratedShaderBinding, new() {
		var shaderLoadResult = context.Device.LoadShader<TBinding>();
		if (shaderLoadResult.IsErr) {
			ShaderLoadReport report = shaderLoadResult.Error;
			return GraphicsError.BackendFailure(report.Error ?? "Shader load failed.");
		}

		ShaderLoadSuccess<TBinding> shaderLoad = shaderLoadResult.Value;
		foreach (string warning in shaderLoad.Warnings) {
			Console.WriteLine($"[shader warning] {warning}");
		}

		return shaderLoad.Shader;
	}
}
