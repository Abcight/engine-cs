namespace Engine.Rendering;

public readonly record struct CameraHandle(int Value) {
	public static CameraHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}

public readonly record struct ModelHandle(int Value) {
	public static ModelHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}

public readonly record struct ModelInstanceHandle(int Value) {
	public static ModelInstanceHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}

public readonly record struct MaterialHandle(int Value) {
	public static MaterialHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}

public readonly record struct DirectionalLightHandle(int Value) {
	public static DirectionalLightHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}

public readonly record struct PointLightHandle(int Value) {
	public static PointLightHandle Invalid { get; } = new(-1);
	public bool IsValid => Value >= 0;
}