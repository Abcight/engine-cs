using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Graphics.VertexInput;

namespace Engine.Rendering;

public sealed class RenderScene : IDisposable {
	private readonly Dictionary<int, CameraState> _cameras = new();
	private readonly Dictionary<int, SceneModel> _models = new();
	private readonly Dictionary<int, ModelInstanceState> _instances = new();
	private readonly List<int> _instanceOrder = [];
	private readonly Dictionary<int, DirectionalLightDescription> _directionalLights = new();
	private readonly List<int> _directionalLightOrder = [];
	private readonly Dictionary<int, PointLightDescription> _pointLights = new();
	private readonly List<int> _pointLightOrder = [];

	private int _nextCameraId = 1;
	private int _nextModelId = 1;
	private int _nextModelInstanceId = 1;
	private int _nextDirectionalLightId = 1;
	private int _nextPointLightId = 1;
	private bool _disposed;

	public Result<CameraHandle, GraphicsError> AddPerspectiveCamera(PerspectiveCameraDescription description) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add camera to a disposed render scene.");
		}

		if (description.VerticalFieldOfViewRadians <= 0.0f || description.VerticalFieldOfViewRadians >= MathF.PI) {
			return GraphicsError.InvalidArgument("Perspective camera vertical FOV must be in range (0, PI).");
		}

		if (description.AspectRatio <= 0.0f) {
			return GraphicsError.InvalidArgument("Perspective camera aspect ratio must be greater than zero.");
		}

		if (description.NearPlane <= 0.0f || description.FarPlane <= description.NearPlane) {
			return GraphicsError.InvalidArgument(
				"Perspective camera planes are invalid. Near must be > 0 and far must be > near."
			);
		}

		Vector3 viewDirection = description.Target - description.Position;
		if (viewDirection.LengthSquared() <= 1e-8f) {
			return GraphicsError.InvalidArgument("Perspective camera position and target must not be identical.");
		}

		if (description.Up.LengthSquared() <= 1e-8f) {
			return GraphicsError.InvalidArgument("Perspective camera up vector must not be zero.");
		}

		Matrix4x4 view = Matrix4x4.CreateLookAt(description.Position, description.Target, Vector3.Normalize(description.Up));
		Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
			description.VerticalFieldOfViewRadians,
			description.AspectRatio,
			description.NearPlane,
			description.FarPlane
		);

		return AddCameraInternal(new CameraState(view, projection, description.Position));
	}

	public Result<CameraHandle, GraphicsError> AddOrthographicCamera(OrthographicCameraDescription description) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add camera to a disposed render scene.");
		}

		if (description.Width <= 0.0f || description.Height <= 0.0f) {
			return GraphicsError.InvalidArgument("Orthographic camera width and height must be greater than zero.");
		}

		if (description.NearPlane <= 0.0f || description.FarPlane <= description.NearPlane) {
			return GraphicsError.InvalidArgument(
				"Orthographic camera planes are invalid. Near must be > 0 and far must be > near."
			);
		}

		Vector3 viewDirection = description.Target - description.Position;
		if (viewDirection.LengthSquared() <= 1e-8f) {
			return GraphicsError.InvalidArgument("Orthographic camera position and target must not be identical.");
		}

		if (description.Up.LengthSquared() <= 1e-8f) {
			return GraphicsError.InvalidArgument("Orthographic camera up vector must not be zero.");
		}

		Matrix4x4 view = Matrix4x4.CreateLookAt(description.Position, description.Target, Vector3.Normalize(description.Up));
		Matrix4x4 projection = Matrix4x4.CreateOrthographic(
			description.Width,
			description.Height,
			description.NearPlane,
			description.FarPlane
		);

		return AddCameraInternal(new CameraState(view, projection, description.Position));
	}

	public Result<GraphicsError> SetCameraMatrices(CameraHandle handle, CameraMatrices camera) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot set camera data on a disposed render scene.");
		}

		if (!_cameras.ContainsKey(handle.Value)) {
			return GraphicsError.InvalidArgument($"Camera handle '{handle.Value}' does not exist.");
		}

		_cameras[handle.Value] = new CameraState(camera.View, camera.Projection, camera.WorldPosition);
		return Unit.Value;
	}

	public Result<GraphicsError> RemoveCamera(CameraHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove camera from a disposed render scene.");
		}

		_cameras.Remove(handle.Value);
		return Unit.Value;
	}

	public Result<ModelHandle, GraphicsError> AddModel<TVertex, TIndex>(
		VertexBuffer<TVertex> vertexBuffer,
		IndexBuffer<TIndex> indexBuffer,
		VertexLayoutDescription vertexLayout,
		PrimitiveTopology topology = PrimitiveTopology.Triangles,
		bool disposeWithScene = false
	)
		where TVertex : unmanaged
		where TIndex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add model to a disposed render scene.");
		}

		if (vertexBuffer is null) {
			return GraphicsError.InvalidArgument("Vertex buffer cannot be null.");
		}

		if (indexBuffer is null) {
			return GraphicsError.InvalidArgument("Index buffer cannot be null.");
		}

		if (vertexLayout is null) {
			return GraphicsError.InvalidArgument("Vertex layout cannot be null.");
		}

		Result<GraphicsError> layoutResult = ValidateSceneVertexLayout(vertexLayout);
		if (layoutResult.IsErr) {
			return layoutResult.Error;
		}

		int id = _nextModelId++;
		_models[id] = new SceneModel(
			new IndexedSceneGeometry<TVertex, TIndex>(vertexBuffer, indexBuffer, vertexLayout, topology),
			disposeWithScene
		);
		return new ModelHandle(id);
	}

	public Result<ModelHandle, GraphicsError> AddModel<TVertex>(
		VertexBuffer<TVertex> vertexBuffer,
		VertexLayoutDescription vertexLayout,
		PrimitiveTopology topology = PrimitiveTopology.Triangles,
		bool disposeWithScene = false
	)
		where TVertex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add model to a disposed render scene.");
		}

		if (vertexBuffer is null) {
			return GraphicsError.InvalidArgument("Vertex buffer cannot be null.");
		}

		if (vertexLayout is null) {
			return GraphicsError.InvalidArgument("Vertex layout cannot be null.");
		}

		Result<GraphicsError> layoutResult = ValidateSceneVertexLayout(vertexLayout);
		if (layoutResult.IsErr) {
			return layoutResult.Error;
		}

		int id = _nextModelId++;
		_models[id] = new SceneModel(
			new ArraySceneGeometry<TVertex>(vertexBuffer, vertexLayout, topology),
			disposeWithScene
		);
		return new ModelHandle(id);
	}

	public Result<GraphicsError> RemoveModel(ModelHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove model from a disposed render scene.");
		}

		if (_models.Remove(handle.Value, out SceneModel? model)) {
			model.DisposeOwnedResources();
		}

		List<int> instanceIds = [];
		foreach ((int instanceId, ModelInstanceState instance) in _instances) {
			if (instance.ModelHandle == handle) {
				instanceIds.Add(instanceId);
			}
		}

		foreach (int instanceId in instanceIds) {
			_instances.Remove(instanceId);
			_instanceOrder.Remove(instanceId);
		}

		return Unit.Value;
	}

	public Result<ModelInstanceHandle, GraphicsError> AddModelInstance(ModelHandle model, MaterialHandle material) {
		return AddModelInstance(model, material, Matrix4x4.Identity);
	}

	public Result<ModelInstanceHandle, GraphicsError> AddModelInstance(
		ModelHandle model,
		MaterialHandle material,
		Matrix4x4 worldTransform
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add model instance to a disposed render scene.");
		}

		if (!_models.ContainsKey(model.Value)) {
			return GraphicsError.InvalidArgument($"Model handle '{model.Value}' does not exist.");
		}

		if (!material.IsValid) {
			return GraphicsError.InvalidArgument("Material handle is invalid.");
		}

		int id = _nextModelInstanceId++;
		_instances[id] = new ModelInstanceState(model, material, worldTransform);
		_instanceOrder.Add(id);
		return new ModelInstanceHandle(id);
	}

	public Result<GraphicsError> SetModelInstanceTransform(ModelInstanceHandle handle, Matrix4x4 worldTransform) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot modify model instance on a disposed render scene.");
		}

		if (!_instances.TryGetValue(handle.Value, out ModelInstanceState instance)) {
			return GraphicsError.InvalidArgument($"Model instance handle '{handle.Value}' does not exist.");
		}

		_instances[handle.Value] = instance with { WorldTransform = worldTransform };
		return Unit.Value;
	}

	public Result<GraphicsError> SetModelInstanceMaterial(ModelInstanceHandle handle, MaterialHandle material) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot modify model instance on a disposed render scene.");
		}

		if (!_instances.TryGetValue(handle.Value, out ModelInstanceState instance)) {
			return GraphicsError.InvalidArgument($"Model instance handle '{handle.Value}' does not exist.");
		}

		if (!material.IsValid) {
			return GraphicsError.InvalidArgument("Material handle is invalid.");
		}

		_instances[handle.Value] = instance with { MaterialHandle = material };
		return Unit.Value;
	}

	public Result<GraphicsError> RemoveModelInstance(ModelInstanceHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove model instance from a disposed render scene.");
		}

		_instances.Remove(handle.Value);
		_instanceOrder.Remove(handle.Value);
		return Unit.Value;
	}

	public Result<DirectionalLightHandle, GraphicsError> AddDirectionalLight(DirectionalLightDescription light) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add light to a disposed render scene.");
		}

		Result<GraphicsError> validation = ValidateDirectionalLight(light);
		if (validation.IsErr) {
			return validation.Error;
		}

		int id = _nextDirectionalLightId++;
		_directionalLights[id] = light;
		_directionalLightOrder.Add(id);
		return new DirectionalLightHandle(id);
	}

	public Result<GraphicsError> SetDirectionalLight(DirectionalLightHandle handle, DirectionalLightDescription light) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot modify light on a disposed render scene.");
		}

		Result<GraphicsError> validation = ValidateDirectionalLight(light);
		if (validation.IsErr) {
			return validation;
		}

		if (!_directionalLights.ContainsKey(handle.Value)) {
			return GraphicsError.InvalidArgument($"Directional light handle '{handle.Value}' does not exist.");
		}

		_directionalLights[handle.Value] = light;
		return Unit.Value;
	}

	public Result<GraphicsError> RemoveDirectionalLight(DirectionalLightHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove light from a disposed render scene.");
		}

		_directionalLights.Remove(handle.Value);
		_directionalLightOrder.Remove(handle.Value);
		return Unit.Value;
	}

	public Result<PointLightHandle, GraphicsError> AddPointLight(PointLightDescription light) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot add light to a disposed render scene.");
		}

		Result<GraphicsError> validation = ValidatePointLight(light);
		if (validation.IsErr) {
			return validation.Error;
		}

		int id = _nextPointLightId++;
		_pointLights[id] = light;
		_pointLightOrder.Add(id);
		return new PointLightHandle(id);
	}

	public Result<GraphicsError> SetPointLight(PointLightHandle handle, PointLightDescription light) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot modify light on a disposed render scene.");
		}

		Result<GraphicsError> validation = ValidatePointLight(light);
		if (validation.IsErr) {
			return validation;
		}

		if (!_pointLights.ContainsKey(handle.Value)) {
			return GraphicsError.InvalidArgument($"Point light handle '{handle.Value}' does not exist.");
		}

		_pointLights[handle.Value] = light;
		return Unit.Value;
	}

	public Result<GraphicsError> RemovePointLight(PointLightHandle handle) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot remove light from a disposed render scene.");
		}

		_pointLights.Remove(handle.Value);
		_pointLightOrder.Remove(handle.Value);
		return Unit.Value;
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		foreach (SceneModel model in _models.Values) {
			model.DisposeOwnedResources();
		}

		_cameras.Clear();
		_models.Clear();
		_instances.Clear();
		_instanceOrder.Clear();
		_directionalLights.Clear();
		_directionalLightOrder.Clear();
		_pointLights.Clear();
		_pointLightOrder.Clear();
		_disposed = true;
	}

	internal bool TryGetCameraState(CameraHandle handle, out CameraState state) {
		return _cameras.TryGetValue(handle.Value, out state);
	}

	internal bool TryGetModelState(
		ModelHandle handle,
		[NotNullWhen(true)] out SceneModel? model
	) {
		return _models.TryGetValue(handle.Value, out model);
	}

	internal IEnumerable<ModelInstanceState> EnumerateModelInstances() {
		foreach (int id in _instanceOrder) {
			if (_instances.TryGetValue(id, out ModelInstanceState instance)) {
				yield return instance;
			}
		}
	}

	internal IEnumerable<DirectionalLightDescription> EnumerateDirectionalLights() {
		foreach (int id in _directionalLightOrder) {
			if (_directionalLights.TryGetValue(id, out DirectionalLightDescription light)) {
				yield return light;
			}
		}
	}

	internal IEnumerable<PointLightDescription> EnumeratePointLights() {
		foreach (int id in _pointLightOrder) {
			if (_pointLights.TryGetValue(id, out PointLightDescription light)) {
				yield return light;
			}
		}
	}

	private Result<CameraHandle, GraphicsError> AddCameraInternal(CameraState camera) {
		int id = _nextCameraId++;
		_cameras[id] = camera;
		return new CameraHandle(id);
	}

	private static Result<GraphicsError> ValidateDirectionalLight(DirectionalLightDescription light) {
		if (light.Direction.LengthSquared() <= 1e-8f) {
			return GraphicsError.InvalidArgument("Directional light direction must not be zero.");
		}

		if (light.Intensity < 0.0f) {
			return GraphicsError.InvalidArgument("Directional light intensity cannot be negative.");
		}

		return Unit.Value;
	}

	private static Result<GraphicsError> ValidatePointLight(PointLightDescription light) {
		if (light.Range <= 0.0f) {
			return GraphicsError.InvalidArgument("Point light range must be greater than zero.");
		}

		if (light.Intensity < 0.0f) {
			return GraphicsError.InvalidArgument("Point light intensity cannot be negative.");
		}

		return Unit.Value;
	}

	private static Result<GraphicsError> ValidateSceneVertexLayout(VertexLayoutDescription layout) {
		if (layout.StrideBytes <= 0) {
			return GraphicsError.InvalidArgument("Vertex layout stride must be greater than zero.");
		}

		if (!TryGetElement(layout, 0, out VertexElementDescription positionElement)) {
			return GraphicsError.InvalidArgument("Scene vertex contract requires a position element at location 0.");
		}

		if (!TryGetElement(layout, 1, out VertexElementDescription normalElement)) {
			return GraphicsError.InvalidArgument("Scene vertex contract requires a normal element at location 1.");
		}

		if (!TryGetElement(layout, 2, out VertexElementDescription texCoordElement)) {
			return GraphicsError.InvalidArgument("Scene vertex contract requires a texcoord element at location 2.");
		}

		if (positionElement.ElementType != VertexElementType.Float32 || positionElement.ComponentCount != 3) {
			return GraphicsError.InvalidArgument(
				"Scene vertex contract requires location 0 to be Float32 with 3 components (vec3 position)."
			);
		}

		if (normalElement.ElementType != VertexElementType.Float32 || normalElement.ComponentCount != 3) {
			return GraphicsError.InvalidArgument(
				"Scene vertex contract requires location 1 to be Float32 with 3 components (vec3 normal)."
			);
		}

		if (texCoordElement.ElementType != VertexElementType.Float32 || texCoordElement.ComponentCount != 2) {
			return GraphicsError.InvalidArgument(
				"Scene vertex contract requires location 2 to be Float32 with 2 components (vec2 texcoord)."
			);
		}

		return Unit.Value;
	}

	private static bool TryGetElement(
		VertexLayoutDescription layout,
		int location,
		out VertexElementDescription element
	) {
		foreach (VertexElementDescription current in layout.Elements) {
			if (current.Location == location) {
				element = current;
				return true;
			}
		}

		element = default;
		return false;
	}

	internal readonly record struct CameraState(
		Matrix4x4 View,
		Matrix4x4 Projection,
		Vector3 WorldPosition
	) {
		public CameraMatrices ToCameraMatrices() {
			return new CameraMatrices(View, Projection, WorldPosition);
		}
	}

	internal readonly record struct ModelInstanceState(
		ModelHandle ModelHandle,
		MaterialHandle MaterialHandle,
		Matrix4x4 WorldTransform
	);

	internal sealed class SceneModel {
		private readonly ISceneGeometry _geometry;
		private readonly bool _disposeWithScene;

		internal SceneModel(ISceneGeometry geometry, bool disposeWithScene) {
			_geometry = geometry;
			_disposeWithScene = disposeWithScene;
		}

		public Result<GraphicsError> Bind(IRenderPassContext pass) {
			return _geometry.Bind(pass);
		}

		public Result<GraphicsError> Draw(IRenderPassContext pass) {
			return _geometry.Draw(pass);
		}

		public void DisposeOwnedResources() {
			if (!_disposeWithScene) {
				return;
			}

			_geometry.DisposeResources();
		}
	}

	internal interface ISceneGeometry {
		Result<GraphicsError> Bind(IRenderPassContext pass);
		Result<GraphicsError> Draw(IRenderPassContext pass);
		void DisposeResources();
	}

	private sealed class IndexedSceneGeometry<TVertex, TIndex> : ISceneGeometry
		where TVertex : unmanaged
		where TIndex : unmanaged {

		private readonly VertexBuffer<TVertex> _vertexBuffer;
		private readonly IndexBuffer<TIndex> _indexBuffer;
		private readonly VertexLayoutDescription _layout;
		private readonly PrimitiveTopology _topology;

		public IndexedSceneGeometry(
			VertexBuffer<TVertex> vertexBuffer,
			IndexBuffer<TIndex> indexBuffer,
			VertexLayoutDescription layout,
			PrimitiveTopology topology
		) {
			_vertexBuffer = vertexBuffer;
			_indexBuffer = indexBuffer;
			_layout = layout;
			_topology = topology;
		}

		public Result<GraphicsError> Bind(IRenderPassContext pass) {
			Result<GraphicsError> bindVertexResult = pass.BindVertexBuffer(_vertexBuffer);
			if (bindVertexResult.IsErr) {
				return bindVertexResult;
			}

			Result<GraphicsError> bindIndexResult = pass.BindIndexBuffer(_indexBuffer);
			if (bindIndexResult.IsErr) {
				return bindIndexResult;
			}

			return pass.SetVertexLayout(_layout);
		}

		public Result<GraphicsError> Draw(IRenderPassContext pass) {
			return pass.DrawIndexed(_topology, _indexBuffer.IndexCount);
		}

		public void DisposeResources() {
			_ = _indexBuffer.DisposeChecked();
			_ = _vertexBuffer.DisposeChecked();
		}
	}

	private sealed class ArraySceneGeometry<TVertex> : ISceneGeometry
		where TVertex : unmanaged {

		private readonly VertexBuffer<TVertex> _vertexBuffer;
		private readonly VertexLayoutDescription _layout;
		private readonly PrimitiveTopology _topology;

		public ArraySceneGeometry(
			VertexBuffer<TVertex> vertexBuffer,
			VertexLayoutDescription layout,
			PrimitiveTopology topology
		) {
			_vertexBuffer = vertexBuffer;
			_layout = layout;
			_topology = topology;
		}

		public Result<GraphicsError> Bind(IRenderPassContext pass) {
			Result<GraphicsError> bindVertexResult = pass.BindVertexBuffer(_vertexBuffer);
			if (bindVertexResult.IsErr) {
				return bindVertexResult;
			}

			return pass.SetVertexLayout(_layout);
		}

		public Result<GraphicsError> Draw(IRenderPassContext pass) {
			return pass.DrawArrays(_topology, _vertexBuffer.VertexCount);
		}

		public void DisposeResources() {
			_ = _vertexBuffer.DisposeChecked();
		}
	}
}