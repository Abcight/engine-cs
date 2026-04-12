namespace Engine.Graphics.Rendering;

[Flags]
public enum ClearTargets {
	None = 0b00000,
	Color = 0b00001,
	Depth = 0b00010,
	Stencil = 0b00100,
	ColorAndDepth = Color | Depth,
	All = Color | Depth | Stencil
}