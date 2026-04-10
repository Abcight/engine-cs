namespace Engine.Graphics.Rendering;

[Flags]
public enum ClearTargets {
	None = 0,
	Color = 1 << 0,
	Depth = 1 << 1,
	Stencil = 1 << 2,
	ColorAndDepth = Color | Depth,
	All = Color | Depth | Stencil
}
