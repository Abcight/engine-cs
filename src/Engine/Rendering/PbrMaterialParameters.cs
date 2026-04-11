using System.Numerics;
using Engine.Graphics.Resources;

namespace Engine.Rendering;

public sealed class PbrMaterialParameters {
	public Vector4 BaseColorFactor { get; set; } = Vector4.One;
	public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
	public float MetallicFactor { get; set; } = 1.0f;
	public float RoughnessFactor { get; set; } = 1.0f;
	public float OcclusionStrength { get; set; } = 1.0f;

	public Texture2D? BaseColorTexture { get; set; }
	public Texture2D? NormalTexture { get; set; }
	public Texture2D? MetallicRoughnessTexture { get; set; }
	public Texture2D? OcclusionTexture { get; set; }
	public Texture2D? EmissiveTexture { get; set; }
}
