namespace Engine.Graphics.Backend.OpenGL;

public interface IOpenGlNativeAccess {
	void PushDebugGroup(string label);
	void PopDebugGroup();
}