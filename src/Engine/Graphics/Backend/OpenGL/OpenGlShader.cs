using System.Threading;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlShader<TBinding> : Shader<TBinding>
	where TBinding : class, IGeneratedShaderBinding {

	private readonly OpenGlGraphicsDevice _device;
	private int _program;
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

	~OpenGlShader() {
		EnqueueProgramForDisposal();
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		int program = _program;
		if (program == 0 || !GL.IsProgram(program)) {
			return GraphicsError.InvalidState("Cannot bind a deleted shader program.");
		}

		GL.UseProgram(program);
		Inner.Upload(_uploader);
		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		EnqueueProgramForDisposal();
		GC.SuppressFinalize(this);
		return Unit.Value;
	}

	private void EnqueueProgramForDisposal() {
		int program = Interlocked.Exchange(ref _program, 0);
		if (program == 0) {
			return;
		}

		GLGC.Enqueue(
			_device.GarbageCollectorBucketId,
			GLGC.DeletionKind.Program,
			program
		);
	}
}
