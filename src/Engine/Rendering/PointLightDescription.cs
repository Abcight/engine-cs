using System.Numerics;

namespace Engine.Rendering;

public readonly record struct PointLightDescription(
	Vector3 Position,
	Vector3 Color,
	float Intensity = 1.0f,
	float Range = 10.0f
);