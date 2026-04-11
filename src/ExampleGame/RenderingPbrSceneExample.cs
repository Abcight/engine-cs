using System.Numerics;
using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Rendering;

namespace ExampleGame;

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

		_cameraPosition = new Vector3(0.0f, 1.5f, 5.2f);
		_cameraTarget = new Vector3(0.0f, 0.0f, 0.0f);
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

		Result<DirectionalLightHandle, GraphicsError> directionalLightResult = _scene.AddDirectionalLight(
			new DirectionalLightDescription(
				Direction: new Vector3(-0.6f, -1.0f, -0.35f),
				Color: new Vector3(1.0f, 0.96f, 0.9f),
				Intensity: 2.7f
			)
		);
		if (directionalLightResult.IsErr) {
			return directionalLightResult.Error;
		}

		Result<PointLightHandle, GraphicsError> pointLightResult = _scene.AddPointLight(
			new PointLightDescription(
				Position: new Vector3(2.0f, 1.8f, 1.8f),
				Color: new Vector3(0.3f, 0.6f, 1.0f),
				Intensity: 8.0f,
				Range: 10.0f
			)
		);
		if (pointLightResult.IsErr) {
			return pointLightResult.Error;
		}

		_movingPointLight = pointLightResult.Value;

		var meshDescriptor = new StaticMeshDescriptor<ScenePbrVertex, uint>(
			Vertices: CubeMeshData.Vertices,
			Indices: CubeMeshData.Indices,
			VertexLayout: ScenePbrVertex.Layout
		);

		Result<ModelHandle, GraphicsError> modelResult = _renderer.CreateStaticModel(
			_scene,
			meshDescriptor,
			BufferUsage.StaticDraw,
			"PbrCube"
		);
		if (modelResult.IsErr) {
			return modelResult.Error;
		}

		ModelHandle cubeModel = modelResult.Value;

		Result<MaterialHandle, GraphicsError> sharedMaterialResult = _renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new Vector4(0.92f, 0.22f, 0.15f, 1.0f),
			MetallicFactor = 0.2f,
			RoughnessFactor = 0.55f,
			EmissiveFactor = new Vector3(0.02f, 0.0f, 0.0f)
		});
		if (sharedMaterialResult.IsErr) {
			return sharedMaterialResult.Error;
		}

		Result<MaterialHandle, GraphicsError> variantMaterialResult = _renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new Vector4(0.14f, 0.4f, 0.95f, 1.0f),
			MetallicFactor = 0.8f,
			RoughnessFactor = 0.22f,
			EmissiveFactor = new Vector3(0.01f, 0.02f, 0.06f)
		});
		if (variantMaterialResult.IsErr) {
			return variantMaterialResult.Error;
		}

		MaterialHandle sharedMaterial = sharedMaterialResult.Value;
		MaterialHandle variantMaterial = variantMaterialResult.Value;

		Result<ModelInstanceHandle, GraphicsError> leftInstanceResult = _scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(-1.4f, 0.0f, 0.0f)
		);
		if (leftInstanceResult.IsErr) {
			return leftInstanceResult.Error;
		}

		Result<ModelInstanceHandle, GraphicsError> rightInstanceResult = _scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(1.4f, 0.0f, 0.0f)
		);
		if (rightInstanceResult.IsErr) {
			return rightInstanceResult.Error;
		}

		Result<ModelInstanceHandle, GraphicsError> centerInstanceResult = _scene.AddModelInstance(
			cubeModel,
			variantMaterial,
			Matrix4x4.CreateTranslation(0.0f, 0.0f, -2.0f)
		);
		if (centerInstanceResult.IsErr) {
			return centerInstanceResult.Error;
		}

		_leftInstance = leftInstanceResult.Value;
		_rightInstance = rightInstanceResult.Value;
		_centerInstance = centerInstanceResult.Value;
		_time = 0.0f;

		return Unit.Value;
	}

	public override Result<GraphicsError> OnUpdate(IWindowRenderContext context, double deltaTimeSeconds) {
		if (_scene is null) {
			return GraphicsError.InvalidState("PBR scene is not initialized.");
		}

		_time += (float)deltaTimeSeconds;

		Result<GraphicsError> leftTransformResult = _scene.SetModelInstanceTransform(
			_leftInstance,
			BuildTransform(
				new Vector3(-1.4f, 0.0f, 0.0f),
				Quaternion.CreateFromYawPitchRoll(_time * 0.9f, _time * 0.4f, _time * 0.2f),
				1.0f
			)
		);
		if (leftTransformResult.IsErr) {
			return leftTransformResult;
		}

		Result<GraphicsError> rightTransformResult = _scene.SetModelInstanceTransform(
			_rightInstance,
			BuildTransform(
				new Vector3(1.4f, 0.0f, 0.0f),
				Quaternion.CreateFromYawPitchRoll(-_time * 1.05f, _time * 0.3f, -_time * 0.6f),
				1.0f
			)
		);
		if (rightTransformResult.IsErr) {
			return rightTransformResult;
		}

		Result<GraphicsError> centerTransformResult = _scene.SetModelInstanceTransform(
			_centerInstance,
			BuildTransform(
				new Vector3(0.0f, 0.0f, -2.0f),
				Quaternion.CreateFromYawPitchRoll(_time * 0.25f, _time * 0.8f, 0.0f),
				1.0f
			)
		);
		if (centerTransformResult.IsErr) {
			return centerTransformResult;
		}

		Vector3 pointPosition = new(
			MathF.Cos(_time * 0.8f) * 2.4f,
			1.5f + MathF.Sin(_time * 1.3f) * 0.5f,
			MathF.Sin(_time * 0.8f) * 2.4f
		);

		return _scene.SetPointLight(
			_movingPointLight,
			new PointLightDescription(
				Position: pointPosition,
				Color: new Vector3(0.3f, 0.6f, 1.0f),
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
			new Vector4(0.05f, 0.07f, 0.11f, 1.0f),
			"Engine.Rendering - PBR Scene"
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
		_scene = null;
		_renderer = null;
		return Unit.Value;
	}

	private static Matrix4x4 BuildTransform(Vector3 translation, Quaternion rotation, float uniformScale) {
		Matrix4x4 scale = Matrix4x4.CreateScale(uniformScale);
		Matrix4x4 rotate = Matrix4x4.CreateFromQuaternion(rotation);
		Matrix4x4 translate = Matrix4x4.CreateTranslation(translation);
		return scale * rotate * translate;
	}
}
