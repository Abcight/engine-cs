using System.Runtime.InteropServices;

namespace Engine.Graphics.VertexInput;

public enum VertexElementType {
	Float32,
	Int32,
	UInt32
}

public readonly record struct VertexElementDescription(
	int Location,
	VertexElementType ElementType,
	int ComponentCount,
	int OffsetBytes,
	bool Normalized = false
);

public sealed class VertexLayoutDescription {
	public VertexLayoutDescription(
		int strideBytes,
		IReadOnlyList<VertexElementDescription> elements
	) {
		StrideBytes = strideBytes;
		Elements = elements;
	}

	public int StrideBytes { get; }

	public IReadOnlyList<VertexElementDescription> Elements { get; }

	public static VertexLayoutDescription Create<TVertex>(params VertexElementDescription[] elements)
		where TVertex : unmanaged {
		return new VertexLayoutDescription(Marshal.SizeOf<TVertex>(), elements);
	}
}
