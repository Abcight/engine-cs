namespace Engine;

public sealed record Unit {
	private Unit() {
	}

	public static Unit Value { get; } = new();
}