namespace Engine.Graphics.Shaders;

public sealed record ShaderLoadSuccess<TBinding>(
	Shader<TBinding> Shader,
	IReadOnlyList<string> Warnings
) where TBinding : class, IGeneratedShaderBinding;