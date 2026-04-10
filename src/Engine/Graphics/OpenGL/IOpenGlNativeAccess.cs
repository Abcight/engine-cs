namespace Engine.Graphics.OpenGL;

public interface IOpenGlNativeAccess {
	void PushDebugGroup(string label);
	void PopDebugGroup();
}
