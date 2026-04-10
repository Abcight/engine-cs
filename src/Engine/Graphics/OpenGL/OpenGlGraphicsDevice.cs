using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTkMatrix4 = OpenTK.Mathematics.Matrix4;
using GlTextureMagFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter;
using GlTextureMinFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter;

namespace Engine.Graphics.OpenGL;

public sealed class OpenGlGraphicsDevice : IGraphicsDevice, IOpenGlNativeAccess {
	private bool _disposed;
	private int _defaultVertexArray;

	public Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot begin a render pass on a disposed graphics device.");
		}

		try {
			var vaoResult = EnsureDefaultVertexArray();
			if (vaoResult.TryErr() is { Error: var error }) {
				return error;
			}

			GL.BindVertexArray(_defaultVertexArray);

			if (!string.IsNullOrWhiteSpace(label)) {
				PushDebugGroup(label);
			}

			return new OpenGlRenderPassContext(this, label);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Failed to begin render pass: {exception.Message}");
		}
	}

	public Result<VertexBuffer<TVertex>, GraphicsError> CreateVertexBuffer<TVertex>(
		ReadOnlySpan<TVertex> vertices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TVertex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create a vertex buffer on a disposed graphics device.");
		}

		int handle = 0;
		try {
			handle = GL.GenBuffer();
			if (handle == 0) {
				return GraphicsError.BackendFailure("OpenGL failed to allocate a vertex buffer.");
			}

			var usageHint = ToBufferUsageHint(usage);
			GL.BindBuffer(BufferTarget.ArrayBuffer, handle);
			UploadBufferData(BufferTarget.ArrayBuffer, vertices, usageHint);
			TrySetObjectLabel(ObjectLabelIdentifier.Buffer, handle, label);

			return new OpenGlVertexBuffer<TVertex>(
				this, handle, vertices.Length, Marshal.SizeOf<TVertex>(), usageHint
			);
		} catch (Exception exception) {
			try {
				if (handle != 0 && GL.IsBuffer(handle)) {
					GL.DeleteBuffer(handle);
				}
			} catch (Exception) {
			}

			return GraphicsError.Unexpected($"Unexpected vertex buffer creation failure: {exception.Message}");
		}
	}

	public Result<IndexBuffer<TIndex>, GraphicsError> CreateIndexBuffer<TIndex>(
		ReadOnlySpan<TIndex> indices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TIndex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create an index buffer on a disposed graphics device.");
		}

		if (!TryGetIndexElementType<TIndex>(out IndexElementType indexElementType, out int elementSizeInBytes)) {
			return GraphicsError.Unsupported(
				$"Index element type '{typeof(TIndex).Name}' is not supported. Use byte, ushort, or uint."
			);
		}

		int handle = 0;
		try {
			handle = GL.GenBuffer();
			if (handle == 0) {
				return GraphicsError.BackendFailure("OpenGL failed to allocate an index buffer.");
			}

			var usageHint = ToBufferUsageHint(usage);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, handle);
			UploadBufferData(BufferTarget.ElementArrayBuffer, indices, usageHint);
			TrySetObjectLabel(ObjectLabelIdentifier.Buffer, handle, label);

			return new OpenGlIndexBuffer<TIndex>(
				this, handle, indices.Length, elementSizeInBytes, indexElementType, usageHint
			);
		} catch (Exception exception) {
			try {
				if (handle != 0 && GL.IsBuffer(handle)) {
					GL.DeleteBuffer(handle);
				}
			} catch (Exception) {
			}

			return GraphicsError.Unexpected($"Unexpected index buffer creation failure: {exception.Message}");
		}
	}

	public Result<Texture2D, GraphicsError> CreateTexture2D(
		Texture2DDescriptor descriptor,
		ReadOnlySpan<byte> pixels,
		string? label = null
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot create a texture on a disposed graphics device.");
		}

		if (descriptor.Width <= 0 || descriptor.Height <= 0) {
			return GraphicsError.InvalidArgument("Texture dimensions must be greater than zero.");
		}

		if (!TryGetTextureFormatSpec(descriptor.Format, out TextureFormatSpec formatSpec)) {
			return GraphicsError.Unsupported($"Texture format '{descriptor.Format}' is not supported by the OpenGL backend.");
		}

		if (!TryGetExpectedPixelByteCount(descriptor, formatSpec, out int expectedByteCount)) {
			return GraphicsError.InvalidArgument("Texture dimensions are too large and overflowed byte size calculation.");
		}

		if (!pixels.IsEmpty && pixels.Length != expectedByteCount) {
			return GraphicsError.InvalidArgument(
				$"Texture upload byte count mismatch. Expected {expectedByteCount} bytes, got {pixels.Length}."
			);
		}

		int handle = 0;
		try {
			handle = GL.GenTexture();
			if (handle == 0) {
				return GraphicsError.BackendFailure("OpenGL failed to allocate a texture object.");
			}

			GL.BindTexture(TextureTarget.Texture2D, handle);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)ToGlMinFilter(descriptor.MinFilter));
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)ToGlMagFilter(descriptor.MagFilter));
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)ToGlWrap(descriptor.WrapU));
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)ToGlWrap(descriptor.WrapV));

			if (pixels.IsEmpty) {
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					formatSpec.InternalFormat,
					descriptor.Width,
					descriptor.Height,
					0,
					formatSpec.PixelFormat,
					formatSpec.PixelType,
					IntPtr.Zero
				);
			} else {
				byte[] initialPixels = pixels.ToArray();
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					formatSpec.InternalFormat,
					descriptor.Width,
					descriptor.Height,
					0,
					formatSpec.PixelFormat,
					formatSpec.PixelType,
					initialPixels
				);
			}

			if (descriptor.GenerateMipmaps) {
				GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
			}

			TrySetObjectLabel(ObjectLabelIdentifier.Texture, handle, label);

			return new OpenGlTexture2D(this, handle, descriptor, formatSpec, expectedByteCount);
		} catch (Exception exception) {
			try {
				if (handle != 0 && GL.IsTexture(handle)) {
					GL.DeleteTexture(handle);
				}
			} catch (Exception) {
			}

			return GraphicsError.Unexpected($"Unexpected texture creation failure: {exception.Message}");
		}
	}

	public Result<TBackend, GraphicsError> GetBackend<TBackend>()
		where TBackend : class {
		if (this is TBackend backend) {
			return backend;
		}

		return GraphicsError.InvalidContext($"Backend '{typeof(TBackend).Name}' is not available.");
	}

	public Result<ShaderLoadSuccess<TBinding>, ShaderLoadReport> LoadShader<TBinding>()
		where TBinding : class, IGeneratedShaderBinding, new() {
		var warnings = new List<string>();
		if (_disposed) {
			return ShaderLoadReport.Failed("Cannot load a shader on a disposed graphics device.", warnings);
		}

		int program = 0;
		try {
			TBinding binding = new();
			GeneratedShaderSchema schema = binding.Schema;

			if (!TryReadShaderSource(
				schema.VertexShaderPath,
				out string vertexSource,
				out string vertexError)
			) {
				return ShaderLoadReport.Failed(vertexError, warnings);
			}

			if (!TryReadShaderSource(
				schema.FragmentShaderPath,
				out string fragmentSource,
				out string fragmentError)
			) {
				return ShaderLoadReport.Failed(fragmentError, warnings);
			}

			if (!TryCompileShader(
				ShaderType.VertexShader,
				vertexSource,
				out int vertexShader,
				out string compileVertexError)
			) {
				return ShaderLoadReport.Failed(compileVertexError, warnings);
			}

			if (!TryCompileShader(
				ShaderType.FragmentShader,
				fragmentSource,
				out int fragmentShader,
				out string compileFragmentError)
			) {
				GL.DeleteShader(vertexShader);
				return ShaderLoadReport.Failed(compileFragmentError, warnings);
			}

			if (!TryLinkProgram(
				vertexShader,
				fragmentShader,
				out program,
				out string linkError)
			) {
				return ShaderLoadReport.Failed(linkError, warnings);
			}

			Dictionary<string, ReflectedUniform> reflectedUniforms = ReflectUniforms(program);
			if (!ValidateSchema(
				schema,
				reflectedUniforms,
				warnings,
				out string? validationError)
			) {
				GL.DeleteProgram(program);
				return ShaderLoadReport.Failed(validationError ?? "Shader reflection validation failed.", warnings);
			}

			var uploader = new OpenGlUniformUploader(program, reflectedUniforms);
			var shader = new OpenGlShader<TBinding>(this, program, uploader, binding);

			return new ShaderLoadSuccess<TBinding>(shader, warnings);
		} catch (Exception exception) {
			try {
				if (program != 0 && GL.IsProgram(program)) {
					GL.DeleteProgram(program);
				}
			} catch (Exception cleanupException) {
				warnings.Add($"Program cleanup failed after load error: {cleanupException.Message}");
			}

			return ShaderLoadReport.Failed($"Unexpected shader load failure: {exception.Message}", warnings);
		}
	}

	public void PushDebugGroup(string label) {
		if (string.IsNullOrWhiteSpace(label)) {
			return;
		}

		try {
			GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 0, label.Length, label);
		} catch (Exception) {
		}
	}

	public void PopDebugGroup() {
		try {
			GL.PopDebugGroup();
		} catch (Exception) {
		}
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		if (_defaultVertexArray != 0) {
			try {
				if (GL.IsVertexArray(_defaultVertexArray)) {
					GL.DeleteVertexArray(_defaultVertexArray);
				}
			} catch (Exception) {
			}
		}

		_disposed = true;
	}

	private Result<GraphicsError> EnsureDefaultVertexArray() {
		if (_defaultVertexArray != 0) {
			return Unit.Value;
		}

		try {
			_defaultVertexArray = GL.GenVertexArray();
			if (_defaultVertexArray == 0) {
				return GraphicsError.BackendFailure("OpenGL failed to allocate the default vertex array object.");
			}

			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to allocate default vertex array: {exception.Message}");
		}
	}

	internal static bool TryGetCompatibleContext(
		IRenderPassContext context,
		OpenGlGraphicsDevice owner,
		out OpenGlRenderPassContext? openGlContext,
		out GraphicsError error
	) {
		if (context is not OpenGlRenderPassContext typedContext || !ReferenceEquals(typedContext.Owner, owner)) {
			openGlContext = null;
			error = GraphicsError.InvalidContext("Resource was used with an incompatible render pass context.");
			return false;
		}

		if (typedContext.IsDisposed) {
			openGlContext = typedContext;
			error = GraphicsError.InvalidContext("Render pass context has already been disposed.");
			return false;
		}

		openGlContext = typedContext;
		error = GraphicsError.None;
		return true;
	}

	private static BufferUsageHint ToBufferUsageHint(BufferUsage usage) {
		return usage switch {
			BufferUsage.StaticDraw => BufferUsageHint.StaticDraw,
			BufferUsage.DynamicDraw => BufferUsageHint.DynamicDraw,
			BufferUsage.StreamDraw => BufferUsageHint.StreamDraw,
			_ => BufferUsageHint.StaticDraw
		};
	}

	private static GlTextureMinFilter ToGlMinFilter(Engine.Graphics.Resources.TextureMinFilter filter) {
		return filter switch {
			Engine.Graphics.Resources.TextureMinFilter.Nearest => GlTextureMinFilter.Nearest,
			Engine.Graphics.Resources.TextureMinFilter.Linear => GlTextureMinFilter.Linear,
			Engine.Graphics.Resources.TextureMinFilter.NearestMipmapNearest => GlTextureMinFilter.NearestMipmapNearest,
			Engine.Graphics.Resources.TextureMinFilter.LinearMipmapLinear => GlTextureMinFilter.LinearMipmapLinear,
			_ => GlTextureMinFilter.Linear
		};
	}

	private static GlTextureMagFilter ToGlMagFilter(Engine.Graphics.Resources.TextureMagFilter filter) {
		return filter switch {
			Engine.Graphics.Resources.TextureMagFilter.Nearest => GlTextureMagFilter.Nearest,
			Engine.Graphics.Resources.TextureMagFilter.Linear => GlTextureMagFilter.Linear,
			_ => GlTextureMagFilter.Linear
		};
	}

	private static TextureWrapMode ToGlWrap(TextureWrap wrap) {
		return wrap switch {
			TextureWrap.Repeat => TextureWrapMode.Repeat,
			TextureWrap.ClampToEdge => TextureWrapMode.ClampToEdge,
			TextureWrap.MirroredRepeat => TextureWrapMode.MirroredRepeat,
			_ => TextureWrapMode.Repeat
		};
	}

	internal static bool TryGetTextureFormatSpec(TextureFormat format, out TextureFormatSpec spec) {
		spec = format switch {
			TextureFormat.R8 => new TextureFormatSpec(PixelInternalFormat.R8, PixelFormat.Red, PixelType.UnsignedByte, 1),
			TextureFormat.RG8 => new TextureFormatSpec(PixelInternalFormat.Rg8, PixelFormat.Rg, PixelType.UnsignedByte, 2),
			TextureFormat.RGB8 => new TextureFormatSpec(PixelInternalFormat.Rgb8, PixelFormat.Rgb, PixelType.UnsignedByte, 3),
			TextureFormat.RGBA8 => new TextureFormatSpec(PixelInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, 4),
			_ => default
		};

		return spec.BytesPerPixel > 0;
	}

	private static bool TryGetExpectedPixelByteCount(
		Texture2DDescriptor descriptor,
		TextureFormatSpec formatSpec,
		out int byteCount
	) {
		try {
			byteCount = checked(descriptor.Width * descriptor.Height * formatSpec.BytesPerPixel);
			return true;
		} catch (OverflowException) {
			byteCount = 0;
			return false;
		}
	}

	private static bool TryGetIndexElementType<TIndex>(
		out IndexElementType elementType,
		out int elementSizeInBytes
	)
		where TIndex : unmanaged {
		var indexType = typeof(TIndex);

		if (indexType == typeof(byte)) {
			elementType = IndexElementType.UnsignedByte;
			elementSizeInBytes = 1;
			return true;
		}

		if (indexType == typeof(ushort)) {
			elementType = IndexElementType.UnsignedShort;
			elementSizeInBytes = 2;
			return true;
		}

		if (indexType == typeof(uint)) {
			elementType = IndexElementType.UnsignedInt;
			elementSizeInBytes = 4;
			return true;
		}

		elementType = default;
		elementSizeInBytes = 0;
		return false;
	}

	internal static void UploadBufferData<T>(
		BufferTarget target,
		ReadOnlySpan<T> data,
		BufferUsageHint usageHint
	)
		where T : unmanaged {
		int sizeInBytes = checked(data.Length * Marshal.SizeOf<T>());
		if (data.IsEmpty) {
			GL.BufferData(target, sizeInBytes, IntPtr.Zero, usageHint);
			return;
		}

		T[] managedData = data.ToArray();
		GL.BufferData(target, sizeInBytes, managedData, usageHint);
	}

	private static void TrySetObjectLabel(ObjectLabelIdentifier identifier, int handle, string? label) {
		if (string.IsNullOrWhiteSpace(label)) {
			return;
		}

		try {
			GL.ObjectLabel(identifier, handle, label.Length, label);
		} catch (Exception) {
		}
	}

	private static bool TryReadShaderSource(string shaderPath, out string source, out string error) {
		if (!TryResolveRuntimePath(shaderPath, out string resolvedPath, out string attemptedPaths)) {
			source = string.Empty;
			error = $"Shader file '{shaderPath}' was not found. Attempted paths:{Environment.NewLine}{attemptedPaths}";
			return false;
		}

		try {
			source = File.ReadAllText(resolvedPath);
			error = string.Empty;
			return true;
		} catch (Exception exception) {
			source = string.Empty;
			error = $"Failed to read shader file '{resolvedPath}': {exception.Message}";
			return false;
		}
	}

	private static bool TryResolveRuntimePath(string path, out string resolvedPath, out string attemptedPaths) {
		var candidates = new List<string>();

		void AddCandidate(string candidate) {
			if (string.IsNullOrWhiteSpace(candidate)) {
				return;
			}

			string fullPath;
			try {
				fullPath = Path.GetFullPath(candidate);
			} catch (Exception) {
				return;
			}

			if (!candidates.Contains(fullPath, StringComparer.Ordinal)) {
				candidates.Add(fullPath);
			}
		}

		if (Path.IsPathRooted(path)) {
			AddCandidate(path);
		}

		string baseDirectory = AppContext.BaseDirectory;
		string currentDirectory = Directory.GetCurrentDirectory();
		string? entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

		AddCandidate(Path.Combine(baseDirectory, path));
		AddCandidate(Path.Combine(currentDirectory, path));

		foreach (string directory in EnumerateSelfAndParents(baseDirectory, 8)) {
			AddCandidate(Path.Combine(directory, path));

			if (!string.IsNullOrWhiteSpace(entryAssemblyName)) {
				AddCandidate(Path.Combine(directory, "src", entryAssemblyName, path));
				AddCandidate(Path.Combine(directory, entryAssemblyName, path));
			}
		}

		foreach (string directory in EnumerateSelfAndParents(currentDirectory, 8)) {
			AddCandidate(Path.Combine(directory, path));

			if (!string.IsNullOrWhiteSpace(entryAssemblyName)) {
				AddCandidate(Path.Combine(directory, "src", entryAssemblyName, path));
				AddCandidate(Path.Combine(directory, entryAssemblyName, path));
			}
		}

		foreach (string candidate in candidates) {
			if (File.Exists(candidate)) {
				resolvedPath = candidate;
				attemptedPaths = string.Join(
					Environment.NewLine,
					candidates.Select(static candidatePath => $"- {candidatePath}")
				);
				return true;
			}
		}

		resolvedPath = string.Empty;
		attemptedPaths = string.Join(
			Environment.NewLine,
			candidates.Select(static candidatePath => $"- {candidatePath}")
		);
		return false;
	}

	private static IEnumerable<string> EnumerateSelfAndParents(string startPath, int maxDepth) {
		DirectoryInfo? directory;
		try {
			directory = new DirectoryInfo(startPath);
		} catch (Exception) {
			yield break;
		}

		int depth = 0;
		while (directory is not null && depth <= maxDepth) {
			yield return directory.FullName;
			directory = directory.Parent;
			depth++;
		}
	}

	private static bool TryCompileShader(
		ShaderType shaderType,
		string source,
		out int shader,
		out string error
	) {
		shader = GL.CreateShader(shaderType);

		GL.ShaderSource(shader, source);
		GL.CompileShader(shader);
		GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);

		if (status != (int)All.True) {
			error = GL.GetShaderInfoLog(shader);
			GL.DeleteShader(shader);
			shader = 0;
			return false;
		}

		error = string.Empty;
		return true;
	}

	private static bool TryLinkProgram(
		int vertexShader,
		int fragmentShader,
		out int program,
		out string error
	) {
		program = GL.CreateProgram();

		GL.AttachShader(program, vertexShader);
		GL.AttachShader(program, fragmentShader);
		GL.LinkProgram(program);
		GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);

		GL.DetachShader(program, vertexShader);
		GL.DetachShader(program, fragmentShader);
		GL.DeleteShader(vertexShader);
		GL.DeleteShader(fragmentShader);

		if (status != (int)All.True) {
			error = GL.GetProgramInfoLog(program);
			GL.DeleteProgram(program);
			program = 0;
			return false;
		}

		error = string.Empty;
		return true;
	}

	private static Dictionary<string, ReflectedUniform> ReflectUniforms(int program) {
		var uniforms = new Dictionary<string, ReflectedUniform>(StringComparer.Ordinal);

		GL.GetProgram(program, GetProgramParameterName.ActiveUniforms, out int activeUniformCount);
		for (int i = 0; i < activeUniformCount; i++) {
			string rawName = GL.GetActiveUniform(program, i, out int size, out ActiveUniformType type);
			string normalizedName = NormalizeUniformName(rawName);
			uniforms[normalizedName] = new ReflectedUniform(type, size);
		}

		return uniforms;
	}

	private static string NormalizeUniformName(string name) {
		const string ArraySuffix = "[0]";

		if (name.EndsWith(ArraySuffix, StringComparison.Ordinal)) {
			return name[..^ArraySuffix.Length];
		}

		return name;
	}

	private static bool ValidateSchema(
		GeneratedShaderSchema schema,
		IReadOnlyDictionary<string, ReflectedUniform> reflectedUniforms,
		List<string> warnings,
		out string? error
	) {
		foreach (GeneratedShaderUniform expected in schema.Uniforms) {
			if (!reflectedUniforms.TryGetValue(expected.Name, out ReflectedUniform reflected)) {
				warnings.Add($"Expected uniform '{expected.Name}' was not active in the linked program.");
				continue;
			}

			if (!IsUniformTypeCompatible(expected.Type, reflected.Type)) {
				error = $"Uniform '{expected.Name}' has incompatible type. Expected {expected.Type}, reflected {reflected.Type}.";
				return false;
			}

			int expectedArrayLength = Math.Max(1, expected.ArrayLength);
			if (expectedArrayLength != reflected.ArrayLength) {
				error = $"Uniform '{expected.Name}' has incompatible array length. Expected {expectedArrayLength}, reflected {reflected.ArrayLength}.";
				return false;
			}
		}

		foreach ((string name, ReflectedUniform _) in reflectedUniforms) {
			bool existsInSchema = false;
			foreach (GeneratedShaderUniform schemaUniform in schema.Uniforms) {
				if (string.Equals(schemaUniform.Name, name, StringComparison.Ordinal)) {
					existsInSchema = true;
					break;
				}
			}

			if (!existsInSchema) {
				warnings.Add($"Linked shader exposed extra uniform '{name}' that is not present in generated bindings.");
			}
		}

		error = null;
		return true;
	}

	private static bool IsUniformTypeCompatible(ShaderUniformType expected, ActiveUniformType actual) {
		return expected switch {
			ShaderUniformType.Float => actual == ActiveUniformType.Float,
			ShaderUniformType.Int => actual == ActiveUniformType.Int,
			ShaderUniformType.UInt => actual == ActiveUniformType.UnsignedInt,
			ShaderUniformType.Bool => actual == ActiveUniformType.Bool,
			ShaderUniformType.Vec2 => actual == ActiveUniformType.FloatVec2,
			ShaderUniformType.Vec3 => actual == ActiveUniformType.FloatVec3,
			ShaderUniformType.Vec4 => actual == ActiveUniformType.FloatVec4,
			ShaderUniformType.Mat4 => actual == ActiveUniformType.FloatMat4,
			ShaderUniformType.Sampler2D => actual == ActiveUniformType.Sampler2D,
			ShaderUniformType.SamplerCube => actual == ActiveUniformType.SamplerCube,
			_ => false
		};
	}

	private readonly record struct ReflectedUniform(ActiveUniformType Type, int ArrayLength);

	internal readonly record struct TextureFormatSpec(
		PixelInternalFormat InternalFormat,
		PixelFormat PixelFormat,
		PixelType PixelType,
		int BytesPerPixel
	);

	private sealed class OpenGlUniformUploader : IUniformUploader {
		private readonly int _program;
		private readonly Dictionary<string, int> _locations;

		public OpenGlUniformUploader(int program, IReadOnlyDictionary<string, ReflectedUniform> uniforms) {
			_program = program;
			_locations = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach ((string name, ReflectedUniform _) in uniforms) {
				_locations[name] = GL.GetUniformLocation(program, name);
			}
		}

		public void SetFloat(string name, float value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform1(location, value);
			}
		}

		public void SetFloatArray(string name, ReadOnlySpan<float> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform1(location, values[i]);
				}
			}
		}

		public void SetInt(string name, int value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform1(location, value);
			}
		}

		public void SetIntArray(string name, ReadOnlySpan<int> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform1(location, values[i]);
				}
			}
		}

		public void SetUInt(string name, uint value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform1(location, value);
			}
		}

		public void SetUIntArray(string name, ReadOnlySpan<uint> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform1(location, values[i]);
				}
			}
		}

		public void SetBool(string name, bool value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform1(location, value ? 1 : 0);
			}
		}

		public void SetBoolArray(string name, ReadOnlySpan<bool> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform1(location, values[i] ? 1 : 0);
				}
			}
		}

		public void SetVector2(string name, Vector2 value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform2(location, value.X, value.Y);
			}
		}

		public void SetVector2Array(string name, ReadOnlySpan<Vector2> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform2(location, values[i].X, values[i].Y);
				}
			}
		}

		public void SetVector3(string name, Vector3 value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform3(location, value.X, value.Y, value.Z);
			}
		}

		public void SetVector3Array(string name, ReadOnlySpan<Vector3> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform3(location, values[i].X, values[i].Y, values[i].Z);
				}
			}
		}

		public void SetVector4(string name, Vector4 value) {
			if (TryGetLocation(name, out int location)) {
				GL.Uniform4(location, value.X, value.Y, value.Z, value.W);
			}
		}

		public void SetVector4Array(string name, ReadOnlySpan<Vector4> values) {
			for (int i = 0; i < values.Length; i++) {
				if (TryGetArrayElementLocation(name, i, out int location)) {
					GL.Uniform4(location, values[i].X, values[i].Y, values[i].Z, values[i].W);
				}
			}
		}

		public void SetMatrix4(string name, Matrix4x4 value) {
			if (!TryGetLocation(name, out int location)) {
				return;
			}

			OpenTkMatrix4 matrix = ToOpenTkMatrix(value);
			GL.UniformMatrix4(location, false, ref matrix);
		}

		public void SetMatrix4Array(string name, ReadOnlySpan<Matrix4x4> values) {
			for (int i = 0; i < values.Length; i++) {
				if (!TryGetArrayElementLocation(name, i, out int location)) {
					continue;
				}

				OpenTkMatrix4 matrix = ToOpenTkMatrix(values[i]);
				GL.UniformMatrix4(location, false, ref matrix);
			}
		}

		public void SetSampler2D(string name, int textureUnit) {
			SetInt(name, textureUnit);
		}

		public void SetSampler2DArray(string name, ReadOnlySpan<int> textureUnits) {
			SetIntArray(name, textureUnits);
		}

		public void SetSamplerCube(string name, int textureUnit) {
			SetInt(name, textureUnit);
		}

		public void SetSamplerCubeArray(string name, ReadOnlySpan<int> textureUnits) {
			SetIntArray(name, textureUnits);
		}

		private bool TryGetLocation(string name, out int location) {
			if (_locations.TryGetValue(name, out location)) {
				return location >= 0;
			}

			location = GL.GetUniformLocation(_program, name);
			_locations[name] = location;
			return location >= 0;
		}

		private bool TryGetArrayElementLocation(string name, int index, out int location) {
			string elementName = $"{name}[{index}]";
			if (_locations.TryGetValue(elementName, out location)) {
				return location >= 0;
			}

			location = GL.GetUniformLocation(_program, elementName);
			_locations[elementName] = location;
			return location >= 0;
		}

		private static OpenTkMatrix4 ToOpenTkMatrix(Matrix4x4 value) {
			return new OpenTkMatrix4(
				value.M11, value.M12, value.M13, value.M14,
				value.M21, value.M22, value.M23, value.M24,
				value.M31, value.M32, value.M33, value.M34,
				value.M41, value.M42, value.M43, value.M44
			);
		}
	}
}
