using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Rendering;
using ExampleGame.Core;
using ExampleGame.Shaders;
using ExampleGame.Shared;

namespace ExampleGame.Examples.Rendering;

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
		var renderer = Renderer.Create(context).Expect("Failed to create scene renderer.");
		var scene = renderer.CreateScene().Expect("Failed to create render scene.");
		var shader = renderer.LoadShader<CustomSceneShaderBinding>(
			static warning => Console.WriteLine($"[shader warning] {warning}")
		)
			.Expect("Failed to load custom scene shader.");

		_renderer = renderer;
		_scene = scene;
		_shader = shader;

		var meshDescriptor = new StaticMeshDescriptor<ScenePbrVertex, uint>(
			Vertices: CubeMeshData.Vertices,
			Indices: CubeMeshData.Indices,
			VertexLayout: ScenePbrVertex.Layout
		);

		var modelResult = renderer.CreateStaticModel(
			scene,
			meshDescriptor,
			BufferUsage.StaticDraw,
			"CustomMaterialCube"
		);

		var cubeModel = modelResult.Expect("Failed to create the cube model.");

		_cameraPosition = new(0.0f, 1.2f, 4.0f);
		_cameraTarget = Vector3.Zero;
		float aspectRatio = context.Height > 0 ? context.Width / (float)context.Height : 1.0f;
		var cameraResult = scene.AddPerspectiveCamera(new(
			Position: _cameraPosition,
			Target: _cameraTarget,
			Up: Vector3.UnitY,
			VerticalFieldOfViewRadians: MathF.PI / 3.0f,
			AspectRatio: aspectRatio,
			NearPlane: 0.1f,
			FarPlane: 200.0f
		));
		_camera = cameraResult.Expect("Failed to create the main camera.");

		var lightResult = scene.AddDirectionalLight(
			new(
				Direction: new(-0.5f, -1.0f, -0.25f),
				Color: new(1.0f, 1.0f, 0.95f),
				Intensity: 2.4f
			)
		).Expect("Failed to create the directional light.");

		scene.AddPointLight(
			new(
				Position: new(0.0f, 1.3f, 2.2f),
				Color: new(0.7f, 0.7f, 1.0f),
				Intensity: 5.0f,
				Range: 7.0f
			)
		).Expect("Failed to create the point light.");

		var warmParameters = CustomSceneShaderBinding.NewParameters();
		warmParameters.TintColor = new(1.0f, 0.55f, 0.24f, 1.0f);
		warmParameters.RimColor = new(1.0f, 0.92f, 0.75f);
		warmParameters.RimPower = 3.2f;

		var coolParameters = CustomSceneShaderBinding.NewParameters();
		coolParameters.TintColor = new(0.22f, 0.62f, 1.0f, 1.0f);
		coolParameters.RimColor = new(0.75f, 0.9f, 1.0f);
		coolParameters.RimPower = 2.2f;

		var warmMaterialResult = renderer.CreateMaterial(shader, warmParameters);
		var coolMaterialResult = renderer.CreateMaterial(shader, coolParameters);
		var warmMaterialHandle = warmMaterialResult.Expect("Failed to create warm material.");
		var coolMaterialHandle = coolMaterialResult.Expect("Failed to create cool material.");

		var leftInstanceResult = scene.AddModelInstance(
			cubeModel,
			warmMaterialHandle,
			Matrix4x4.CreateTranslation(-0.95f, 0.0f, 0.0f)
		);
		var rightInstanceResult = scene.AddModelInstance(
			cubeModel,
			coolMaterialHandle,
			Matrix4x4.CreateTranslation(0.95f, 0.0f, 0.0f)
		);
		_leftInstance = leftInstanceResult.Expect("Failed to create left model instance.");
		_rightInstance = rightInstanceResult.Expect("Failed to create right model instance.");
		_time = 0.0f;
		return Unit.Value;
	}

	public override Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_scene is null) {
			return GraphicsError.InvalidState("Custom material scene is not initialized.");
		}

		_time += (float)deltaTimeSeconds;

		var leftTransformResult = _scene.SetModelInstanceTransform(
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
			new(0.07f, 0.05f, 0.07f, 1.0f),
			"Engine.Rendering - Custom Material"
		);
	}

	public override Result<GraphicsError> OnResize(IWindowRenderContext context, int width, int height) {
		if (_scene is null) {
			return Unit.Value;
		}

		float aspectRatio = height > 0 ? width / (float)height : 1.0f;
		var view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, Vector3.UnitY);
		var projection = Matrix4x4.CreatePerspectiveFieldOfView(
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