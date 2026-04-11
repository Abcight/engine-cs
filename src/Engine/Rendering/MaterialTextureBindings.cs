using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

public sealed class MaterialTextureBindings {
	private readonly Dictionary<int, Texture2D> _bindings = new();

	public IReadOnlyDictionary<int, Texture2D> Bindings => _bindings;

	public Result<GraphicsError> Bind(int textureUnit, Texture2D texture) {
		if (textureUnit < 0) {
			return GraphicsError.InvalidArgument("Texture unit cannot be negative.");
		}

		if (texture is null) {
			return GraphicsError.InvalidArgument("Texture cannot be null.");
		}

		_bindings[textureUnit] = texture;
		return Unit.Value;
	}
}
