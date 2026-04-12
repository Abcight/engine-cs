namespace Engine.Graphics.Shaders;

[Flags]
public enum ShaderStage {
	None = 0b0000,
	Vertex = 0b0001,
	Fragment = 0b0010,
	Compute = 0b0100
}

public enum ShaderUniformType {
	Float,
	Int,
	UInt,
	Bool,
	Vec2,
	Vec3,
	Vec4,
	Mat4,
	Sampler2D,
	SamplerCube
}

public sealed record GeneratedShaderUniform(
	string Name,
	ShaderUniformType Type,
	int ArrayLength,
	ShaderStage Stages
);

public sealed class GeneratedShaderSchema {
	public GeneratedShaderSchema(
		string vertexShaderPath,
		string fragmentShaderPath,
		IReadOnlyList<GeneratedShaderUniform> uniforms
	) {
		VertexShaderPath = vertexShaderPath;
		FragmentShaderPath = fragmentShaderPath;
		Uniforms = uniforms;
	}
	public string VertexShaderPath { get; }
	public string FragmentShaderPath { get; }
	public IReadOnlyList<GeneratedShaderUniform> Uniforms { get; }
}