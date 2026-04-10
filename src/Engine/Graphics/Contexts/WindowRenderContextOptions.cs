namespace Engine.Graphics.Contexts;

public readonly record struct WindowRenderContextOptions {
	public WindowRenderContextOptions(
		string title,
		int width,
		int height
	) {
		Title = title;
		Width = width;
		Height = height;
		UpdateFrequency = 60.0;
		StartVisible = true;
		StartFocused = true;
	}
	public string Title { get; init; }
	public int Width { get; init; }
	public int Height { get; init; }
	public double UpdateFrequency { get; init; }
	public bool StartVisible { get; init; }
	public bool StartFocused { get; init; }
}
