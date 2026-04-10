using System.Numerics;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTkMatrix4 = OpenTK.Mathematics.Matrix4;

namespace Engine.Graphics.OpenGL;

public sealed class OpenGlGraphicsDevice : IGraphicsDevice, IOpenGlNativeAccess {
	private bool _disposed;

	public Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot begin a render pass on a disposed graphics device.");
		}

		try {
			if (!string.IsNullOrWhiteSpace(label)) {
				PushDebugGroup(label);
			}
			return new OpenGlRenderPassContext(this, label);
		} catch (Exception exception) {
			return GraphicsError.Unexpected($"Failed to begin render pass: {exception.Message}");
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
			var shader = new Shader<TBinding>(
				binding,
				(context, inner) => {
					if (context is not OpenGlRenderPassContext openGlContext || !ReferenceEquals(openGlContext.Owner, this)) {
						return GraphicsError.InvalidContext("Shader was bound on an incompatible render pass context.");
					}

					try {
						GL.UseProgram(program);
						inner.Upload(uploader);
						return Unit.Value;
					} catch (Exception exception) {
						return GraphicsError.BackendFailure($"Failed to bind shader: {exception.Message}");
					}
				},
				() => {
					try {
						if (GL.IsProgram(program)) {
							GL.DeleteProgram(program);
						}

						return Unit.Value;
					} catch (Exception exception) {
						return GraphicsError.BackendFailure($"Failed to dispose shader program: {exception.Message}");
					}
				}
			);

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

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	private static bool TryReadShaderSource(string shaderPath, out string source, out string error) {
		string resolvedPath = ResolveRuntimePath(shaderPath);

		if (!File.Exists(resolvedPath)) {
			source = string.Empty;
			error = $"Shader file '{shaderPath}' was not found. Resolved path: '{resolvedPath}'.";
			return false;
		}

		source = File.ReadAllText(resolvedPath);
		error = string.Empty;
		return true;
	}

	private static string ResolveRuntimePath(string path) {
		if (Path.IsPathRooted(path)) {
			return path;
		}

		string fromBase = Path.GetFullPath(path, AppContext.BaseDirectory);

		if (File.Exists(fromBase)) {
			return fromBase;
		}

		return Path.GetFullPath(path, Directory.GetCurrentDirectory());
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
