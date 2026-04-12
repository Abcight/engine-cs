using System.Numerics;
using Engine.Graphics.Assets;
using Engine.Graphics.Contexts;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;

namespace Engine.Rendering;

public sealed class Renderer : IDisposable {
	private readonly IRenderContext _context;
	private readonly TemporaryRenderTargetPool _temporaryRenderTargetPool;
	private readonly Dictionary<int, Material> _materials = new();
	private readonly List<IRenderFeature> _features = [];

	private Shader<BuiltInPbrShaderBinding>? _builtInPbrShader;
	private int _nextMaterialId = 1;
	private bool _disposed;

	private Renderer(IRenderContext context) {
		_context = context;
		_temporaryRenderTargetPool = new TemporaryRenderTargetPool(context);
	}

	public IRenderContext Context => _context;

	public IGraphicsDevice Device => _context.Device;

	public ITemporaryRenderTargetPool TemporaryRenderTargets => _temporaryRenderTargetPool;

	public static Result<Renderer, GraphicsError> Create(IRenderContext context) {
		if (context is null) {
			return GraphicsError.InvalidArgument("Render context cannot be null.");
		}

		return new Renderer(context);
	}

	public Result<RenderScene, GraphicsError> CreateScene() {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create a scene from a disposed renderer.");
		}

