namespace Engine.Graphics.Shaders;

public interface IGeneratedShaderBinding {
	GeneratedShaderSchema Schema { get; }
	void Upload(IUniformUploader uploader);
}