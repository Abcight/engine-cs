namespace Engine.Graphics.Shaders;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ShaderAttribute : Attribute {
	public ShaderAttribute(string vertexShaderPath, string fragmentShaderPath) {
		VertexShaderPath = vertexShaderPath;
		FragmentShaderPath = fragmentShaderPath;
	}
	public string VertexShaderPath { get; }
	public string FragmentShaderPath { get; }
}
