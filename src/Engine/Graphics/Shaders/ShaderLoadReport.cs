namespace Engine.Graphics.Shaders;

public sealed class ShaderLoadReport {
	private ShaderLoadReport(bool success, string? error, IReadOnlyList<string> warnings) {
		Success = success;
		Error = error;
		Warnings = warnings;
	}
	public bool Success { get; }
	public string? Error { get; }
	public IReadOnlyList<string> Warnings { get; }

	public static ShaderLoadReport Successful(IEnumerable<string>? warnings = null) {
		return new(true, null, warnings?.ToArray() ?? []);
	}

	public static ShaderLoadReport Failed(string error, IEnumerable<string>? warnings = null) {
		return new(false, error, warnings?.ToArray() ?? []);
	}
}