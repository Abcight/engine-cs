using System.Numerics;
using Engine;
using Engine.Graphics.Assets;
using Engine.Graphics.Contexts;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Rendering;
using ExampleGame.Assets;
using ExampleGame.Core;
using ExampleGame.Shared;
using GraphicsAssets = Engine.Graphics.Assets.Assets;

namespace ExampleGame.Examples.Rendering;

internal sealed class RenderingPbrSceneExample : ExampleBase {
	private GraphicsAssets? _assetsRegistry;
	private Renderer? _renderer;
	private RenderScene? _scene;
	private CrateAssets? _crateAssets;
	private CameraHandle _camera;
	private ModelInstanceHandle _leftInstance;
	private ModelInstanceHandle _rightInstance;
	private ModelInstanceHandle _centerInstance;
	private ModelInstanceHandle _pointLightMarker;
	private PointLightHandle _movingPointLight;

	private Vector3 _cameraPosition;
	private Vector3 _cameraTarget;
	private float _time;

	public override string Id => "rendering-pbr";

	public override string DisplayName => "Engine.Rendering PBR Scene";

	public override Result<GraphicsError> OnLoad(IWindowRenderContext context) {
		_renderer = Renderer.Create(context).Expect("Failed to create scene renderer.");
		_scene = _renderer.CreateScene().Expect("Failed to create render scene.");
		_assetsRegistry = _renderer.CreateAssets().Expect("Failed to create assets registry.");
		_crateAssets = CrateAssets.FromAssets(_assetsRegistry);

		_cameraPosition = new(0.0f, 1.5f, 5.2f);
		_cameraTarget = new(0.0f, 0.0f, 0.0f);
		float aspectRatio = context.Height > 0 ? context.Width / (float)context.Height : 1.0f;
		var cameraResult = _scene.AddPerspectiveCamera(new PerspectiveCameraDescription(
			Position: _cameraPosition,
			Target: _cameraTarget,
			Up: Vector3.UnitY,
			VerticalFieldOfViewRadians: MathF.PI / 3.0f,
			AspectRatio: aspectRatio,
			NearPlane: 0.1f,
			FarPlane: 200.0f
		));
		_camera = cameraResult.Expect("Failed to create the main camera.");

		_scene.AddDirectionalLight(
			new DirectionalLightDescription(
				Direction: new(-0.6f, -1.0f, -0.35f),
				Color: new(1.0f, 0.96f, 0.9f),
				Intensity: 2.7f
			)
		).Expect("Failed to create the directional light.");

		_movingPointLight = _scene.AddPointLight(
			new PointLightDescription(
				Position: new(2.0f, 1.8f, 1.8f),
				Color: new(0.3f, 0.6f, 1.0f),
				Intensity: 8.0f,
				Range: 10.0f
			)
		).Expect("Failed to create the moving point light.");

		var meshDescriptor = new StaticMeshDescriptor<ScenePbrVertex, uint>(
			Vertices: CubeMeshData.Vertices,
			Indices: CubeMeshData.Indices,
			VertexLayout: ScenePbrVertex.Layout
		);

		var modelResult = _renderer.CreateStaticModel(
			_scene,
			meshDescriptor,
			BufferUsage.StaticDraw,
			"PbrCube"
		);
		var cubeModel = modelResult.Expect("Failed to create the cube model.");

		var sharedMaterialResult = _renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = Vector4.One,
			MetallicFactor = 1.0f,
			RoughnessFactor = 1.0f,
			OcclusionStrength = 1.0f,
			BaseColorTexture = _crateAssets.BaseColor.Get(),
			NormalTexture = _crateAssets.Normal.Get(),
			MetallicRoughnessTexture = _crateAssets.Metallic.Get(),
			RoughnessTexture = _crateAssets.Roughness.Get(),
			OcclusionTexture = _crateAssets.Occlusion.Get()
		});

		var variantMaterialResult = _renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new(0.72f, 0.84f, 1.0f, 1.0f),
			MetallicFactor = 1.0f,
			RoughnessFactor = 0.75f,
			OcclusionStrength = 1.0f,
			BaseColorTexture = _crateAssets.BaseColor.Get(),
			NormalTexture = _crateAssets.Normal.Get(),
			MetallicRoughnessTexture = _crateAssets.Metallic.Get(),
			RoughnessTexture = _crateAssets.Roughness.Get(),
			OcclusionTexture = _crateAssets.Occlusion.Get()
		});
		var sharedMaterial = sharedMaterialResult.Expect("Failed to create the shared PBR material.");
		var variantMaterial = variantMaterialResult.Expect("Failed to create the variant PBR material.");
		var lightMarkerMaterial = _renderer.CreatePbrMaterial(new PbrMaterialParameters {
			BaseColorFactor = new(0.0f, 0.0f, 0.0f, 1.0f),
			EmissiveFactor = new(4.0f, 3.6f, 1.8f),
			MetallicFactor = 0.0f,
			RoughnessFactor = 1.0f
		}).Expect("Failed to create point light marker material.");

		var leftInstanceResult = _scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(-1.4f, 0.0f, 0.0f)
		);
		var rightInstanceResult = _scene.AddModelInstance(
			cubeModel,
			sharedMaterial,
			Matrix4x4.CreateTranslation(1.4f, 0.0f, 0.0f)
		);
		var centerInstanceResult = _scene.AddModelInstance(
			cubeModel,
			variantMaterial,
			Matrix4x4.CreateTranslation(0.0f, 0.0f, -2.0f)
		);
		var pointLightMarkerResult = _scene.AddModelInstance(
			cubeModel,
			lightMarkerMaterial,
			BuildTransform(new(2.0f, 1.8f, 1.8f), Quaternion.Identity, 0.18f)
		);
		_leftInstance = leftInstanceResult.Expect("Failed to create left model instance.");
		_rightInstance = rightInstanceResult.Expect("Failed to create right model instance.");
		_centerInstance = centerInstanceResult.Expect("Failed to create center model instance.");
		_pointLightMarker = pointLightMarkerResult.Expect("Failed to create point light marker instance.");
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

		var markerResult = _scene.SetModelInstanceTransform(
			_pointLightMarker,
			BuildTransform(pointPosition, Quaternion.Identity, 0.18f)
		);
		if (markerResult.IsErr) {
			return markerResult;
		}

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
		_crateAssets?.Dispose();
		_assetsRegistry?.Dispose();
		_scene?.Dispose();
		_renderer?.Dispose();
		_crateAssets = null;
		_assetsRegistry = null;
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
