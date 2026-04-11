using System.Numerics;

namespace Engine.Graphics.Shaders;

public readonly struct EngineSceneUniformValues {
	public Matrix4x4 Model { get; init; }
	public Matrix4x4 View { get; init; }
	public Matrix4x4 Projection { get; init; }
	public Matrix4x4 ModelViewProjection { get; init; }
	public Vector3 CameraWorldPosition { get; init; }

	public int DirectionalLightCount { get; init; }
	public Vector4[] DirectionalLightDirections { get; init; }
	public Vector4[] DirectionalLightColors { get; init; }

	public int PointLightCount { get; init; }
	public Vector4[] PointLightPositions { get; init; }
	public Vector4[] PointLightColors { get; init; }
	public float[] PointLightRanges { get; init; }

	public static EngineSceneUniformValues Empty => new() {
		DirectionalLightDirections = [],
		DirectionalLightColors = [],
		PointLightPositions = [],
		PointLightColors = [],
		PointLightRanges = []
	};
}
