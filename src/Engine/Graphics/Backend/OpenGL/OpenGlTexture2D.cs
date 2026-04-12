using System.Threading;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlTexture2D : Texture2D {

	private readonly OpenGlGraphicsDevice _device;
	private int _handle;
	private readonly OpenGlGraphicsDevice.TextureFormatSpec _formatSpec;
	private readonly int _expectedByteCount;
	private readonly bool _isRenderTarget;

	internal OpenGlTexture2D(
		OpenGlGraphicsDevice device,
		int handle,
		Texture2DDescriptor descriptor,
		OpenGlGraphicsDevice.TextureFormatSpec formatSpec,
		int expectedByteCount,
		bool isRenderTarget = false
	) : base(descriptor) {
		_device = device;
		_handle = handle;
		_formatSpec = formatSpec;
		_expectedByteCount = expectedByteCount;
		_isRenderTarget = isRenderTarget;
	}

	~OpenGlTexture2D() {
		EnqueueTextureForDisposal();
	}

	protected override Result<GraphicsError> BindCore(IRenderPassContext context, int textureUnit) {
		if (!OpenGlGraphicsDevice.TryGetCompatibleContext(context, _device, out _, out GraphicsError contextError)) {
			return contextError;
		}

		int textureHandle = _handle;
		if (textureHandle == 0 || !GL.IsTexture(textureHandle)) {
			return GraphicsError.InvalidState("Cannot bind a deleted texture.");
		}

		GL.ActiveTexture(TextureUnit.Texture0 + textureUnit);
		GL.BindTexture(TextureTarget.Texture2D, textureHandle);
		return Unit.Value;
	}

	protected override Result<GraphicsError> SetPixelsCore(ReadOnlySpan<byte> pixels) {
		if (_isRenderTarget) {
			return GraphicsError.Unsupported("Render target attachment textures cannot be updated via SetPixels.");
		}

		if (pixels.Length != _expectedByteCount) {
			return GraphicsError.InvalidArgument(
				$"Texture update byte count mismatch. Expected {_expectedByteCount} bytes, got {pixels.Length}."
			);
		}

		int textureHandle = _handle;
		if (textureHandle == 0 || !GL.IsTexture(textureHandle)) {
			return GraphicsError.InvalidState("Cannot update a deleted texture.");
		}

		byte[] pixelBytes = pixels.ToArray();
		GL.BindTexture(TextureTarget.Texture2D, textureHandle);
		GL.TexSubImage2D(
			TextureTarget.Texture2D,
			0,
			0,
			0,
			Descriptor.Width,
			Descriptor.Height,
			_formatSpec.PixelFormat,
			_formatSpec.PixelType,
			pixelBytes
		);

		if (Descriptor.GenerateMipmaps) {
			GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
		}

		return Unit.Value;
	}

	protected override Result<GraphicsError> DisposeCore() {
		EnqueueTextureForDisposal();
		GC.SuppressFinalize(this);
		return Unit.Value;
	}

	private void EnqueueTextureForDisposal() {
		int textureHandle = Interlocked.Exchange(ref _handle, 0);
		if (textureHandle == 0) {
			return;
		}

		GLGC.Enqueue(
			_device.GarbageCollectorBucketId,
			GLGC.DeletionKind.Texture,
			textureHandle,
			_expectedByteCount
		);
	}
}
