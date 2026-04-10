using System.Diagnostics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Engine;

public class GameWindowCtx : GameWindow, IGameCtx {
	private Stopwatch _frameUpdateStopwatch;
	private Stopwatch _frameRenderStopwatch;

	public GameWindowCtx() : base(
		GameWindowSettings.Default,
		NativeWindowSettings.Default
	) {
		_frameUpdateStopwatch = new();
		_frameRenderStopwatch = new();
		_frameUpdateStopwatch.Start();
		_frameRenderStopwatch.Start();
	}

	protected override void OnUpdateFrame(FrameEventArgs e) {
		base.OnUpdateFrame(e);

		double elapsed = _frameUpdateStopwatch.ElapsedMilliseconds / 1_000.0;
		_frameRenderStopwatch.Reset();
	}

	protected override void OnRenderFrame(FrameEventArgs args) {
		base.OnRenderFrame(args);

		double elapsed = _frameRenderStopwatch.ElapsedMilliseconds / 1_000.0;
		_frameRenderStopwatch.Reset();
	}
}
