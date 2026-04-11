using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlShader<TBinding> : Shader<TBinding>
	where TBinding : class, IGeneratedShaderBinding {

	private readonly OpenGlGraphicsDevice _device;
	private readonly int _program;
	private readonly IUniformUploader _uploader;

	internal OpenGlShader(
		OpenGlGraphicsDevice device,
		int program,
		IUniformUploader uploader,
		TBinding binding
	) : base(binding) {
		_device = device;
		_program = program;
		_uploader = uploader;
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		GL.UseProgram(_program);
		Inner.Upload(_uploader);
		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		if (GL.IsProgram(_program)) {
			GL.DeleteProgram(_program);
		}

		return Unit.Value;
	}
}
