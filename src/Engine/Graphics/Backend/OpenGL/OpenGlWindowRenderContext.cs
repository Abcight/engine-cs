using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Engine.Graphics.Backend.OpenGL;

internal sealed class OpenGlWindowRenderContext : IWindowRenderContext {
	private readonly OpenGlGraphicsDevice _device;
	private readonly OpenTkWindowHost _window;
	private WindowRenderCallbacks _callbacks;
	private GraphicsError? _runtimeError;
	private bool _disposed;
	private bool _runInvoked;

	public OpenGlWindowRenderContext(WindowRenderContextOptions options) {
		_device = new OpenGlGraphicsDevice();
		_callbacks = new WindowRenderCallbacks();

		double updateFrequency = options.UpdateFrequency > 0.0 ? options.UpdateFrequency : 60.0;
		var gameWindowSettings = new GameWindowSettings {
			UpdateFrequency = updateFrequency
		};

		var nativeWindowSettings = new NativeWindowSettings {
			Title = options.Title,
			ClientSize = new Vector2i(options.Width, options.Height),
			StartVisible = options.StartVisible,
			StartFocused = options.StartFocused
		};

		_window = new OpenTkWindowHost(this, gameWindowSettings, nativeWindowSettings);
	}

	public IGraphicsDevice Device => _device;

	public int Width => _window.ClientSize.X;

	public int Height => _window.ClientSize.Y;

	public Result<GraphicsError> Run(WindowRenderCallbacks callbacks) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot run a disposed window render context.");
		}

		if (_runInvoked) {
			return GraphicsError.InvalidState("Window render context can only be run once.");
		}

		if (callbacks is null) {
			return GraphicsError.InvalidArgument("Window callbacks cannot be null.");
		}

		if (callbacks.OnRender is null) {
			return GraphicsError.InvalidArgument("Window callbacks must provide an OnRender handler.");
		}

		_runInvoked = true;
		_callbacks = callbacks;

		try {
			_window.Run();
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Window loop failed unexpectedly: {exception.Message}");
		}

		if (_runtimeError is GraphicsError runtimeError) {
			return runtimeError;
		}

		return Unit.Value;
	}

	public Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot begin a render pass on a disposed window context.");
		}

		try {
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.Viewport(0, 0, Math.Max(1, Width), Math.Max(1, Height));
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to bind default framebuffer: {exception.Message}");
		}

		return _device.BeginRenderPass(label);
	}

	public Result<GraphicsError> Present() {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot present a disposed window context.");
		}

		try {
			_window.SwapBuffers();
			_device.DrainDeferredDisposals();
			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to swap window buffers: {exception.Message}");
		}
	}

	public void RequestClose() {
		if (_disposed) {
			return;
		}

		_window.Close();
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		try {
			_window.Close();
		} catch (Exception) {
		}

		_device.DrainDeferredDisposals(force: true);
		_window.Dispose();
		_device.Dispose();
		_disposed = true;
	}

	internal void HandleLoad() {
		InvokeCallback(_callbacks.OnLoad, "load");
	}

	internal void HandleUpdate(double deltaTimeSeconds) {
		if (_callbacks.OnUpdate is null) {
			return;
		}

		InvokeCallback(ctx => _callbacks.OnUpdate(ctx, deltaTimeSeconds), "update");
	}

	internal void HandleRender(double deltaTimeSeconds) {
		if (_callbacks.OnRender is null) {
			SetRuntimeError(GraphicsError.InvalidState("OnRender callback is not set."));
			_window.Close();
			return;
		}

		InvokeCallback(ctx => _callbacks.OnRender(ctx, deltaTimeSeconds), "render");
	}

	internal void HandleResize() {
		try {
			GL.Viewport(0, 0, Math.Max(1, Width), Math.Max(1, Height));
		} catch (Exception exception) {
			SetRuntimeError(GraphicsError.BackendFailure($"Failed to update viewport after resize: {exception.Message}"));
			_window.Close();
			return;
		}

		if (_callbacks.OnResize is null) {
			return;
		}

		InvokeCallback(ctx => _callbacks.OnResize(ctx, Width, Height), "resize");
	}

	internal void HandleUnload() {
		InvokeCallback(_callbacks.OnUnload, "unload");
	}

	private void InvokeCallback(
		Func<IWindowRenderContext, Result<GraphicsError>>? callback,
		string stage
	) {
		if (callback is null) {
			return;
		}

		try {
			Result<GraphicsError> result = callback(this);
			if (result.TryErr() is { Error: var error }) {
				SetRuntimeError(new GraphicsError(
					error.Code,
					$"Window callback '{stage}' failed: {error.Message}"
				));
				Console.Error.WriteLine($"[window:{stage}] {error.Code}: {error.Message}");
				_window.Close();
			}
		} catch (Exception exception) {
			SetRuntimeError(GraphicsError.Unexpected(
				$"Unhandled exception in window callback '{stage}': {exception.Message}"
			));
			Console.Error.WriteLine($"[window:{stage}] Unexpected exception: {exception.Message}");
			_window.Close();
		}
	}

	private void SetRuntimeError(GraphicsError error) {
		if (_runtimeError is not null) {
			return;
		}

		_runtimeError = error;
	}

	private sealed class OpenTkWindowHost : GameWindow {
		private readonly OpenGlWindowRenderContext _owner;

		public OpenTkWindowHost(
			OpenGlWindowRenderContext owner,
			GameWindowSettings gameWindowSettings,
			NativeWindowSettings nativeWindowSettings
		) : base(gameWindowSettings, nativeWindowSettings) {
			_owner = owner;
		}

		protected override void OnLoad() {
			base.OnLoad();
			_owner.HandleLoad();
		}

		protected override void OnUpdateFrame(FrameEventArgs args) {
			base.OnUpdateFrame(args);
			_owner.HandleUpdate(args.Time);
		}

		protected override void OnRenderFrame(FrameEventArgs args) {
			base.OnRenderFrame(args);
			_owner.HandleRender(args.Time);
		}

		protected override void OnResize(ResizeEventArgs e) {
			base.OnResize(e);
			_owner.HandleResize();
		}

		protected override void OnUnload() {
			_owner.HandleUnload();
			base.OnUnload();
		}
	}
}