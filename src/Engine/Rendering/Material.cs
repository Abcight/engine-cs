using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

internal readonly record struct MaterialSceneContractLimits(
	int DirectionalLightCapacity,
	int PointLightCapacity
);

public abstract class Material {
	private readonly Dictionary<int, Texture2D> _textureBindings = new();

	protected Material(MaterialSettings settings) {
		Settings = settings.ResolveDefaults();
	}

	public MaterialSettings Settings { get; set; }

	public IReadOnlyDictionary<int, Texture2D> TextureBindings => _textureBindings;

	public Result<GraphicsError> SetTextureBinding(int textureUnit, Texture2D texture) {
		if (textureUnit < 0) {
			return GraphicsError.InvalidArgument("Texture unit cannot be negative.");
		}

		if (texture is null) {
			return GraphicsError.InvalidArgument("Texture cannot be null.");
		}

		_textureBindings[textureUnit] = texture;
		return Unit.Value;
	}

	public Result<GraphicsError> RemoveTextureBinding(int textureUnit) {
		if (textureUnit < 0) {
			return GraphicsError.InvalidArgument("Texture unit cannot be negative.");
		}

		_textureBindings.Remove(textureUnit);
		return Unit.Value;
	}

	// TODO: optimize later lol
	internal Result<GraphicsError> ApplyBindings(MaterialTextureBindings bindings) {
		if (bindings is null) {
			return Unit.Value;
		}

		foreach ((int textureUnit, Texture2D texture) in bindings.Bindings.OrderBy(static entry => entry.Key)) {
			Result<GraphicsError> result = SetTextureBinding(textureUnit, texture);
			if (result.IsErr) {
				return result;
			}
		}

		return Unit.Value;
	}

	protected Result<GraphicsError> BindTextures(IRenderPassContext context) {
		foreach ((int textureUnit, Texture2D texture) in _textureBindings.OrderBy(static entry => entry.Key)) {
			Result<GraphicsError> bindResult = context.BindTexture2D(texture, textureUnit);
			if (bindResult.IsErr) {
				return bindResult;
			}
		}

		return Unit.Value;
	}

	internal abstract bool SupportsSceneContract { get; }
	internal abstract MaterialSceneContractLimits SceneContractLimits { get; }
	internal abstract Result<GraphicsError> Bind(IRenderPassContext context, in EngineSceneUniformValues sceneUniformValues);
}

public sealed class Material<TBinding> : Material
	where TBinding : class, IGeneratedShaderBinding {

	private readonly MaterialSceneContractLimits _sceneContractLimits;
	private readonly bool _supportsSceneContract;

	internal Material(
		Shader<TBinding> shader,
		ShaderParameters<TBinding> parameters,
		MaterialSettings settings = default
	) : base(settings) {
		Shader = shader;
		Parameters = parameters;

		_sceneContractLimits = ResolveSceneContractLimits(shader.Inner);
		_supportsSceneContract = shader.Inner is IEngineSceneShaderBinding sceneBinding
			&& sceneBinding.SupportsEngineSceneContract;
	}

	public Shader<TBinding> Shader { get; }

	public ShaderParameters<TBinding> Parameters { get; }

	internal override bool SupportsSceneContract => _supportsSceneContract;

	internal override MaterialSceneContractLimits SceneContractLimits => _sceneContractLimits;

	internal override Result<GraphicsError> Bind(
		IRenderPassContext context,
		in EngineSceneUniformValues sceneUniformValues
	) {
		if (!_supportsSceneContract || Shader.Inner is not IEngineSceneShaderBinding sceneBinding) {
			return GraphicsError.InvalidState(
				$"Shader binding '{typeof(TBinding).Name}' does not satisfy the reserved _engine_ scene contract."
			);
		}

		Parameters.ApplyTo(Shader.Inner);
		sceneBinding.SetEngineSceneUniforms(sceneUniformValues);

		Result<GraphicsError> shaderBindResult = context.BindShader(Shader);
		if (shaderBindResult.IsErr) {
			return shaderBindResult;
		}

		return BindTextures(context);
	}

	private static MaterialSceneContractLimits ResolveSceneContractLimits(TBinding binding) {
		if (binding is not IEngineSceneShaderBinding sceneBinding || !sceneBinding.SupportsEngineSceneContract) {
			return new MaterialSceneContractLimits(0, 0);
		}

		GeneratedShaderSchema schema = binding.Schema;
		int directionalDirections = GetUniformArrayLength(schema, "_engine_directional_light_directions");
		int directionalColors = GetUniformArrayLength(schema, "_engine_directional_light_colors");
		int pointPositions = GetUniformArrayLength(schema, "_engine_point_light_positions");
		int pointColors = GetUniformArrayLength(schema, "_engine_point_light_colors");
		int pointRanges = GetUniformArrayLength(schema, "_engine_point_light_ranges");

		int directionalCapacity = MinPositive(directionalDirections, directionalColors);
		int pointCapacity = MinPositive(pointPositions, pointColors, pointRanges);
		return new MaterialSceneContractLimits(directionalCapacity, pointCapacity);
	}

	private static int GetUniformArrayLength(GeneratedShaderSchema schema, string uniformName) {
		foreach (GeneratedShaderUniform uniform in schema.Uniforms) {
			if (string.Equals(uniform.Name, uniformName, StringComparison.Ordinal)) {
				return Math.Max(0, uniform.ArrayLength);
			}
		}

		return 0;
	}

	private static int MinPositive(params int[] values) {
		int min = int.MaxValue;
		foreach (int value in values) {
			if (value <= 0) {
				return 0;
			}

			if (value < min) {
				min = value;
			}
		}

		return min == int.MaxValue ? 0 : min;
	}
}