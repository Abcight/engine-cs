using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Rendering;

namespace ExampleGame;

internal sealed class RenderingCustomMaterialExample : ExampleBase {
	private Renderer? _renderer;
	private RenderScene? _scene;
	private Shader<CustomSceneShaderBinding>? _shader;
	private CameraHandle _camera;
	private ModelInstanceHandle _leftInstance;
	private ModelInstanceHandle _rightInstance;
	private Vector3 _cameraPosition;
	private Vector3 _cameraTarget;
	private float _time;

	public override string Id => "rendering-custom";

	public override string DisplayName => "Engine.Rendering Custom Material";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		Result<Renderer, GraphicsError> rendererResult = Renderer.Create(context);
		if (rendererResult.IsErr) {
			return rendererResult.Error;
		}

		_renderer = rendererResult.Value;

		Result<RenderScene, GraphicsError> sceneResult = _renderer.CreateScene();
		if (sceneResult.IsErr) {
			return sceneResult.Error;
		}

		_scene = sceneResult.Value;

		Result<Shader<CustomSceneShaderBinding>, GraphicsError> shaderResult = LoadShader<CustomSceneShaderBinding>(context);
		if (shaderResult.IsErr) {
			return shaderResult.Error;
		}

		_shader = shaderResult.Value;

		var meshDescriptor = new StaticMeshDescriptor<ScenePbrVertex, uint>(
			Vertices: CubeMeshData.Vertices,
			Indices: CubeMeshData.Indices,
			VertexLayout: ScenePbrVertex.Layout
		);

		Result<ModelHandle, GraphicsError> modelResult = _renderer.CreateStaticModel(
			_scene,
			meshDescriptor,
			BufferUsage.StaticDraw,
			"CustomMaterialCube"
		);
		if (modelResult.IsErr) {
			return modelResult.Error;
		}

		ModelHandle cubeModel = modelResult.Value;

		_cameraPosition = new Vector3(0.0f, 1.2f, 4.0f);
		_cameraTarget = Vector3.Zero;
		float aspectRatio = context.Height > 0 ? context.Width / (float)context.Height : 1.0f;
		Result<CameraHandle, GraphicsError> cameraResult = _scene.AddPerspectiveCamera(new PerspectiveCameraDescription(
			Position: _cameraPosition,
			Target: _cameraTarget,
			Up: Vector3.UnitY,
			VerticalFieldOfViewRadians: MathF.PI / 3.0f,
			AspectRatio: aspectRatio,
			NearPlane: 0.1f,
			FarPlane: 200.0f
		));
		if (cameraResult.IsErr) {
			return cameraResult.Error;
		}

		_camera = cameraResult.Value;

		Result<DirectionalLightHandle, GraphicsError> lightResult = _scene.AddDirectionalLight(
			new DirectionalLightDescription(
				Direction: new Vector3(-0.5f, -1.0f, -0.25f),
				Color: new Vector3(1.0f, 1.0f, 0.95f),
				Intensity: 2.4f
			)
		);
		if (lightResult.IsErr) {
			return lightResult.Error;
		}

		Result<PointLightHandle, GraphicsError> pointLightResult = _scene.AddPointLight(
			new PointLightDescription(
				Position: new Vector3(0.0f, 1.3f, 2.2f),
				Color: new Vector3(0.7f, 0.7f, 1.0f),
				Intensity: 5.0f,
				Range: 7.0f
			)
		);
		if (pointLightResult.IsErr) {
			return pointLightResult.Error;
		}

		var warmParameters = CustomSceneShaderBinding.NewParameters();
		warmParameters.TintColor = new Vector4(1.0f, 0.55f, 0.24f, 1.0f);
		warmParameters.RimColor = new Vector3(1.0f, 0.92f, 0.75f);
		warmParameters.RimPower = 3.2f;

		var coolParameters = CustomSceneShaderBinding.NewParameters();
		coolParameters.TintColor = new Vector4(0.22f, 0.62f, 1.0f, 1.0f);
		coolParameters.RimColor = new Vector3(0.75f, 0.9f, 1.0f);
		coolParameters.RimPower = 2.2f;

		Result<MaterialHandle, GraphicsError> warmMaterialResult = _renderer.CreateMaterial(_shader, warmParameters);
		if (warmMaterialResult.IsErr) {
			return warmMaterialResult.Error;
		}

		Result<MaterialHandle, GraphicsError> coolMaterialResult = _renderer.CreateMaterial(_shader, coolParameters);
		if (coolMaterialResult.IsErr) {
			return coolMaterialResult.Error;
		}

		Result<ModelInstanceHandle, GraphicsError> leftInstanceResult = _scene.AddModelInstance(
			cubeModel,
			warmMaterialResult.Value,
			Matrix4x4.CreateTranslation(-0.95f, 0.0f, 0.0f)
		);
		if (leftInstanceResult.IsErr) {
			return leftInstanceResult.Error;
		}

		Result<ModelInstanceHandle, GraphicsError> rightInstanceResult = _scene.AddModelInstance(
			cubeModel,
			coolMaterialResult.Value,
			Matrix4x4.CreateTranslation(0.95f, 0.0f, 0.0f)
		);
		if (rightInstanceResult.IsErr) {
			return rightInstanceResult.Error;
		}

		_leftInstance = leftInstanceResult.Value;
		_rightInstance = rightInstanceResult.Value;
		_time = 0.0f;
		return Unit.Value;
	}

	public override Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_scene is null) {
			return GraphicsError.InvalidState("Custom material scene is not initialized.");
		}

		_time += (float)deltaTimeSeconds;

		Result<GraphicsError> leftTransformResult = _scene.SetModelInstanceTransform(
			_leftInstance,
			Matrix4x4.CreateFromYawPitchRoll(_time * 0.9f, _time * 0.45f, _time * 0.2f)
				* Matrix4x4.CreateTranslation(-0.95f, 0.0f, 0.0f)
		);
		if (leftTransformResult.IsErr) {
			return leftTransformResult;
		}

		return _scene.SetModelInstanceTransform(
			_rightInstance,
			Matrix4x4.CreateFromYawPitchRoll(-_time * 0.8f, _time * 0.35f, -_time * 0.25f)
				* Matrix4x4.CreateTranslation(0.95f, 0.0f, 0.0f)
		);
	}

	public override Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_renderer is null || _scene is null) {
			return GraphicsError.InvalidState("Custom material renderer is not initialized.");
		}

		return _renderer.Render(
			_scene,
			_camera,
			new Vector4(0.07f, 0.05f, 0.07f, 1.0f),
			"Engine.Rendering - Custom Material"
		);
	}

	public override Result<GraphicsError> OnResize(IWindowRenderContext context, int width, int height) {
		if (_scene is null) {
			return Unit.Value;
		}

		float aspectRatio = height > 0 ? width / (float)height : 1.0f;
		Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, Vector3.UnitY);
		Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
			MathF.PI / 3.0f,
			aspectRatio,
			0.1f,
			200.0f
		);

		return _scene.SetCameraMatrices(_camera, new CameraMatrices(view, projection, _cameraPosition));
	}

	public override Result<GraphicsError> OnUnload(IWindowRenderContext context) {
		_scene?.Dispose();
		_renderer?.Dispose();
		_shader?.DisposeChecked();
		_scene = null;
		_renderer = null;
		_shader = null;
		return Unit.Value;
	}
}
