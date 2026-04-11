namespace Engine.Graphics.Shaders;

public interface IEngineSceneShaderBinding {
	bool SupportsEngineSceneContract { get; }

	void SetEngineSceneUniforms(in EngineSceneUniformValues values);
}