		return new RenderScene();
	}

	public Result<Assets, GraphicsError> CreateAssets(AssetsOptions? options = null) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create an assets registry from a disposed renderer.");
		}

		return new Assets(_context, options);
	}

	public Result<Shader<TBinding>, GraphicsError> LoadShader<TBinding>(
		Action<string>? onWarning = null
	)
		where TBinding : class, IGeneratedShaderBinding, new() {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot load shaders from a disposed renderer.");
		}

		return _context.LoadShader<TBinding>(onWarning);
	}

	public Result<GraphicsError> AddFeature(IRenderFeature feature) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add a feature to a disposed renderer.");
		}

		if (feature is null) {
			return GraphicsError.InvalidArgument("Render feature cannot be null.");
		}

		if (_features.Contains(feature)) {
			return Unit.Value;
		}

		_features.Add(feature);
		return Unit.Value;
	}

	public Result<GraphicsError> RemoveFeature(IRenderFeature feature) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove a feature from a disposed renderer.");
		}

		if (feature is null) {
			return GraphicsError.InvalidArgument("Render feature cannot be null.");
		}

		_features.Remove(feature);
		return Unit.Value;
	}

	public Result<ModelHandle, GraphicsError> CreateStaticModel<TVertex, TIndex>(
		RenderScene scene,
		StaticMeshDescriptor<TVertex, TIndex> descriptor,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TVertex : unmanaged
		where TIndex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create model resources on a disposed renderer.");
		}

		if (scene is null) {
			return GraphicsError.InvalidArgument("Render scene cannot be null.");
		}

		if (descriptor.VertexLayout is null) {
			return GraphicsError.InvalidArgument("Static mesh descriptor must provide a vertex layout.");
		}

		string? vertexLabel = string.IsNullOrWhiteSpace(label) ? null : $"{label}.Vertices";
		string? indexLabel = string.IsNullOrWhiteSpace(label) ? null : $"{label}.Indices";

		Result<VertexBuffer<TVertex>, GraphicsError> vertexBufferResult = _context.Device.CreateVertexBuffer(
			descriptor.Vertices.Span,
			usage,
			vertexLabel
		);
		if (vertexBufferResult.IsErr) {
			return vertexBufferResult.Error;
		}

		VertexBuffer<TVertex> vertexBuffer = vertexBufferResult.Value;
		Result<IndexBuffer<TIndex>, GraphicsError> indexBufferResult = _context.Device.CreateIndexBuffer(
			descriptor.Indices.Span,
			usage,
			indexLabel
		);
		if (indexBufferResult.IsErr) {
			_ = vertexBuffer.DisposeChecked();
			return indexBufferResult.Error;
		}

		IndexBuffer<TIndex> indexBuffer = indexBufferResult.Value;
		Result<ModelHandle, GraphicsError> modelResult = scene.AddModel(
			vertexBuffer,
			indexBuffer,
			descriptor.VertexLayout,
			descriptor.Topology,
			disposeWithScene: true
		);
		if (modelResult.IsErr) {
			_ = indexBuffer.DisposeChecked();
			_ = vertexBuffer.DisposeChecked();
		}

		return modelResult;
	}

	public Result<MaterialHandle, GraphicsError> CreateMaterial<TBinding, TParameters>(
		Shader<TBinding> shader,
		TParameters parameters,
		MaterialSettings settings = default
	)
		where TBinding : class, IGeneratedShaderBinding
		where TParameters : ShaderParameters<TBinding> {
		return CreateMaterial(shader, parameters, settings, textureBindings: null);
	}

	public Result<MaterialHandle, GraphicsError> CreateMaterial<TBinding, TParameters>(
		Shader<TBinding> shader,
		TParameters parameters,
		MaterialSettings settings,
		MaterialTextureBindings? textureBindings
	)
		where TBinding : class, IGeneratedShaderBinding
		where TParameters : ShaderParameters<TBinding> {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create a material on a disposed renderer.");
		}

		if (shader is null) {
			return GraphicsError.InvalidArgument("Shader cannot be null.");
		}

		if (parameters is null) {
			return GraphicsError.InvalidArgument("Shader parameters cannot be null.");
		}

		var material = new Material<TBinding>(shader, parameters, settings.ResolveDefaults());
		if (!material.SupportsSceneContract) {
			return GraphicsError.InvalidArgument(
				$"Shader binding '{typeof(TBinding).Name}' is missing the required _engine_ scene contract uniforms."
			);
		}

		if (textureBindings is not null) {
			Result<GraphicsError> textureResult = material.ApplyBindings(textureBindings);
			if (textureResult.IsErr) {
				return textureResult.Error;
			}
		}

		int id = _nextMaterialId++;
		_materials[id] = material;
		return new MaterialHandle(id);
	}

	public Result<MaterialHandle, GraphicsError> CreatePbrMaterial(
		PbrMaterialParameters parameters,
		MaterialSettings settings = default
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create a material on a disposed renderer.");
		}

		if (parameters is null) {
			return GraphicsError.InvalidArgument("PBR material parameters cannot be null.");
		}

		Result<Shader<BuiltInPbrShaderBinding>, GraphicsError> shaderResult = EnsureBuiltInPbrShader();
		if (shaderResult.IsErr) {
			return shaderResult.Error;
		}

		Shader<BuiltInPbrShaderBinding> shader = shaderResult.Value;
		var generatedParameters = BuiltInPbrShaderBinding.NewParameters();

		generatedParameters.BaseColorFactor = parameters.BaseColorFactor;
		generatedParameters.EmissiveFactor = parameters.EmissiveFactor;
		generatedParameters.MetallicFactor = MathF.Max(0.0f, parameters.MetallicFactor);
		generatedParameters.RoughnessFactor = Math.Clamp(parameters.RoughnessFactor, 0.04f, 1.0f);
		generatedParameters.OcclusionStrength = Math.Clamp(parameters.OcclusionStrength, 0.0f, 1.0f);

		generatedParameters.BaseColorTexture = 0;
		generatedParameters.NormalTexture = 1;
		generatedParameters.MetallicRoughnessTexture = 2;
		generatedParameters.RoughnessTexture = 3;
		generatedParameters.OcclusionTexture = 4;
		generatedParameters.EmissiveTexture = 5;

		generatedParameters.HasBaseColorTexture = parameters.BaseColorTexture is not null;
		generatedParameters.HasNormalTexture = parameters.NormalTexture is not null;
		generatedParameters.HasMetallicRoughnessTexture = parameters.MetallicRoughnessTexture is not null;
		generatedParameters.HasRoughnessTexture = parameters.RoughnessTexture is not null;
		generatedParameters.HasOcclusionTexture = parameters.OcclusionTexture is not null;
		generatedParameters.HasEmissiveTexture = parameters.EmissiveTexture is not null;

		var bindings = new MaterialTextureBindings();
		if (parameters.BaseColorTexture is { } baseColorTexture) {
			Result<GraphicsError> result = bindings.Bind(0, baseColorTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		if (parameters.NormalTexture is { } normalTexture) {
			Result<GraphicsError> result = bindings.Bind(1, normalTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		if (parameters.MetallicRoughnessTexture is { } metallicRoughnessTexture) {
			Result<GraphicsError> result = bindings.Bind(2, metallicRoughnessTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		if (parameters.RoughnessTexture is { } roughnessTexture) {
			Result<GraphicsError> result = bindings.Bind(3, roughnessTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		if (parameters.OcclusionTexture is { } occlusionTexture) {
			Result<GraphicsError> result = bindings.Bind(4, occlusionTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		if (parameters.EmissiveTexture is { } emissiveTexture) {
			Result<GraphicsError> result = bindings.Bind(5, emissiveTexture);
			if (result.IsErr) {
				return result.Error;
			}
		}

		return CreateMaterial(shader, generatedParameters, settings, bindings);
	}

	public Result<Material, GraphicsError> GetMaterial(MaterialHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot access materials on a disposed renderer.");
		}

		if (!_materials.TryGetValue(handle.Value, out Material? material)) {
			return GraphicsError.InvalidArgument($"Material handle '{handle.Value}' does not exist.");
		}

		return material;
	}

	public Result<GraphicsError> SetMaterialSettings(MaterialHandle handle, MaterialSettings settings) {
		Result<Material, GraphicsError> materialResult = GetMaterial(handle);
		if (materialResult.IsErr) {
			return materialResult.Error;
		}

		materialResult.Value.Settings = settings.ResolveDefaults();
		return Unit.Value;
	}

	public Result<GraphicsError> SetMaterialTexture(MaterialHandle handle, int textureUnit, Texture2D texture) {
		Result<Material, GraphicsError> materialResult = GetMaterial(handle);
		if (materialResult.IsErr) {
			return materialResult.Error;
		}

		return materialResult.Value.SetTextureBinding(textureUnit, texture);
	}

	public Result<GraphicsError> RemoveMaterialTexture(MaterialHandle handle, int textureUnit) {
		Result<Material, GraphicsError> materialResult = GetMaterial(handle);
		if (materialResult.IsErr) {
			return materialResult.Error;
		}

		return materialResult.Value.RemoveTextureBinding(textureUnit);
	}

	public Result<GraphicsError> Render(
		RenderScene scene,
		CameraHandle cameraHandle,
		Vector4 clearColor,
		string? label = null
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot render with a disposed renderer.");
		}

		if (scene is null) {
			return GraphicsError.InvalidArgument("Render scene cannot be null.");
		}

		if (!scene.TryGetCameraState(cameraHandle, out RenderScene.CameraState cameraState)) {
			return GraphicsError.InvalidArgument($"Camera handle '{cameraHandle.Value}' does not exist.");
		}

		var featureContext = new RenderFeatureContext(
			Renderer: this,
			Context: _context,
			Device: _context.Device,
			Scene: scene,
			CameraHandle: cameraHandle,
			Camera: cameraState.ToCameraMatrices(),
			TemporaryRenderTargets: _temporaryRenderTargetPool
		);

		Result<GraphicsError> beforeFeaturesResult = InvokeBeforeSceneFeatures(featureContext);
		if (beforeFeaturesResult.IsErr) {
			return beforeFeaturesResult;
		}

		using RenderPass pass = _context.BeginPass(label ?? "Engine.Rendering.Scene");
		pass.Clear(ClearTargets.ColorAndDepth, clearColor);
		foreach (RenderScene.ModelInstanceState instance in scene.EnumerateModelInstances()) {
			pass.Run(ctx => DrawInstance(ctx, scene, cameraState, instance));
			if (pass.IsFaulted) {
				break;
			}
		}

		Result<GraphicsError> passEndResult = pass.End();
		if (passEndResult.IsErr) {
			return passEndResult;
		}

		Result<GraphicsError> afterFeaturesResult = InvokeAfterSceneFeatures(featureContext);
		if (afterFeaturesResult.IsErr) {
			return afterFeaturesResult;
		}

		return _context.Present();
	}

	public Result<GraphicsError> Render(
		RenderScene scene,
		CameraHandle cameraHandle,
		string? label = null
	) {
		return Render(scene, cameraHandle, new Vector4(0.03f, 0.03f, 0.04f, 1.0f), label);
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_temporaryRenderTargetPool.Dispose();
		_ = _builtInPbrShader?.DisposeChecked();

		_features.Clear();
		_materials.Clear();
		_disposed = true;
	}

	private Result<GraphicsError> DrawInstance(
		IRenderPassContext pass,
		RenderScene scene,
		in RenderScene.CameraState cameraState,
		in RenderScene.ModelInstanceState instance
	) {
		if (!scene.TryGetModelState(instance.ModelHandle, out RenderScene.SceneModel? model)) {
			return GraphicsError.InvalidState(
				$"Scene instance references missing model handle '{instance.ModelHandle.Value}'."
			);
		}

		if (!_materials.TryGetValue(instance.MaterialHandle.Value, out Material? material)) {
			return GraphicsError.InvalidState(
				$"Scene instance references missing material handle '{instance.MaterialHandle.Value}'."
			);
		}

		MaterialSettings settings = material.Settings.ResolveDefaults();
		if (settings.RenderDomain != RenderDomain.OpaqueSurface) {
			return Unit.Value;
		}

		CullMode cullMode = settings.DoubleSided ? CullMode.None : settings.CullMode;

		Result<GraphicsError> depthTestResult = pass.SetDepthTestEnabled(settings.DepthTest);
		if (depthTestResult.IsErr) {
			return depthTestResult;
		}

		Result<GraphicsError> depthWriteResult = pass.SetDepthWriteEnabled(settings.DepthWrite);
		if (depthWriteResult.IsErr) {
			return depthWriteResult;
		}

		Result<GraphicsError> cullResult = pass.SetCullMode(cullMode);
		if (cullResult.IsErr) {
			return cullResult;
		}

		EngineSceneUniformValues uniforms = BuildSceneUniformValues(
			scene,
			cameraState,
			instance.WorldTransform,
			material.SceneContractLimits
		);

		Result<GraphicsError> bindMaterialResult = material.Bind(pass, uniforms);
		if (bindMaterialResult.IsErr) {
			return bindMaterialResult;
		}

		Result<GraphicsError> bindModelResult = model.Bind(pass);
		if (bindModelResult.IsErr) {
			return bindModelResult;
		}

		return model.Draw(pass);
	}

	private Result<GraphicsError> InvokeBeforeSceneFeatures(in RenderFeatureContext context) {
		foreach (IRenderFeature feature in _features) {
			Result<GraphicsError> result = feature.BeforeScene(context);
			if (result.IsErr) {
				return result;
			}
		}

		return Unit.Value;
	}

	private Result<GraphicsError> InvokeAfterSceneFeatures(in RenderFeatureContext context) {
		foreach (IRenderFeature feature in _features) {
			Result<GraphicsError> result = feature.AfterScene(context);
			if (result.IsErr) {
				return result;
			}
		}

		return Unit.Value;
	}

	private Result<Shader<BuiltInPbrShaderBinding>, GraphicsError> EnsureBuiltInPbrShader() {
		if (_builtInPbrShader is not null) {
			return _builtInPbrShader;
		}

		Result<Shader<BuiltInPbrShaderBinding>, GraphicsError> loadResult = LoadShader<BuiltInPbrShaderBinding>(
			static warning => {
				if (!IsIgnorableBuiltInPbrWarning(warning)) {
					Console.WriteLine($"[rendering:pbr warning] {warning}");
				}
			}
		);
		if (loadResult.IsErr) {
			return loadResult.Error;
		}

		_builtInPbrShader = loadResult.Value;
		return _builtInPbrShader;
	}

	private static bool IsIgnorableBuiltInPbrWarning(string warning) {
		if (string.IsNullOrWhiteSpace(warning)) {
			return false;
		}

		return warning.Contains(
			"Expected uniform '_engine_model_view_projection' was not active in the linked program.",
			StringComparison.Ordinal
		);
	}

	private static EngineSceneUniformValues BuildSceneUniformValues(
		RenderScene scene,
		in RenderScene.CameraState cameraState,
		in Matrix4x4 model,
		in MaterialSceneContractLimits lightCapacity
	) {
		int maxDirectionalLights = Math.Max(0, lightCapacity.DirectionalLightCapacity);
		int maxPointLights = Math.Max(0, lightCapacity.PointLightCapacity);

		Vector4[] directionalLightDirections = maxDirectionalLights > 0 ? new Vector4[maxDirectionalLights] : [];
		Vector4[] directionalLightColors = maxDirectionalLights > 0 ? new Vector4[maxDirectionalLights] : [];
		Vector4[] pointLightPositions = maxPointLights > 0 ? new Vector4[maxPointLights] : [];
		Vector4[] pointLightColors = maxPointLights > 0 ? new Vector4[maxPointLights] : [];
		float[] pointLightRanges = maxPointLights > 0 ? new float[maxPointLights] : [];

		int directionalLightCount = 0;
		if (maxDirectionalLights > 0) {
			foreach (DirectionalLightDescription light in scene.EnumerateDirectionalLights()) {
				if (directionalLightCount >= maxDirectionalLights) {
					break;
				}

				Vector3 direction = light.Direction.LengthSquared() > 1e-8f
					? Vector3.Normalize(light.Direction)
					: -Vector3.UnitY;

				Vector3 color = ClampNonNegative(light.Color) * MathF.Max(0.0f, light.Intensity);
				directionalLightDirections[directionalLightCount] = new Vector4(direction, 0.0f);
				directionalLightColors[directionalLightCount] = new Vector4(color, 1.0f);
				directionalLightCount++;
			}
		}

		int pointLightCount = 0;
		if (maxPointLights > 0) {
			foreach (PointLightDescription light in scene.EnumeratePointLights()) {
				if (pointLightCount >= maxPointLights) {
					break;
				}

				Vector3 color = ClampNonNegative(light.Color) * MathF.Max(0.0f, light.Intensity);
				pointLightPositions[pointLightCount] = new Vector4(light.Position, 1.0f);
				pointLightColors[pointLightCount] = new Vector4(color, 1.0f);
				pointLightRanges[pointLightCount] = MathF.Max(0.0001f, light.Range);
				pointLightCount++;
			}
		}

		Matrix4x4 modelViewProjection = model * cameraState.View * cameraState.Projection;
		return new EngineSceneUniformValues {
			Model = model,
			View = cameraState.View,
			Projection = cameraState.Projection,
			ModelViewProjection = modelViewProjection,
			CameraWorldPosition = cameraState.WorldPosition,
			DirectionalLightCount = directionalLightCount,
			DirectionalLightDirections = directionalLightDirections,
			DirectionalLightColors = directionalLightColors,
			PointLightCount = pointLightCount,
			PointLightPositions = pointLightPositions,
			PointLightColors = pointLightColors,
			PointLightRanges = pointLightRanges
		};
	}

	private static Vector3 ClampNonNegative(Vector3 value) {
		return new Vector3(
			MathF.Max(0.0f, value.X),
			MathF.Max(0.0f, value.Y),
			MathF.Max(0.0f, value.Z)
		);
	}
}
