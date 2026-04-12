using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ShaderBindingGenerator : IIncrementalGenerator {
	private const string SHADER_ATTRIBUTE_FULL_NAME = "Engine.Graphics.Shaders.ShaderAttribute";
	private const string ENGINE_UNIFORM_PREFIX = "_engine_";

	private static readonly DiagnosticDescriptor PartialClassRequiredDiagnostic = new(
		id: "SHADERGEN001",
		title: "Shader binding type must be partial",
		messageFormat: "Type '{0}' must be declared partial to receive generated shader bindings.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor ShaderFileMissingDiagnostic = new(
		id: "SHADERGEN002",
		title: "Shader file not found",
		messageFormat: "Shader file '{0}' declared on '{1}' could not be found at '{2}'.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor InvalidAttributeDiagnostic = new(
		id: "SHADERGEN003",
		title: "Invalid shader attribute",
		messageFormat: "'{0}' must declare exactly two non-empty string paths: vertex and fragment shader.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedUniformTypeDiagnostic = new(
		id: "SHADERGEN004",
		title: "Unsupported GLSL uniform type",
		messageFormat: "Uniform '{0}' uses unsupported GLSL type '{1}' and will be ignored by generated bindings.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor ConflictingUniformDiagnostic = new(
		id: "SHADERGEN005",
		title: "Conflicting uniform declarations",
		messageFormat: "Uniform '{0}' has conflicting declarations across shader stages.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedTypeShapeDiagnostic = new(
		id: "SHADERGEN006",
		title: "Unsupported shader binding type shape",
		messageFormat: "Type '{0}' must be a non-generic, non-nested class.",
		category: "ShaderGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly Regex UniformRegex = new(
		@"\buniform\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*\[\s*(?<size>\d+)\s*\])?\s*;",
		RegexOptions.Compiled | RegexOptions.CultureInvariant
	);

	private static readonly Regex IdentifierSplitRegex = new(
		@"[^A-Za-z0-9]+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant
	);

	private static readonly ImmutableDictionary<string, UniformTypeSpec> TypeMap =
		new Dictionary<string, UniformTypeSpec>(StringComparer.Ordinal) {
			["float"] = new("float", "ShaderUniformType.Float", "SetFloat"),
			["int"] = new("int", "ShaderUniformType.Int", "SetInt"),
			["uint"] = new("uint", "ShaderUniformType.UInt", "SetUInt"),
			["bool"] = new("bool", "ShaderUniformType.Bool", "SetBool"),
			["vec2"] = new("global::System.Numerics.Vector2", "ShaderUniformType.Vec2", "SetVector2"),
			["vec3"] = new("global::System.Numerics.Vector3", "ShaderUniformType.Vec3", "SetVector3"),
			["vec4"] = new("global::System.Numerics.Vector4", "ShaderUniformType.Vec4", "SetVector4"),
			["mat4"] = new("global::System.Numerics.Matrix4x4", "ShaderUniformType.Mat4", "SetMatrix4"),
			["sampler2D"] = new("int", "ShaderUniformType.Sampler2D", "SetSampler2D"),
			["samplerCube"] = new("int", "ShaderUniformType.SamplerCube", "SetSamplerCube")
		}.ToImmutableDictionary(StringComparer.Ordinal);

	private static readonly ImmutableArray<EngineUniformSpec> EngineUniformSpecs = [
		new("_engine_model", "Model", "ShaderUniformType.Mat4", isArray: false),
		new("_engine_view", "View", "ShaderUniformType.Mat4", isArray: false),
		new("_engine_projection", "Projection", "ShaderUniformType.Mat4", isArray: false),
		new("_engine_model_view_projection", "ModelViewProjection", "ShaderUniformType.Mat4", isArray: false),
		new("_engine_camera_world_position", "CameraWorldPosition", "ShaderUniformType.Vec3", isArray: false),
		new("_engine_directional_light_count", "DirectionalLightCount", "ShaderUniformType.Int", isArray: false),
		new("_engine_directional_light_directions", "DirectionalLightDirections", "ShaderUniformType.Vec4", isArray: true),
		new("_engine_directional_light_colors", "DirectionalLightColors", "ShaderUniformType.Vec4", isArray: true),
		new("_engine_point_light_count", "PointLightCount", "ShaderUniformType.Int", isArray: false),
		new("_engine_point_light_positions", "PointLightPositions", "ShaderUniformType.Vec4", isArray: true),
		new("_engine_point_light_colors", "PointLightColors", "ShaderUniformType.Vec4", isArray: true),
		new("_engine_point_light_ranges", "PointLightRanges", "ShaderUniformType.Float", isArray: true)
	];

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValuesProvider<ShaderTypeCandidate?> candidates = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) => IsCandidateNode(node),
			transform: static (generatorContext, _) => TryGetCandidate(generatorContext)
		);

		IncrementalValueProvider<string> projectDirectory = context.AnalyzerConfigOptionsProvider.Select(
			static (provider, _) => {
				provider.GlobalOptions.TryGetValue("build_property.ProjectDir", out string? projectDir);
				return projectDir ?? string.Empty;
			}
		);

		var pipeline =
			candidates
			.Where(static candidate => candidate.HasValue)
			.Select(static (candidate, _) => candidate.GetValueOrDefault())
			.Combine(projectDirectory);

		context.RegisterSourceOutput(pipeline, (productionContext, payload) =>
			GenerateBindingSource(productionContext, payload.Left, payload.Right)
		);
	}

	private static bool IsCandidateNode(SyntaxNode node) {
		if (node is not ClassDeclarationSyntax classDeclaration) {
			return false;
		}

		return classDeclaration.AttributeLists.Count > 0;
	}

	private static ShaderTypeCandidate? TryGetCandidate(GeneratorSyntaxContext context) {
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol) {
			return null;
		}

		AttributeData? shaderAttribute = typeSymbol.GetAttributes()
			.FirstOrDefault(static attribute =>
				string.Equals(attribute.AttributeClass?.ToDisplayString(), SHADER_ATTRIBUTE_FULL_NAME, StringComparison.Ordinal)
			);

		if (shaderAttribute is null) {
			return null;
		}

		if (shaderAttribute.ConstructorArguments.Length != 2) {
			return new ShaderTypeCandidate(
				typeSymbol,
				classDeclaration.GetLocation(),
				isPartial: classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword),
				vertexPath: null,
				fragmentPath: null
			);
		}

		string? vertexPath = shaderAttribute.ConstructorArguments[0].Value as string;
		string? fragmentPath = shaderAttribute.ConstructorArguments[1].Value as string;

		return new ShaderTypeCandidate(
			typeSymbol,
			classDeclaration.GetLocation(),
			isPartial: classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword),
			vertexPath,
			fragmentPath
		);
	}

	private static void GenerateBindingSource(
		SourceProductionContext context,
		ShaderTypeCandidate candidate,
		string projectDir
	) {
		if (!candidate.IsPartial) {
			context.ReportDiagnostic(Diagnostic.Create(
				PartialClassRequiredDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		if (candidate.Symbol.ContainingType is not null || candidate.Symbol.Arity > 0) {
			context.ReportDiagnostic(Diagnostic.Create(
				UnsupportedTypeShapeDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		if (string.IsNullOrWhiteSpace(candidate.VertexPath) || string.IsNullOrWhiteSpace(candidate.FragmentPath)) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidAttributeDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		string resolvedVertexPath = ResolvePath(projectDir, candidate.VertexPath!);
		if (!File.Exists(resolvedVertexPath)) {
			context.ReportDiagnostic(Diagnostic.Create(
				ShaderFileMissingDiagnostic,
				candidate.Location,
				candidate.VertexPath,
				candidate.Symbol.Name,
				resolvedVertexPath
			));
			return;
		}

		string resolvedFragmentPath = ResolvePath(projectDir, candidate.FragmentPath!);
		if (!File.Exists(resolvedFragmentPath)) {
			context.ReportDiagnostic(Diagnostic.Create(
				ShaderFileMissingDiagnostic,
				candidate.Location,
				candidate.FragmentPath,
				candidate.Symbol.Name,
				resolvedFragmentPath
			));
			return;
		}

		string vertexSource = File.ReadAllText(resolvedVertexPath);
		string fragmentSource = File.ReadAllText(resolvedFragmentPath);

		ImmutableArray<ParsedUniform> parsedUniforms = ParseUniforms(vertexSource, ParsedShaderStage.Vertex)
			.AddRange(ParseUniforms(fragmentSource, ParsedShaderStage.Fragment));

		if (!TryBuildUniformModels(context, candidate, parsedUniforms, out List<GeneratedUniformModel> generatedUniforms)) {
			return;
		}

		string source = GeneratePartialClassSource(candidate, generatedUniforms);
		context.AddSource($"{candidate.Symbol.Name}.ShaderBindings.g.cs", source);
	}

	private static bool TryBuildUniformModels(
		SourceProductionContext context,
		ShaderTypeCandidate candidate,
		ImmutableArray<ParsedUniform> parsedUniforms,
		out List<GeneratedUniformModel> generatedUniforms
	) {
		var mergedUniforms = new Dictionary<string, ParsedUniform>(StringComparer.Ordinal);
		foreach (ParsedUniform parsedUniform in parsedUniforms) {
			if (!mergedUniforms.TryGetValue(parsedUniform.Name, out ParsedUniform existing)) {
				mergedUniforms[parsedUniform.Name] = parsedUniform;
				continue;
			}

			if (!string.Equals(existing.TypeToken, parsedUniform.TypeToken, StringComparison.Ordinal)
				|| existing.ArrayLength != parsedUniform.ArrayLength) {
				context.ReportDiagnostic(Diagnostic.Create(
					ConflictingUniformDiagnostic,
					candidate.Location,
					parsedUniform.Name
				));
				generatedUniforms = new List<GeneratedUniformModel>();
				return false;
			}

			mergedUniforms[parsedUniform.Name] = new ParsedUniform(
				existing.Name,
				existing.TypeToken,
				existing.ArrayLength,
				existing.Stages | parsedUniform.Stages
			);
		}

		var usedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
		generatedUniforms = new List<GeneratedUniformModel>();
		foreach (KeyValuePair<string, ParsedUniform> pair in mergedUniforms.OrderBy(static p => p.Key, StringComparer.Ordinal)) {
			ParsedUniform uniform = pair.Value;
			if (!TypeMap.TryGetValue(uniform.TypeToken, out UniformTypeSpec typeSpec)) {
				context.ReportDiagnostic(Diagnostic.Create(
					UnsupportedUniformTypeDiagnostic,
					candidate.Location,
					uniform.Name,
					uniform.TypeToken
				));
				continue;
			}

			string propertyName = BuildUniquePropertyName(uniform.Name, usedPropertyNames);
			generatedUniforms.Add(new GeneratedUniformModel(uniform.Name, propertyName, uniform.ArrayLength, uniform.Stages, typeSpec));
		}

		return true;
	}

	private static ImmutableArray<ParsedUniform> ParseUniforms(string source, ParsedShaderStage stage) {
		string stripped = StripComments(source);
		var uniforms = ImmutableArray.CreateBuilder<ParsedUniform>();
		foreach (Match match in UniformRegex.Matches(stripped)) {
			if (!match.Success) {
				continue;
			}

			string typeToken = match.Groups["type"].Value;
			string name = match.Groups["name"].Value;
			int arrayLength = 1;
			Group sizeGroup = match.Groups["size"];
			if (sizeGroup.Success && int.TryParse(sizeGroup.Value, out int parsedSize) && parsedSize > 0) {
				arrayLength = parsedSize;
			}

			uniforms.Add(new ParsedUniform(name, typeToken, arrayLength, stage));
		}

		return uniforms.ToImmutable();
	}

	private static string StripComments(string source) {
		string withoutLineComments = Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
		return Regex.Replace(withoutLineComments, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
	}

	private static string GeneratePartialClassSource(ShaderTypeCandidate candidate, IReadOnlyList<GeneratedUniformModel> uniforms) {
		string namespaceName = candidate.Symbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: candidate.Symbol.ContainingNamespace.ToDisplayString();

		string bindingName = candidate.Symbol.Name;
		string parametersTypeName = $"{bindingName}ShaderParameters";
		List<GeneratedUniformModel> materialUniforms = uniforms
			.Where(static uniform => !IsEngineOwnedUniform(uniform.UniformName))
			.ToList();

		bool supportsEngineContract = SupportsEngineContract(uniforms, out Dictionary<string, GeneratedUniformModel> engineUniforms);

		var source = new StringBuilder();
		source.AppendLine("// <auto-generated />");
		source.AppendLine("#nullable enable");
		if (!string.IsNullOrWhiteSpace(namespaceName)) {
			source.Append("namespace ").Append(namespaceName).AppendLine(";");
			source.AppendLine();
		}

		source.Append("public partial class ").Append(bindingName)
			.AppendLine(" : global::Engine.Graphics.Shaders.IGeneratedShaderBinding, global::Engine.Graphics.Shaders.IEngineSceneShaderBinding {");
		source.AppendLine();

		foreach (GeneratedUniformModel uniform in uniforms) {
			if (uniform.ArrayLength > 1) {
				source.Append("\tpublic ").Append(uniform.Type.CSharpType).Append("[] ").Append(uniform.PropertyName)
					.Append(" { get; set; } = new ").Append(uniform.Type.CSharpType).Append('[')
					.Append(uniform.ArrayLength).AppendLine("];");
				source.AppendLine();
			} else {
				source.Append("\tpublic ").Append(uniform.Type.CSharpType).Append(' ').Append(uniform.PropertyName)
					.AppendLine(" { get; set; }");
				source.AppendLine();
			}
		}

		source.Append("\tpublic static ").Append(parametersTypeName).AppendLine(" NewParameters() => new();");
		source.AppendLine();

		source.AppendLine("\tprivate static readonly global::Engine.Graphics.Shaders.GeneratedShaderSchema __generatedSchema = new(");
		source.Append("\t\tvertexShaderPath: \"").Append(EscapeStringLiteral(candidate.VertexPath!)).AppendLine("\",");
		source.Append("\t\tfragmentShaderPath: \"").Append(EscapeStringLiteral(candidate.FragmentPath!)).AppendLine("\",");
		source.AppendLine("\t\tuniforms: new global::Engine.Graphics.Shaders.GeneratedShaderUniform[] {");
		foreach (GeneratedUniformModel uniform in uniforms) {
			source.Append("\t\t\tnew(\"").Append(EscapeStringLiteral(uniform.UniformName)).Append("\", ")
				.Append("global::Engine.Graphics.Shaders.").Append(uniform.Type.UniformEnumName)
				.Append(", ").Append(uniform.ArrayLength)
				.Append(", ").Append(ToShaderStageExpression(uniform.Stages))
				.AppendLine("),");
		}
		source.AppendLine("\t\t});");
		source.AppendLine();

		source.AppendLine("\tglobal::Engine.Graphics.Shaders.GeneratedShaderSchema global::Engine.Graphics.Shaders.IGeneratedShaderBinding.Schema => __generatedSchema;");
		source.AppendLine();

		source.AppendLine("\tvoid global::Engine.Graphics.Shaders.IGeneratedShaderBinding.Upload(global::Engine.Graphics.Shaders.IUniformUploader uploader) {");
		foreach (GeneratedUniformModel uniform in uniforms) {
			if (uniform.ArrayLength > 1) {
				source.Append("\t\tuploader.").Append(uniform.Type.UploaderMethodName).Append("Array(\"")
					.Append(EscapeStringLiteral(uniform.UniformName)).Append("\", ")
					.Append(uniform.PropertyName).AppendLine(");");
			} else {
				source.Append("\t\tuploader.").Append(uniform.Type.UploaderMethodName).Append("(\"")
					.Append(EscapeStringLiteral(uniform.UniformName)).Append("\", ")
					.Append(uniform.PropertyName).AppendLine(");");
			}
		}
		source.AppendLine("\t}");
		source.AppendLine();

		source.Append("\tbool global::Engine.Graphics.Shaders.IEngineSceneShaderBinding.SupportsEngineSceneContract => ")
			.Append(supportsEngineContract ? "true" : "false").AppendLine(";");
		source.AppendLine();

		source.AppendLine("\tvoid global::Engine.Graphics.Shaders.IEngineSceneShaderBinding.SetEngineSceneUniforms(");
		source.AppendLine("\t\tin global::Engine.Graphics.Shaders.EngineSceneUniformValues values");
		source.AppendLine("\t) {");
		foreach (EngineUniformSpec spec in EngineUniformSpecs) {
			if (!engineUniforms.TryGetValue(spec.UniformName, out GeneratedUniformModel uniform)) {
				continue;
			}

			if (spec.IsArray) {
				source.Append("\t\tCopyArray(values.").Append(spec.ValuePropertyName).Append(", ")
					.Append(uniform.PropertyName).AppendLine(");");
			} else {
				source.Append("\t\t").Append(uniform.PropertyName).Append(" = values.")
					.Append(spec.ValuePropertyName).AppendLine(";");
			}
		}
		source.AppendLine("\t}");
		source.AppendLine();

		source.AppendLine("\tprivate static void CopyArray<T>(T[] source, T[] destination) {");
		source.AppendLine("\t\tif (destination.Length == 0) {");
		source.AppendLine("\t\t\treturn;");
		source.AppendLine("\t\t}");
		source.AppendLine();
		source.AppendLine("\t\tglobal::System.Array.Clear(destination, 0, destination.Length);");
		source.AppendLine("\t\tint count = global::System.Math.Min(source.Length, destination.Length);");
		source.AppendLine("\t\tif (count > 0) {");
		source.AppendLine("\t\t\tglobal::System.Array.Copy(source, destination, count);");
		source.AppendLine("\t\t}");
		source.AppendLine("\t}");
		source.AppendLine("}");
		source.AppendLine();

		source.Append("public sealed class ").Append(parametersTypeName).Append(" : global::Engine.Graphics.Shaders.ShaderParameters<")
			.Append(bindingName).AppendLine("> {");
		source.AppendLine();

		foreach (GeneratedUniformModel uniform in materialUniforms) {
			if (uniform.ArrayLength > 1) {
				source.Append("\tpublic ").Append(uniform.Type.CSharpType).Append("[] ").Append(uniform.PropertyName)
					.Append(" { get; set; } = new ").Append(uniform.Type.CSharpType).Append('[')
					.Append(uniform.ArrayLength).AppendLine("];");
				source.AppendLine();
			} else {
				source.Append("\tpublic ").Append(uniform.Type.CSharpType).Append(' ').Append(uniform.PropertyName)
					.AppendLine(" { get; set; }");
				source.AppendLine();
			}
		}

		source.Append("\tpublic override void ApplyTo(").Append(bindingName).AppendLine(" binding) {");
		foreach (GeneratedUniformModel uniform in materialUniforms) {
			if (uniform.ArrayLength > 1) {
				source.Append("\t\tCopyArray(").Append(uniform.PropertyName).Append(", binding.")
					.Append(uniform.PropertyName).AppendLine(");");
			} else {
				source.Append("\t\tbinding.").Append(uniform.PropertyName).Append(" = ")
					.Append(uniform.PropertyName).AppendLine(";");
			}
		}
		source.AppendLine("\t}");
		source.AppendLine();

		source.Append("\tpublic override global::Engine.Graphics.Shaders.ShaderParameters<").Append(bindingName).AppendLine("> Clone() {");
		source.Append("\t\tvar copy = new ").Append(parametersTypeName).AppendLine("();");
		foreach (GeneratedUniformModel uniform in materialUniforms) {
			if (uniform.ArrayLength > 1) {
				source.Append("\t\tcopy.").Append(uniform.PropertyName).Append(" = (")
					.Append(uniform.Type.CSharpType).Append("[])").Append(uniform.PropertyName).AppendLine(".Clone();");
			} else {
				source.Append("\t\tcopy.").Append(uniform.PropertyName).Append(" = ")
					.Append(uniform.PropertyName).AppendLine(";");
			}
		}
		source.AppendLine("\t\treturn copy;");
		source.AppendLine("\t}");
		source.AppendLine();

		source.AppendLine("\tprivate static void CopyArray<T>(T[] source, T[] destination) {");
		source.AppendLine("\t\tif (destination.Length == 0) {");
		source.AppendLine("\t\t\treturn;");
		source.AppendLine("\t\t}");
		source.AppendLine();
		source.AppendLine("\t\tglobal::System.Array.Clear(destination, 0, destination.Length);");
		source.AppendLine("\t\tint count = global::System.Math.Min(source.Length, destination.Length);");
		source.AppendLine("\t\tif (count > 0) {");
		source.AppendLine("\t\t\tglobal::System.Array.Copy(source, destination, count);");
		source.AppendLine("\t\t}");
		source.AppendLine("\t}");
		source.AppendLine("}");

		return source.ToString();
	}

	private static bool SupportsEngineContract(
		IReadOnlyList<GeneratedUniformModel> uniforms,
		out Dictionary<string, GeneratedUniformModel> engineUniforms
	) {
		engineUniforms = new Dictionary<string, GeneratedUniformModel>(StringComparer.Ordinal);
		foreach (GeneratedUniformModel uniform in uniforms) {
			if (IsEngineOwnedUniform(uniform.UniformName)) {
				engineUniforms[uniform.UniformName] = uniform;
			}
		}

		foreach (EngineUniformSpec spec in EngineUniformSpecs) {
			if (!engineUniforms.TryGetValue(spec.UniformName, out GeneratedUniformModel uniform)) {
				return false;
			}

			if (!string.Equals(uniform.Type.UniformEnumName, spec.ExpectedUniformEnumName, StringComparison.Ordinal)) {
				return false;
			}

			if (spec.IsArray) {
				if (uniform.ArrayLength < 1) {
					return false;
				}
			} else if (uniform.ArrayLength != 1) {
				return false;
			}
		}

		return true;
	}

	private static bool IsEngineOwnedUniform(string uniformName) {
		return uniformName.StartsWith(ENGINE_UNIFORM_PREFIX, StringComparison.Ordinal);
	}

	private static string ToShaderStageExpression(ParsedShaderStage stage) {
		if (stage == ParsedShaderStage.None) {
			return "global::Engine.Graphics.Shaders.ShaderStage.None";
		}

		var stages = new List<string>();
		if (stage.HasFlag(ParsedShaderStage.Vertex)) {
			stages.Add("global::Engine.Graphics.Shaders.ShaderStage.Vertex");
		}

		if (stage.HasFlag(ParsedShaderStage.Fragment)) {
			stages.Add("global::Engine.Graphics.Shaders.ShaderStage.Fragment");
		}

		if (stage.HasFlag(ParsedShaderStage.Compute)) {
			stages.Add("global::Engine.Graphics.Shaders.ShaderStage.Compute");
		}

		return string.Join(" | ", stages);
	}

	private static string ResolvePath(string projectDir, string path) {
		if (Path.IsPathRooted(path)) {
			return path;
		}

		if (string.IsNullOrWhiteSpace(projectDir)) {
			return path;
		}

		return Path.GetFullPath(Path.Combine(projectDir, path));
	}

	private static string BuildUniquePropertyName(string uniformName, HashSet<string> usedNames) {
		string baseName = ToPropertyName(uniformName);
		string name = baseName;
		int suffix = 1;
		while (!usedNames.Add(name)) {
			suffix++;
			name = baseName + suffix;
		}

		return name;
	}

	private static string ToPropertyName(string uniformName) {
		string trimmed = uniformName.Trim('_');
		if (trimmed.Length == 0) {
			trimmed = "uniform";
		}

		string[] parts = IdentifierSplitRegex.Split(trimmed)
			.Where(static part => part.Length > 0)
			.ToArray();

		if (parts.Length == 0) {
			parts = ["uniform"];
		}

		var builder = new StringBuilder();
		foreach (string part in parts) {
			if (part.Length == 0) {
				continue;
			}

			string lowered = part.ToLowerInvariant();
			builder.Append(char.ToUpperInvariant(lowered[0]));
			if (lowered.Length > 1) {
				builder.Append(lowered.Substring(1));
			}
		}

		if (builder.Length == 0) {
			builder.Append("Uniform");
		}

		if (char.IsDigit(builder[0])) {
			builder.Insert(0, 'U');
		}

		string candidateName = builder.ToString();
		if (SyntaxFacts.GetKeywordKind(candidateName) != SyntaxKind.None) {
			candidateName += "Value";
		}

		return candidateName;
	}

	private static string EscapeStringLiteral(string value) {
		return value.Replace("\\", "\\\\")
			.Replace("\"", "\\\"");
	}

	private readonly struct ShaderTypeCandidate {

		public ShaderTypeCandidate(
			INamedTypeSymbol symbol,
			Location location,
			bool isPartial,
			string? vertexPath,
			string? fragmentPath
		) {
			Symbol = symbol;
			Location = location;
			IsPartial = isPartial;
			VertexPath = vertexPath;
			FragmentPath = fragmentPath;
		}

		public INamedTypeSymbol Symbol { get; }

		public Location Location { get; }

		public bool IsPartial { get; }

		public string? VertexPath { get; }

		public string? FragmentPath { get; }
	}

	private readonly struct ParsedUniform {

		public ParsedUniform(
			string name,
			string typeToken,
			int arrayLength,
			ParsedShaderStage stages
		) {
			Name = name;
			TypeToken = typeToken;
			ArrayLength = arrayLength;
			Stages = stages;
		}

		public string Name { get; }

		public string TypeToken { get; }

		public int ArrayLength { get; }

		public ParsedShaderStage Stages { get; }
	}

	private readonly struct GeneratedUniformModel {
		public GeneratedUniformModel(
			string uniformName,
			string propertyName,
			int arrayLength,
			ParsedShaderStage stages,
			UniformTypeSpec type
		) {
			UniformName = uniformName;
			PropertyName = propertyName;
			ArrayLength = arrayLength;
			Stages = stages;
			Type = type;
		}

		public string UniformName { get; }
		public string PropertyName { get; }
		public int ArrayLength { get; }
		public ParsedShaderStage Stages { get; }
		public UniformTypeSpec Type { get; }
	}

	private readonly struct UniformTypeSpec {
		public UniformTypeSpec(
			string cSharpType,
			string uniformEnumName,
			string uploaderMethodName
		) {
			CSharpType = cSharpType;
			UniformEnumName = uniformEnumName;
			UploaderMethodName = uploaderMethodName;
		}

		public string CSharpType { get; }
		public string UniformEnumName { get; }
		public string UploaderMethodName { get; }
	}

	private readonly struct EngineUniformSpec {
		public EngineUniformSpec(
			string uniformName,
			string valuePropertyName,
			string expectedUniformEnumName,
			bool isArray
		) {
			UniformName = uniformName;
			ValuePropertyName = valuePropertyName;
			ExpectedUniformEnumName = expectedUniformEnumName;
			IsArray = isArray;
		}

		public string UniformName { get; }
		public string ValuePropertyName { get; }
		public string ExpectedUniformEnumName { get; }
		public bool IsArray { get; }
	}

	[Flags]
	private enum ParsedShaderStage {
		None = 0,
		Vertex = 1,
		Fragment = 1 << 1,
		Compute = 1 << 2
	}
}