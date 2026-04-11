using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Rendering;
using ExampleGame.Core;
using ExampleGame.Shared;

namespace ExampleGame.Examples.Rendering;

internal sealed class RenderingPbrSceneExample : ExampleBase {
	private Renderer? _renderer;
	private RenderScene? _scene;
	private CameraHandle _camera;
	private ModelInstanceHandle _leftInstance;
	private ModelInstanceHandle _rightInstance;
	private ModelInstanceHandle _centerInstance;
	private PointLightHandle _movingPointLight;

	private Vector3 _cameraPosition;
	private Vector3 _cameraTarget;
	private float _time;

	public override string Id => "rendering-pbr";

	public override string DisplayName => "Engine.Rendering PBR Scene";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		var renderer = Renderer.Create(context).Expect("Failed to create scene renderer.");
		var scene = renderer.CreateScene().Expect("Failed to create render scene.");
		_renderer = renderer;
		_scene = scene;

		_cameraPosition = new(0.0f, 1.5f, 5.2f);
		_cameraTarget = new(0.0f, 0.0f, 0.0f);
		float aspectRatio = context.Height > 0 ? context.Width / (float)context.Height : 1.0f;
		var cameraResult = scene.AddPerspectiveCamera(new PerspectiveCameraDescription(
			Position: _cameraPosition,
			Target: _cameraTarget,
			Up: Vector3.UnitY,
			VerticalFieldOfViewRadians: MathF.PI / 3.0f,
			AspectRatio: aspectRatio,
			NearPlane: 0.1f,
			FarPlane: 200.0f
		));
		_camera = cameraResult.Expect("Failed to create the main camera.");

		var directionalLightResult = scene.AddDirectionalLight(
			new DirectionalLightDescription(
				Direction: new(-0.6f, -1.0f, -0.35f),
				Color: new(1.0f, 0.96f, 0.9f),
				Intensity: 2.7f
			)
		);
		_ = directionalLightResult.Expect("Failed to create the directional light.");

		var pointLightResult = scene.AddPointLight(
			new PointLightDescription(
				Position: new(2.0f, 1.8f, 1.8f),
				Color: new(0.3f, 0.6f, 1.0f),
				Intensity: 8.0f,
				Range: 10.0f
			)
		);
		_movingPointLight = pointLightResult.Expect("Failed to create the moving point light.");

		var meshDescriptor = new StaticMeshDescriptor<ScenePbrVertex, uint>(
			Vertices: CubeMeshData.Vertices,
			Indices: CubeMeshData.Indices,
			VertexLayout: ScenePbrVertex.Layout
		);

		var modelResult = renderer.CreateStaticModel(
			scene,
			meshDescriptor,
			BufferUsage.StaticDraw,
			"PbrCube"
		);
		var cubeModel = modelResult.Expect("Failed to create the cube model.");

		var sharedMaterialResult = renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new(0.92f, 0.22f, 0.15f, 1.0f),
			MetallicFactor = 0.2f,
			RoughnessFactor = 0.55f,
			EmissiveFactor = new(0.02f, 0.0f, 0.0f)
		});

		var variantMaterialResult = renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new(0.14f, 0.4f, 0.95f, 1.0f),
			MetallicFactor = 0.8f,
			RoughnessFactor = 0.22f,
			EmissiveFactor = new(0.01f, 0.02f, 0.06f)
		});
		var sharedMaterial = sharedMaterialResult.Expect("Failed to create the shared PBR material.");
		var variantMaterial = variantMaterialResult.Expect("Failed to create the variant PBR material.");

		var leftInstanceResult = scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(-1.4f, 0.0f, 0.0f)
		);
		var rightInstanceResult = scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(1.4f, 0.0f, 0.0f)
		);
		var centerInstanceResult = scene.AddModelInstance(
			cubeModel,
			variantMaterial,
			Matrix4x4.CreateTranslation(0.0f, 0.0f, -2.0f)
		);
		_leftInstance = leftInstanceResult.Expect("Failed to create left model instance.");
		_rightInstance = rightInstanceResult.Expect("Failed to create right model instance.");
		_centerInstance = centerInstanceResult.Expect("Failed to create center model instance.");
		_time = 0.0f;

		return Unit.Value;
	}

	public override Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_scene is null) {
			return GraphicsError.InvalidState("PBR scene is not initialized.");
		}

		_time += (float)deltaTimeSeconds;

		var leftTransformResult = _scene.SetModelInstanceTransform(
			_leftInstance,
			BuildTransform(
				new(-1.4f, 0.0f, 0.0f),
				Quaternion.CreateFromYawPitchRoll(_time * 0.9f, _time * 0.4f, _time * 0.2f),
				1.0f
			)
		);
		if (leftTransformResult.IsErr) {
			return leftTransformResult;
		}

		var rightTransformResult = _scene.SetModelInstanceTransform(
			_rightInstance,
			BuildTransform(
				new(1.4f, 0.0f, 0.0f),
				Quaternion.CreateFromYawPitchRoll(-_time * 1.05f, _time * 0.3f, -_time * 0.6f),
				1.0f
			)
		);
		if (rightTransformResult.IsErr) {
			return rightTransformResult;
		}

		var centerTransformResult = _scene.SetModelInstanceTransform(
			_centerInstance,
			BuildTransform(
				new(0.0f, 0.0f, -2.0f),
				Quaternion.CreateFromYawPitchRoll(_time * 0.25f, _time * 0.8f, 0.0f),
				1.0f
			)
		);
		if (centerTransformResult.IsErr) {
			return centerTransformResult;
		}

		var pointPosition = new Vector3(
			MathF.Cos(_time * 0.8f) * 2.4f,
			1.5f + MathF.Sin(_time * 1.3f) * 0.5f,
			MathF.Sin(_time * 0.8f) * 2.4f
		);

		return _scene.SetPointLight(
			_movingPointLight,
			new PointLightDescription(
				Position: pointPosition,
				Color: new(0.3f, 0.6f, 1.0f),
				Intensity: 8.0f,
				Range: 10.0f
			)
		);
	}

	public override Result<GraphicsError> OnRender(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_renderer is null || _scene is null) {
			return GraphicsError.InvalidState("PBR renderer is not initialized.");
		}

		return _renderer.Render(
			_scene,
			_camera,
			new(0.05f, 0.07f, 0.11f, 1.0f),
			"Engine.Rendering - PBR Scene"
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
		_scene = null;
		_renderer = null;
		return Unit.Value;
	}

	private static Matrix4x4 BuildTransform(Vector3 translation, Quaternion rotation, float uniformScale) {
		var scale = Matrix4x4.CreateScale(uniformScale);
		var rotate = Matrix4x4.CreateFromQuaternion(rotation);
		var translate = Matrix4x4.CreateTranslation(translation);
		return scale * rotate * translate;
	}
}
