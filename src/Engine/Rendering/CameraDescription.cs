using System.Numerics;

namespace Engine.Rendering;

public readonly record struct PerspectiveCameraDescription(
	Vector3 Position,
	Vector3 Target,
	Vector3 Up,
	float VerticalFieldOfViewRadians,
	float AspectRatio,
	float NearPlane,
	float FarPlane
);

public readonly record struct OrthographicCameraDescription(
	Vector3 Position,
	Vector3 Target,
	Vector3 Up,
	float Width,
	float Height,
	float NearPlane,
	float FarPlane
);

public readonly record struct CameraMatrices(
	Matrix4x4 View,
	Matrix4x4 Projection,
	Vector3 WorldPosition
);