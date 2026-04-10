namespace Engine.Graphics.Shaders;

public enum GraphicsErrorCode {
	None = 0,
	InvalidArgument,
	InvalidState,
	Unsupported,
	DeviceDisposed,
	InvalidContext,
	BackendFailure,
	Unexpected
}

public sealed record GraphicsError(GraphicsErrorCode Code, string Message) {

	public bool IsNone => Code == GraphicsErrorCode.None;

	public static GraphicsError None { get; } = new(GraphicsErrorCode.None, string.Empty);

	public static GraphicsError InvalidArgument(string message) =>
		new(GraphicsErrorCode.InvalidArgument, message);

	public static GraphicsError InvalidState(string message) =>
		new(GraphicsErrorCode.InvalidState, message);

	public static GraphicsError Unsupported(string message) =>
		new(GraphicsErrorCode.Unsupported, message);

	public static GraphicsError DeviceDisposed(string message) =>
		new(GraphicsErrorCode.DeviceDisposed, message);

	public static GraphicsError InvalidContext(string message) =>
		new(GraphicsErrorCode.InvalidContext, message);

	public static GraphicsError BackendFailure(string message) =>
		new(GraphicsErrorCode.BackendFailure, message);

	public static GraphicsError Unexpected(string message) =>
		new(GraphicsErrorCode.Unexpected, message);
}
