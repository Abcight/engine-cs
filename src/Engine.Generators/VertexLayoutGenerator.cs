using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class VertexLayoutGenerator : IIncrementalGenerator {
	private const string VERTEX_LAYOUT_ATTRIBUTE_FULL_NAME = "Engine.Graphics.VertexInput.VertexLayoutAttribute";
	private const string VERTEX_ELEMENT_ATTRIBUTE_FULL_NAME = "Engine.Graphics.VertexInput.VertexElementAttribute";
	private const string STRUCT_LAYOUT_ATTRIBUTE_FULL_NAME = "System.Runtime.InteropServices.StructLayoutAttribute";

	private static readonly DiagnosticDescriptor PartialStructRequiredDiagnostic = new(
		id: "VERTEXGEN001",
		title: "Vertex layout type must be partial",
		messageFormat: "Struct '{0}' must be declared partial to receive a generated vertex layout.",
		category: "VertexLayoutGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor SequentialLayoutRequiredDiagnostic = new(
		id: "VERTEXGEN002",
		title: "Vertex layout type must have sequential struct layout",
		messageFormat: "Struct '{0}' must be annotated with [StructLayout(LayoutKind.Sequential)] to derive a vertex layout.",
		category: "VertexLayoutGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor UnsupportedFieldTypeDiagnostic = new(
		id: "VERTEXGEN003",
		title: "Unsupported vertex element field type",
		messageFormat: "Field '{0}' on struct '{1}' has unsupported type '{2}'. Supported types: float, int, uint, Vector2, Vector3, Vector4.",
		category: "VertexLayoutGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor NoElementsDiagnostic = new(
		id: "VERTEXGEN004",
		title: "No vertex elements declared",
		messageFormat: "Struct '{0}' has [VertexLayout] but no fields annotated with [VertexElement].",
		category: "VertexLayoutGenerator",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor MustBeStructDiagnostic = new(
		id: "VERTEXGEN005",
		title: "Vertex layout attribute must be on a struct",
		messageFormat: "Type '{0}' must be a struct to use [VertexLayout].",
		category: "VertexLayoutGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private record struct FieldTypeMapping(
		string ElementTypeName,
		int ComponentCount,
		int SizeInBytes
	);

	private static readonly ImmutableDictionary<string, FieldTypeMapping> FieldTypeMappings =
		new Dictionary<string, FieldTypeMapping>(StringComparer.Ordinal) {
			["System.Single"] = new("VertexElementType.Float32", 1, 4),
			["System.Numerics.Vector2"] = new("VertexElementType.Float32", 2, 8),
			["System.Numerics.Vector3"] = new("VertexElementType.Float32", 3, 12),
			["System.Numerics.Vector4"] = new("VertexElementType.Float32", 4, 16),
			["System.Int32"] = new("VertexElementType.Int32", 1, 4),
			["System.UInt32"] = new("VertexElementType.UInt32", 1, 4),
		}.ToImmutableDictionary(StringComparer.Ordinal);

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValuesProvider<VertexLayoutCandidate?> candidates = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) => IsCandidateNode(node),
			transform: static (generatorContext, _) => TryGetCandidate(generatorContext)
		);

		var pipeline = candidates
			.Where(static candidate => candidate.HasValue)
			.Select(static (candidate, _) => candidate.GetValueOrDefault());

		context.RegisterSourceOutput(pipeline, static (productionContext, candidate) =>
			GenerateVertexLayoutSource(productionContext, candidate)
		);
	}

	private static bool IsCandidateNode(SyntaxNode node) {
		if (node is not StructDeclarationSyntax structDeclaration) {
			return false;
		}

		return structDeclaration.AttributeLists.Count > 0;
	}

	private static VertexLayoutCandidate? TryGetCandidate(GeneratorSyntaxContext context) {
		var structDeclaration = (StructDeclarationSyntax)context.Node;
		if (context.SemanticModel.GetDeclaredSymbol(structDeclaration) is not INamedTypeSymbol typeSymbol) {
			return null;
		}

		bool hasVertexLayoutAttribute = typeSymbol.GetAttributes()
			.Any(static attribute =>
				string.Equals(attribute.AttributeClass?.ToDisplayString(), VERTEX_LAYOUT_ATTRIBUTE_FULL_NAME, StringComparison.Ordinal)
			);

		if (!hasVertexLayoutAttribute) {
			return null;
		}

		bool isPartial = structDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
		bool hasSequentialLayout = HasSequentialStructLayout(typeSymbol);

		var fields = ImmutableArray.CreateBuilder<VertexFieldInfo>();
		foreach (ISymbol member in typeSymbol.GetMembers()) {
			if (member is not IFieldSymbol fieldSymbol) {
				continue;
			}

			if (fieldSymbol.IsStatic || fieldSymbol.IsConst) {
				continue;
			}

			AttributeData? elementAttribute = fieldSymbol.GetAttributes()
				.FirstOrDefault(static attribute =>
					string.Equals(attribute.AttributeClass?.ToDisplayString(), VERTEX_ELEMENT_ATTRIBUTE_FULL_NAME, StringComparison.Ordinal)
				);

			int? location = null;
			if (elementAttribute is not null && elementAttribute.ConstructorArguments.Length >= 1) {
				if (elementAttribute.ConstructorArguments[0].Value is int loc) {
					location = loc;
				}
			}

			string fieldTypeName = fieldSymbol.Type.ToDisplayString();
			fields.Add(new VertexFieldInfo(fieldSymbol.Name, fieldTypeName, location));
		}

		return new VertexLayoutCandidate(
			typeSymbol,
			structDeclaration.GetLocation(),
			isPartial,
			hasSequentialLayout,
			fields.ToImmutable()
		);
	}

	private static bool HasSequentialStructLayout(INamedTypeSymbol typeSymbol) {
		foreach (AttributeData attribute in typeSymbol.GetAttributes()) {
			if (!string.Equals(attribute.AttributeClass?.ToDisplayString(), STRUCT_LAYOUT_ATTRIBUTE_FULL_NAME, StringComparison.Ordinal)) {
				continue;
			}

			if (attribute.ConstructorArguments.Length >= 1) {
				object? value = attribute.ConstructorArguments[0].Value;
				// LayoutKind.Sequential == 0
				if (value is int intValue && intValue == 0) {
					return true;
				}
			}

			return false;
		}

		// C# structs default to LayoutKind.Sequential when no attribute is specified.
		return true;
	}

	private static void GenerateVertexLayoutSource(
		SourceProductionContext context,
		VertexLayoutCandidate candidate
	) {
		if (candidate.Symbol.TypeKind != TypeKind.Struct) {
			context.ReportDiagnostic(Diagnostic.Create(
				MustBeStructDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		if (!candidate.IsPartial) {
			context.ReportDiagnostic(Diagnostic.Create(
				PartialStructRequiredDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		if (!candidate.HasSequentialLayout) {
			context.ReportDiagnostic(Diagnostic.Create(
				SequentialLayoutRequiredDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		// Compute byte offsets by accumulating field sizes in declaration order.
		var elements = new List<VertexElementModel>();
		int currentOffset = 0;

		foreach (VertexFieldInfo field in candidate.Fields) {
			if (!FieldTypeMappings.TryGetValue(field.TypeName, out FieldTypeMapping mapping)) {
				if (field.Location.HasValue) {
					context.ReportDiagnostic(Diagnostic.Create(
						UnsupportedFieldTypeDiagnostic,
						candidate.Location,
						field.Name,
						candidate.Symbol.Name,
						field.TypeName
					));
					return;
				}

				context.ReportDiagnostic(Diagnostic.Create(
					UnsupportedFieldTypeDiagnostic,
					candidate.Location,
					field.Name,
					candidate.Symbol.Name,
					field.TypeName
				));
				return;
			}

			if (field.Location.HasValue) {
				elements.Add(new VertexElementModel(
					field.Location.Value,
					mapping.ElementTypeName,
					mapping.ComponentCount,
					currentOffset
				));
			}

			currentOffset += mapping.SizeInBytes;
		}

		if (elements.Count == 0) {
			context.ReportDiagnostic(Diagnostic.Create(
				NoElementsDiagnostic,
				candidate.Location,
				candidate.Symbol.Name
			));
			return;
		}

		int strideBytes = currentOffset;
		string source = GenerateSource(candidate, elements, strideBytes);
		context.AddSource($"{candidate.Symbol.Name}.VertexLayout.g.cs", source);
	}

	private static string GenerateSource(
		VertexLayoutCandidate candidate,
		IReadOnlyList<VertexElementModel> elements,
		int strideBytes
	) {
		string namespaceName = candidate.Symbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: candidate.Symbol.ContainingNamespace.ToDisplayString();

		// Collect the chain of containing types (outermost first).
		var containingTypes = new List<INamedTypeSymbol>();
		INamedTypeSymbol? container = candidate.Symbol.ContainingType;
		while (container is not null) {
			containingTypes.Insert(0, container);
			container = container.ContainingType;
		}

		var source = new StringBuilder();
		source.AppendLine("// <auto-generated />");
		source.AppendLine("#nullable enable");
		if (!string.IsNullOrWhiteSpace(namespaceName)) {
			source.Append("namespace ").Append(namespaceName).AppendLine(";");
			source.AppendLine();
		}

		// Open containing types as partial wrappers.
		foreach (INamedTypeSymbol outer in containingTypes) {
			string keyword = outer.TypeKind == TypeKind.Struct ? "struct" : "class";
			source.Append("partial ").Append(keyword).Append(' ').Append(outer.Name).AppendLine(" {");
		}

		source.Append("partial struct ").Append(candidate.Symbol.Name).AppendLine(" {");
		source.AppendLine();

		source.Append("\tpublic static global::Engine.Graphics.VertexInput.VertexLayoutDescription Layout { get; } = new(");
		source.AppendLine();
		source.Append("\t\tstrideBytes: ").Append(strideBytes).AppendLine(",");
		source.AppendLine("\t\telements: new global::Engine.Graphics.VertexInput.VertexElementDescription[] {");

		foreach (VertexElementModel element in elements) {
			source.Append("\t\t\tnew(")
				.Append(element.Location)
				.Append(", global::Engine.Graphics.VertexInput.").Append(element.ElementTypeName)
				.Append(", ").Append(element.ComponentCount)
				.Append(", ").Append(element.OffsetBytes)
				.AppendLine("),");
		}

		source.AppendLine("\t\t}");
		source.AppendLine("\t);");
		source.AppendLine("}");

		// Close containing types.
		for (int i = 0; i < containingTypes.Count; i++) {
			source.AppendLine("}");
		}

		return source.ToString();
	}


	private readonly struct VertexLayoutCandidate {
		public VertexLayoutCandidate(
			INamedTypeSymbol symbol,
			Location location,
			bool isPartial,
			bool hasSequentialLayout,
			ImmutableArray<VertexFieldInfo> fields
		) {
			Symbol = symbol;
			Location = location;
			IsPartial = isPartial;
			HasSequentialLayout = hasSequentialLayout;
			Fields = fields;
		}

		public INamedTypeSymbol Symbol { get; }
		public Location Location { get; }
		public bool IsPartial { get; }
		public bool HasSequentialLayout { get; }
		public ImmutableArray<VertexFieldInfo> Fields { get; }
	}

	private readonly struct VertexFieldInfo {
		public VertexFieldInfo(string name, string typeName, int? location) {
			Name = name;
			TypeName = typeName;
			Location = location;
		}

		public string Name { get; }
		public string TypeName { get; }
		public int? Location { get; }
	}

	private readonly struct VertexElementModel {
		public VertexElementModel(int location, string elementTypeName, int componentCount, int offsetBytes) {
			Location = location;
			ElementTypeName = elementTypeName;
			ComponentCount = componentCount;
			OffsetBytes = offsetBytes;
		}

		public int Location { get; }
		public string ElementTypeName { get; }
		public int ComponentCount { get; }
		public int OffsetBytes { get; }
	}
}