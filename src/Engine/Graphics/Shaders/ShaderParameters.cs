namespace Engine.Graphics.Shaders;

public abstract class ShaderParameters<TBinding>
	where TBinding : class, IGeneratedShaderBinding {

	public abstract void ApplyTo(TBinding binding);

	public abstract ShaderParameters<TBinding> Clone();
}
