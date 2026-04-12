using Engine.Graphics.Rendering;

namespace Engine.Rendering;

public readonly record struct MaterialSettings {
	public bool DoubleSided { get; init; }
	public bool DepthWrite { get; init; }
	public bool DepthTest { get; init; }
	public CullMode CullMode { get; init; }
	public RenderDomain RenderDomain { get; init; }

	public static MaterialSettings OpaqueDefault { get; } = new() {
		DoubleSided = false,
		DepthWrite = true,
		DepthTest = true,
		CullMode = CullMode.Back,
		RenderDomain = RenderDomain.OpaqueSurface
	};

	internal MaterialSettings ResolveDefaults() {
		return this == default ? OpaqueDefault : this;
	}
}