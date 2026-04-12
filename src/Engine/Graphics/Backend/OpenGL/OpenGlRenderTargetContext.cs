using System.Threading;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlRenderTargetContext : IRenderTargetContext {
	private readonly OpenGlGraphicsDevice _device;
	private int _framebufferHandle;
	private int _depthRenderbufferHandle;
	private bool _disposed;

	private OpenGlRenderTargetContext(
		OpenGlGraphicsDevice device,
		int width,
		int height,
		int framebufferHandle,
		Texture2D colorTexture,
		int depthRenderbufferHandle
	) {
		_device = device;
		Width = width;
		Height = height;
		_framebufferHandle = framebufferHandle;
		ColorTexture = colorTexture;
		_depthRenderbufferHandle = depthRenderbufferHandle;
	}

	~OpenGlRenderTargetContext() {
		EnqueueNativeHandlesForDisposal();
	}

	public IGraphicsDevice Device => _device;

	public int Width { get; }

	public int Height { get; }

	public Texture2D ColorTexture { get; }

	public static Result<IRenderTargetContext, GraphicsError> TryCreate(
		OpenGlGraphicsDevice device,
		RenderTargetContextDescriptor descriptor,
		string? label
	) {
		if (!TryMapColorFormat(
			descriptor.ColorFormat,
			out PixelInternalFormat internalFormat,
			out PixelFormat pixelFormat,
			out PixelType pixelType)
		) {
			return GraphicsError.Unsupported(
				$"Render target color format '{descriptor.ColorFormat}' is not supported."
			);
		}

		int framebuffer = 0;
		int colorTextureHandle = 0;
		int depthRenderbuffer = 0;
		bool created = false;

		try {
			framebuffer = GL.GenFramebuffer();
			if (framebuffer == 0) {
				return GraphicsError.BackendFailure("Failed to allocate framebuffer object.");
			}

			colorTextureHandle = GL.GenTexture();
			if (colorTextureHandle == 0) {
				return GraphicsError.BackendFailure("Failed to allocate render target texture.");
			}

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
			GL.BindTexture(TextureTarget.Texture2D, colorTextureHandle);
			GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				internalFormat,
				descriptor.Width,
				descriptor.Height,
				0,
				pixelFormat,
				pixelType,
				IntPtr.Zero
			);

			GL.TexParameter(
				TextureTarget.Texture2D,
				TextureParameterName.TextureMinFilter,
				(int)OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear
			);
			GL.TexParameter(
				TextureTarget.Texture2D,
				TextureParameterName.TextureMagFilter,
				(int)OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear
			);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			GL.FramebufferTexture2D(
				FramebufferTarget.Framebuffer,
				FramebufferAttachment.ColorAttachment0,
				TextureTarget.Texture2D,
				colorTextureHandle,
				0
			);

			if (descriptor.HasDepthAttachment) {
				depthRenderbuffer = GL.GenRenderbuffer();
				GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRenderbuffer);
				GL.RenderbufferStorage(
					RenderbufferTarget.Renderbuffer,
					RenderbufferStorage.DepthComponent24,
					descriptor.Width,
					descriptor.Height
				);
				GL.FramebufferRenderbuffer(
					FramebufferTarget.Framebuffer,
					FramebufferAttachment.DepthAttachment,
					RenderbufferTarget.Renderbuffer,
					depthRenderbuffer
				);
			}

			FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
			if (status != FramebufferErrorCode.FramebufferComplete) {
				return GraphicsError.BackendFailure(
					$"Framebuffer is incomplete. OpenGL status: {status}."
				);
			}

			if (!string.IsNullOrWhiteSpace(label)) {
				try {
					GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, framebuffer, label.Length, label);
					GL.ObjectLabel(ObjectLabelIdentifier.Texture, colorTextureHandle, label.Length, label);
				} catch (Exception) {
				}
			}

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.BindTexture(TextureTarget.Texture2D, 0);
			GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

			Texture2DDescriptor colorDescriptor = new(descriptor.Width, descriptor.Height, descriptor.ColorFormat) {
				MinFilter = Engine.Graphics.Resources.TextureMinFilter.Linear,
				MagFilter = Engine.Graphics.Resources.TextureMagFilter.Linear,
				WrapU = TextureWrap.ClampToEdge,
				WrapV = TextureWrap.ClampToEdge,
				GenerateMipmaps = false
			};

			Texture2D colorTexture = CreateRenderTargetTexture(device, colorTextureHandle, colorDescriptor);

			created = true;
			return new OpenGlRenderTargetContext(
				device,
				descriptor.Width,
				descriptor.Height,
				framebuffer,
				colorTexture,
				depthRenderbuffer
			);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Failed to create render target context: {exception.Message}");
		} finally {
			if (!created) {
				if (depthRenderbuffer != 0 && GL.IsRenderbuffer(depthRenderbuffer)) {
					GL.DeleteRenderbuffer(depthRenderbuffer);
				}

				if (colorTextureHandle != 0 && GL.IsTexture(colorTextureHandle)) {
					GL.DeleteTexture(colorTextureHandle);
				}

				if (framebuffer != 0 && GL.IsFramebuffer(framebuffer)) {
					GL.DeleteFramebuffer(framebuffer);
				}
			}
		}
	}

	public Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot begin a render pass on a disposed render target context.");
		}

		try {
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebufferHandle);
			GL.Viewport(0, 0, Width, Height);
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to bind render target framebuffer: {exception.Message}");
		}

		return _device.BeginRenderPass(label);
	}

	public Result<GraphicsError> Present() {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot present a disposed render target context.");
		}

		_device.DrainDeferredDisposals();
		return Unit.Value;
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_ = ColorTexture.DisposeChecked();
		EnqueueNativeHandlesForDisposal();
		_device.DrainDeferredDisposals(force: true);

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	private static Texture2D CreateRenderTargetTexture(
		OpenGlGraphicsDevice device,
		int textureHandle,
		Texture2DDescriptor descriptor
	) {
		if (!OpenGlGraphicsDevice.TryGetTextureFormatSpec(descriptor.Format, out var formatSpec)) {
			formatSpec = default;
		}

		int expectedByteCount = descriptor.Width * descriptor.Height * formatSpec.BytesPerPixel;
		return new OpenGlTexture2D(device, textureHandle, descriptor, formatSpec, expectedByteCount, isRenderTarget: true);
	}

	private void EnqueueNativeHandlesForDisposal() {
		int framebufferHandle = Interlocked.Exchange(ref _framebufferHandle, 0);
		if (framebufferHandle != 0) {
			GLGC.Enqueue(
				_device.GarbageCollectorBucketId,
				GLGC.DeletionKind.Framebuffer,
				framebufferHandle
			);
		}

		int depthHandle = Interlocked.Exchange(ref _depthRenderbufferHandle, 0);
		if (depthHandle != 0) {
			GLGC.Enqueue(
				_device.GarbageCollectorBucketId,
				GLGC.DeletionKind.Renderbuffer,
				depthHandle
			);
		}
	}

	private static bool TryMapColorFormat(
		TextureFormat format,
		out PixelInternalFormat internalFormat,
		out PixelFormat pixelFormat,
		out PixelType pixelType
	) {
		switch (format) {
			case TextureFormat.R8:
				internalFormat = PixelInternalFormat.R8;
				pixelFormat = PixelFormat.Red;
				pixelType = PixelType.UnsignedByte;
				return true;
			case TextureFormat.RG8:
				internalFormat = PixelInternalFormat.Rg8;
				pixelFormat = PixelFormat.Rg;
				pixelType = PixelType.UnsignedByte;
				return true;
			case TextureFormat.RGB8:
				internalFormat = PixelInternalFormat.Rgb8;
				pixelFormat = PixelFormat.Rgb;
				pixelType = PixelType.UnsignedByte;
				return true;
			case TextureFormat.RGBA8:
				internalFormat = PixelInternalFormat.Rgba8;
				pixelFormat = PixelFormat.Rgba;
				pixelType = PixelType.UnsignedByte;
				return true;
			default:
				internalFormat = default;
				pixelFormat = default;
				pixelType = default;
				return false;
		}
	}
}
