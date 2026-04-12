using Engine.Graphics.Rendering;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Contexts;

public interface IRenderContext : IDisposable {
	IGraphicsDevice Device { get; }
	int Width { get; }
	int Height { get; }
	Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null);
	Result<GraphicsError> Present();

	RenderPass BeginPass(string? label = null) {
		Result<IRenderPassContext, GraphicsError> beginResult = BeginRenderPass(label);
		if (beginResult.TryErr() is { Error: var error }) {
			return RenderPass.FromError(error);
		}

		if (beginResult.TryOk() is { Value: var passContext }) {
			return RenderPass.FromContext(passContext);
		}

		return RenderPass.FromError(
			GraphicsError.Unexpected("Render pass begin returned an invalid result state.")
		);
	}

	Result<Shader<TBinding>, GraphicsError> LoadShader<TBinding>(Action<string>? onWarning = null)
		where TBinding : class, IGeneratedShaderBinding, new() {
		Result<ShaderLoadSuccess<TBinding>, ShaderLoadReport> loadResult = Device.LoadShader<TBinding>();
		if (loadResult.TryErr() is { Error: var report }) {
			return GraphicsError.BackendFailure(report.Error ?? "Shader load failed.");
		}

		if (loadResult.TryOk() is { Value: var success }) {
			if (onWarning is not null) {
				foreach (string warning in success.Warnings) {
					onWarning(warning);
				}
			}

			return success.Shader;
		}

		return GraphicsError.Unexpected("Shader load returned an invalid result state.");
	}
}