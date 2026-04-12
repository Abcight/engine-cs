using System.Numerics;

namespace Engine.Rendering;

public readonly record struct DirectionalLightDescription(
	Vector3 Direction,
	Vector3 Color,
	float Intensity = 1.0f
);