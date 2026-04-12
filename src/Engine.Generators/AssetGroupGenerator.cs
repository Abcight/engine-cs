using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AssetGroupGenerator : IIncrementalGenerator {
	private const string AssetGroupAttributeFullName = "Engine.Graphics.Assets.AssetGroupAttribute";
	private const string AssetAttributeFullName = "Engine.Graphics.Assets.AssetAttribute";
	private const string TextureHandleFullName = "Engine.Graphics.Assets.AssetHandle<Engine.Graphics.Resources.Texture2D>";

	private static readonly DiagnosticDescriptor PartialClassRequiredDiagnostic = new(
		id: "ASSETGEN001",
		title: "Asset group type must be partial",
		messageFormat: "Type '{0}' must be declared partial to receive generated asset group members.",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedTypeShapeDiagnostic = new(
		id: "ASSETGEN002",
		title: "Unsupported asset group type shape",
		messageFormat: "Type '{0}' must be a non-generic, non-nested class.",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor InvalidAssetAttributeDiagnostic = new(
		id: "ASSETGEN003",
		title: "Invalid asset attribute",
		messageFormat: "Member '{0}' must declare [Asset] with one non-empty string path.",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor AssetFileMissingDiagnostic = new(
		id: "ASSETGEN004",
		title: "Asset file not found",
		messageFormat: "Asset file '{0}' on member '{1}' could not be found at '{2}'.",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedMemberTypeDiagnostic = new(
		id: "ASSETGEN005",
		title: "Unsupported asset member type",
		messageFormat: "Member '{0}' must be of type 'AssetHandle<Texture2D>' in AssetGroup V1.",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedMemberShapeDiagnostic = new(
		id: "ASSETGEN006",
		title: "Unsupported asset member shape",
		messageFormat: "Member '{0}' must be writable (non-readonly field or property with a non-init setter).",
		category: "AssetGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValuesProvider<AssetGroupCandidate?> candidates = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) => IsCandidateNode(node),
			transform: static (generatorContext, _) => TryGetCandidate(generatorContext)
		);

		IncrementalValueProvider<string> projectDirectory = context.AnalyzerConfigOptionsProvider.Select(
			static (provider, _) => {
				provider.GlobalOptions.TryGetValue("build_property.ProjectDir", out string? projectDir);
				return projectDir ?? string.Empty;
			}
		);

		var pipeline = candidates
			.Where(static candidate => candidate.HasValue)
			.Select(static (candidate, _) => candidate.GetValueOrDefault())
			.Combine(projectDirectory);

		context.RegisterSourceOutput(pipeline, static (productionContext, payload) => {
			GenerateAssetGroupSource(productionContext, payload.Left, payload.Right);
		});
	}

	private static bool IsCandidateNode(SyntaxNode node) {
		if (node is not ClassDeclarationSyntax classDeclaration) {
			return false;
		}

		return classDeclaration.AttributeLists.Count > 0;
	}

	private static AssetGroupCandidate? TryGetCandidate(GeneratorSyntaxContext context) {
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol) {
			return null;
		}

		AttributeData? assetGroupAttribute = typeSymbol.GetAttributes()
			.FirstOrDefault(static attribute =>
				string.Equals(attribute.AttributeClass?.ToDisplayString(), AssetGroupAttributeFullName, StringComparison.Ordinal)
			);
		if (assetGroupAttribute is null) {
			return null;
		}

		return new AssetGroupCandidate(
			typeSymbol,
			classDeclaration.GetLocation(),
			classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
		);
	}

	private static void GenerateAssetGroupSource(
		SourceProductionContext context,
		AssetGroupCandidate candidate,
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

		if (!TryCollectMembers(context, candidate, projectDir, out ImmutableArray<AssetMemberModel> members)) {
			return;
		}

		bool shouldGenerateDefaultConstructor = !candidate.Symbol.InstanceConstructors.Any(static constructor =>
			constructor.Parameters.Length == 0 && !constructor.IsImplicitlyDeclared
		);
		string source = GenerateClassSource(candidate.Symbol, members, shouldGenerateDefaultConstructor);
		context.AddSource($"{candidate.Symbol.Name}.AssetGroup.g.cs", source);
	}

	private static bool TryCollectMembers(
		SourceProductionContext context,
		AssetGroupCandidate candidate,
		string projectDir,
		out ImmutableArray<AssetMemberModel> members
	) {
		var builder = ImmutableArray.CreateBuilder<AssetMemberModel>();
		bool hasErrors = false;

		foreach (ISymbol member in candidate.Symbol.GetMembers().OrderBy(static symbol => symbol.Name, StringComparer.Ordinal)) {
			AttributeData? assetAttribute = member.GetAttributes()
				.FirstOrDefault(static attribute =>
					string.Equals(attribute.AttributeClass?.ToDisplayString(), AssetAttributeFullName, StringComparison.Ordinal)
				);
			if (assetAttribute is null) {
				continue;
			}

			if (!TryValidateWritableMember(context, member)) {
				hasErrors = true;
				continue;
			}

			if (!TryValidateMemberType(context, member)) {
				hasErrors = true;
				continue;
			}

			if (!TryParseAssetPath(context, member, assetAttribute, out string assetPath)) {
				hasErrors = true;
				continue;
			}

			string resolvedAssetPath = ResolvePath(projectDir, assetPath);
			if (!File.Exists(resolvedAssetPath)) {
				context.ReportDiagnostic(Diagnostic.Create(
					AssetFileMissingDiagnostic,
					member.Locations.FirstOrDefault() ?? candidate.Location,
					assetPath,
					member.Name,
					resolvedAssetPath
				));
				hasErrors = true;
				continue;
			}

			TextureLoadOptionsModel options = ParseTextureLoadOptions(assetAttribute);
			string label = ParseLabel(assetAttribute, candidate.Symbol.Name, member.Name);
			builder.Add(new AssetMemberModel(member.Name, assetPath, label, options));
		}

		members = builder.ToImmutable();
		return !hasErrors;
	}

	private static bool TryValidateWritableMember(SourceProductionContext context, ISymbol member) {
		switch (member) {
			case IFieldSymbol field:
				if (field.IsReadOnly || field.IsConst) {
					context.ReportDiagnostic(Diagnostic.Create(
						UnsupportedMemberShapeDiagnostic,
						field.Locations.FirstOrDefault(),
						field.Name
					));
					return false;
				}
				return true;
			case IPropertySymbol property:
				if (property.SetMethod is null || property.SetMethod.IsInitOnly) {
					context.ReportDiagnostic(Diagnostic.Create(
						UnsupportedMemberShapeDiagnostic,
						property.Locations.FirstOrDefault(),
						property.Name
					));
					return false;
				}
				return true;
			default:
				context.ReportDiagnostic(Diagnostic.Create(
					UnsupportedMemberShapeDiagnostic,
					member.Locations.FirstOrDefault(),
					member.Name
				));
				return false;
		}
	}

	private static bool TryValidateMemberType(SourceProductionContext context, ISymbol member) {
		ITypeSymbol? type = member switch {
			IFieldSymbol field => field.Type,
			IPropertySymbol property => property.Type,
			_ => null
		};
		if (type is null) {
			context.ReportDiagnostic(Diagnostic.Create(
				UnsupportedMemberTypeDiagnostic,
				member.Locations.FirstOrDefault(),
				member.Name
			));
			return false;
		}

		if (!string.Equals(type.ToDisplayString(), TextureHandleFullName, StringComparison.Ordinal)) {
			context.ReportDiagnostic(Diagnostic.Create(
				UnsupportedMemberTypeDiagnostic,
				member.Locations.FirstOrDefault(),
				member.Name
			));
			return false;
		}

		return true;
	}

	private static bool TryParseAssetPath(
		SourceProductionContext context,
		ISymbol member,
		AttributeData attribute,
		out string path
	) {
		path = string.Empty;
		if (attribute.ConstructorArguments.Length != 1) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidAssetAttributeDiagnostic,
				member.Locations.FirstOrDefault(),
				member.Name
			));
			return false;
		}

		path = attribute.ConstructorArguments[0].Value as string ?? string.Empty;
		if (string.IsNullOrWhiteSpace(path)) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidAssetAttributeDiagnostic,
				member.Locations.FirstOrDefault(),
				member.Name
			));
			return false;
		}

		return true;
	}

	private static TextureLoadOptionsModel ParseTextureLoadOptions(AttributeData attribute) {
		bool generateMipmaps = true;
		bool flipVertically = false;
		TextureMinFilterModel minFilter = TextureMinFilterModel.LinearMipmapLinear;
		TextureMagFilterModel magFilter = TextureMagFilterModel.Linear;
		TextureWrapModel wrapU = TextureWrapModel.Repeat;
		TextureWrapModel wrapV = TextureWrapModel.Repeat;

		foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments) {
			switch (namedArgument.Key) {
				case "GenerateMipmaps":
					if (namedArgument.Value.Value is bool gm) {
						generateMipmaps = gm;
					}
					break;
				case "FlipVertically":
					if (namedArgument.Value.Value is bool fv) {
						flipVertically = fv;
					}
					break;
				case "MinFilter":
					if (namedArgument.Value.Value is int minFilterValue) {
						minFilter = MapTextureMinFilter(minFilterValue);
					}
					break;
				case "MagFilter":
					if (namedArgument.Value.Value is int magFilterValue) {
						magFilter = MapTextureMagFilter(magFilterValue);
					}
					break;
				case "WrapU":
					if (namedArgument.Value.Value is int wrapUValue) {
						wrapU = MapTextureWrap(wrapUValue);
					}
					break;
				case "WrapV":
					if (namedArgument.Value.Value is int wrapVValue) {
						wrapV = MapTextureWrap(wrapVValue);
					}
					break;
			}
		}

		return new TextureLoadOptionsModel(generateMipmaps, flipVertically, minFilter, magFilter, wrapU, wrapV);
	}

	private static string ParseLabel(AttributeData attribute, string typeName, string memberName) {
		foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments) {
			if (!string.Equals(namedArgument.Key, "Label", StringComparison.Ordinal)) {
				continue;
			}

			if (namedArgument.Value.Value is string label && !string.IsNullOrWhiteSpace(label)) {
				return label;
			}
		}

		return $"{typeName}.{memberName}";
	}

	private static TextureMinFilterModel MapTextureMinFilter(int value) {
		return value switch {
			0 => TextureMinFilterModel.Nearest,
			1 => TextureMinFilterModel.Linear,
			2 => TextureMinFilterModel.NearestMipmapNearest,
			3 => TextureMinFilterModel.LinearMipmapLinear,
			_ => TextureMinFilterModel.LinearMipmapLinear
		};
	}

	private static TextureMagFilterModel MapTextureMagFilter(int value) {
		return value switch {
			0 => TextureMagFilterModel.Nearest,
			1 => TextureMagFilterModel.Linear,
			_ => TextureMagFilterModel.Linear
		};
	}

	private static TextureWrapModel MapTextureWrap(int value) {
		return value switch {
			0 => TextureWrapModel.Repeat,
			1 => TextureWrapModel.ClampToEdge,
			2 => TextureWrapModel.MirroredRepeat,
			_ => TextureWrapModel.Repeat
		};
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

	private static string GenerateClassSource(
		INamedTypeSymbol symbol,
		ImmutableArray<AssetMemberModel> members,
		bool generateDefaultConstructor
	) {
		string namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: symbol.ContainingNamespace.ToDisplayString();
		string typeName = symbol.Name;

		var source = new StringBuilder();
		source.AppendLine("// <auto-generated />");
		source.AppendLine("#nullable enable");
		if (!string.IsNullOrWhiteSpace(namespaceName)) {
			source.Append("namespace ").Append(namespaceName).AppendLine(";");
			source.AppendLine();
		}

		source.Append("public partial class ").Append(typeName).AppendLine(" : global::System.IDisposable {");
		source.AppendLine();

		if (generateDefaultConstructor) {
			source.Append("\tpublic ").Append(typeName).AppendLine("() {");
			foreach (AssetMemberModel member in members) {
				source.Append("\t\t").Append(member.MemberName)
					.Append(" = global::Engine.Graphics.Assets.AssetHandle<global::Engine.Graphics.Resources.Texture2D>.Unbound;")
					.AppendLine();
			}
			source.AppendLine("\t}");
			source.AppendLine();
		}

		source.Append("\tpublic static ").Append(typeName)
			.AppendLine(" FromAssets(global::Engine.Graphics.Assets.Assets assets) {");
		source.Append("\t\tvar group = new ").Append(typeName).AppendLine("();");
		foreach (AssetMemberModel member in members) {
			source.Append("\t\tgroup.").Append(member.MemberName)
				.Append(" = FromAssets").Append(member.MemberName).AppendLine("(assets);");
		}
		source.AppendLine("\t\treturn group;");
		source.AppendLine("\t}");
		source.AppendLine();

		source.Append("\tpublic static global::Engine.Result<").Append(typeName)
			.AppendLine(", global::Engine.Graphics.Shaders.GraphicsError> TryFromAssets(global::Engine.Graphics.Assets.Assets assets) {");
		source.AppendLine("\t\tif (assets is null) {");
		source.AppendLine("\t\t\treturn global::Engine.Graphics.Shaders.GraphicsError.InvalidArgument(\"Assets registry cannot be null.\");");
		source.AppendLine("\t\t}");
		source.AppendLine();
		source.Append("\t\tvar group = new ").Append(typeName).AppendLine("();");
		foreach (AssetMemberModel member in members) {
			source.Append("\t\tvar ").Append(member.MemberName).Append("Result = TryFromAssets")
				.Append(member.MemberName).AppendLine("(assets);");
			source.Append("\t\tif (").Append(member.MemberName).Append("Result.TryErr() is { Error: var ")
				.Append(member.MemberName).AppendLine("Error }) {");
			source.AppendLine("\t\t\tgroup.Dispose();");
			source.Append("\t\t\treturn ").Append(member.MemberName).AppendLine("Error;");
			source.AppendLine("\t\t}");
			source.Append("\t\tif (").Append(member.MemberName).Append("Result.TryOk() is not { Value: var ")
				.Append(member.MemberName).AppendLine("Handle }) {");
			source.AppendLine("\t\t\tgroup.Dispose();");
			source.AppendLine("\t\t\treturn global::Engine.Graphics.Shaders.GraphicsError.Unexpected(");
			source.Append("\t\t\t\t\"Generated TryFromAssets member '").Append(member.MemberName)
				.AppendLine("' returned an invalid result state.\"");
			source.AppendLine("\t\t\t);");
			source.AppendLine("\t\t}");
			source.Append("\t\tgroup.").Append(member.MemberName).Append(" = ").Append(member.MemberName)
				.AppendLine("Handle;");
			source.AppendLine();
		}
		source.AppendLine("\t\treturn group;");
		source.AppendLine("\t}");
		source.AppendLine();

		foreach (AssetMemberModel member in members) {
			string optionsExpression = BuildOptionsExpression(member.Options);

			source.Append("\tpublic static global::Engine.Graphics.Assets.AssetHandle<global::Engine.Graphics.Resources.Texture2D> FromAssets")
				.Append(member.MemberName)
				.AppendLine("(global::Engine.Graphics.Assets.Assets assets) {");
			source.AppendLine("\t\tif (assets is null) {");
			source.AppendLine("\t\t\treturn global::Engine.Graphics.Assets.AssetHandle<global::Engine.Graphics.Resources.Texture2D>.Unbound;");
			source.AppendLine("\t\t}");
			source.Append("\t\tvar handle = assets.FromTexture2D(\"")
				.Append(EscapeStringLiteral(member.Path))
				.Append("\", ")
				.Append(optionsExpression)
				.Append(", \"")
				.Append(EscapeStringLiteral(member.Label))
				.AppendLine("\");");
			source.AppendLine("\t\t_ = handle.Get();");
			source.AppendLine("\t\treturn handle;");
			source.AppendLine("\t}");
			source.AppendLine();

			source.Append("\tpublic static global::Engine.Result<global::Engine.Graphics.Assets.AssetHandle<global::Engine.Graphics.Resources.Texture2D>, global::Engine.Graphics.Shaders.GraphicsError> TryFromAssets")
				.Append(member.MemberName)
				.AppendLine("(global::Engine.Graphics.Assets.Assets assets) {");
			source.AppendLine("\t\tif (assets is null) {");
			source.AppendLine("\t\t\treturn global::Engine.Graphics.Shaders.GraphicsError.InvalidArgument(\"Assets registry cannot be null.\");");
			source.AppendLine("\t\t}");
			source.AppendLine();
			source.Append("\t\tvar handleResult = assets.TryFromTexture2D(\"")
				.Append(EscapeStringLiteral(member.Path))
				.Append("\", ")
				.Append(optionsExpression)
				.Append(", \"")
				.Append(EscapeStringLiteral(member.Label))
				.AppendLine("\");");
			source.AppendLine("\t\tif (handleResult.TryErr() is { Error: var handleError }) {");
			source.AppendLine("\t\t\treturn handleError;");
			source.AppendLine("\t\t}");
			source.AppendLine("\t\tif (handleResult.TryOk() is not { Value: var handle }) {");
			source.AppendLine("\t\t\treturn global::Engine.Graphics.Shaders.GraphicsError.Unexpected(");
			source.Append("\t\t\t\t\"Generated TryFromAssets member '").Append(member.MemberName)
				.AppendLine("' returned an invalid handle result state.\"");
			source.AppendLine("\t\t\t);");
			source.AppendLine("\t\t}");
			source.AppendLine("\t\tvar warmResult = handle.TryGet();");
			source.AppendLine("\t\tif (warmResult.TryErr() is { Error: var warmError }) {");
			source.AppendLine("\t\t\thandle.Dispose();");
			source.AppendLine("\t\t\treturn warmError;");
			source.AppendLine("\t\t}");
			source.AppendLine("\t\tif (warmResult.TryOk() is not { Value: _ }) {");
			source.AppendLine("\t\t\thandle.Dispose();");
			source.AppendLine("\t\t\treturn global::Engine.Graphics.Shaders.GraphicsError.Unexpected(");
			source.Append("\t\t\t\t\"Generated TryFromAssets member '").Append(member.MemberName)
				.AppendLine("' returned an invalid warm result state.\"");
			source.AppendLine("\t\t\t);");
			source.AppendLine("\t\t}");
			source.AppendLine("\t\treturn handle;");
			source.AppendLine("\t}");
			source.AppendLine();
		}

		source.AppendLine("\tpublic void Dispose() {");
		foreach (AssetMemberModel member in members) {
			source.Append("\t\t").Append(member.MemberName).AppendLine(".Dispose();");
		}
		source.AppendLine("\t}");
		source.AppendLine("}");

		return source.ToString();
	}

	private static string BuildOptionsExpression(TextureLoadOptionsModel options) {
		return "new global::Engine.Graphics.Resources.Texture2DLoadOptions(" +
			$"GenerateMipmaps: {ToBoolLiteral(options.GenerateMipmaps)}, " +
			$"FlipVertically: {ToBoolLiteral(options.FlipVertically)}, " +
			$"MinFilter: {ToMinFilterLiteral(options.MinFilter)}, " +
			$"MagFilter: {ToMagFilterLiteral(options.MagFilter)}, " +
			$"WrapU: {ToWrapLiteral(options.WrapU)}, " +
			$"WrapV: {ToWrapLiteral(options.WrapV)})";
	}

	private static string ToBoolLiteral(bool value) => value ? "true" : "false";

	private static string ToMinFilterLiteral(TextureMinFilterModel value) {
		return value switch {
			TextureMinFilterModel.Nearest => "global::Engine.Graphics.Resources.TextureMinFilter.Nearest",
			TextureMinFilterModel.Linear => "global::Engine.Graphics.Resources.TextureMinFilter.Linear",
			TextureMinFilterModel.NearestMipmapNearest => "global::Engine.Graphics.Resources.TextureMinFilter.NearestMipmapNearest",
			TextureMinFilterModel.LinearMipmapLinear => "global::Engine.Graphics.Resources.TextureMinFilter.LinearMipmapLinear",
			_ => "global::Engine.Graphics.Resources.TextureMinFilter.LinearMipmapLinear"
		};
	}

	private static string ToMagFilterLiteral(TextureMagFilterModel value) {
		return value switch {
			TextureMagFilterModel.Nearest => "global::Engine.Graphics.Resources.TextureMagFilter.Nearest",
			TextureMagFilterModel.Linear => "global::Engine.Graphics.Resources.TextureMagFilter.Linear",
			_ => "global::Engine.Graphics.Resources.TextureMagFilter.Linear"
		};
	}

	private static string ToWrapLiteral(TextureWrapModel value) {
		return value switch {
			TextureWrapModel.Repeat => "global::Engine.Graphics.Resources.TextureWrap.Repeat",
			TextureWrapModel.ClampToEdge => "global::Engine.Graphics.Resources.TextureWrap.ClampToEdge",
			TextureWrapModel.MirroredRepeat => "global::Engine.Graphics.Resources.TextureWrap.MirroredRepeat",
			_ => "global::Engine.Graphics.Resources.TextureWrap.Repeat"
		};
	}

	private static string EscapeStringLiteral(string value) {
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private readonly struct AssetGroupCandidate {
		public AssetGroupCandidate(INamedTypeSymbol symbol, Location location, bool isPartial) {
			Symbol = symbol;
			Location = location;
			IsPartial = isPartial;
		}

		public INamedTypeSymbol Symbol { get; }
		public Location Location { get; }
		public bool IsPartial { get; }
	}

	private readonly struct AssetMemberModel {
		public AssetMemberModel(string memberName, string path, string label, TextureLoadOptionsModel options) {
			MemberName = memberName;
			Path = path;
			Label = label;
			Options = options;
		}

		public string MemberName { get; }
		public string Path { get; }
		public string Label { get; }
		public TextureLoadOptionsModel Options { get; }
	}

	private readonly struct TextureLoadOptionsModel {
		public TextureLoadOptionsModel(
			bool generateMipmaps,
			bool flipVertically,
			TextureMinFilterModel minFilter,
			TextureMagFilterModel magFilter,
			TextureWrapModel wrapU,
			TextureWrapModel wrapV
		) {
			GenerateMipmaps = generateMipmaps;
			FlipVertically = flipVertically;
			MinFilter = minFilter;
			MagFilter = magFilter;
			WrapU = wrapU;
			WrapV = wrapV;
		}

		public bool GenerateMipmaps { get; }
		public bool FlipVertically { get; }
		public TextureMinFilterModel MinFilter { get; }
		public TextureMagFilterModel MagFilter { get; }
		public TextureWrapModel WrapU { get; }
		public TextureWrapModel WrapV { get; }
	}

	private enum TextureMinFilterModel {
		Nearest,
		Linear,
		NearestMipmapNearest,
		LinearMipmapLinear
	}

	private enum TextureMagFilterModel {
		Nearest,
		Linear
	}

	private enum TextureWrapModel {
		Repeat,
		ClampToEdge,
		MirroredRepeat
	}
}