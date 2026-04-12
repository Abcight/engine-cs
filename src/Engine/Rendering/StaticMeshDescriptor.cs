using Engine.Graphics.Rendering;
using Engine.Graphics.VertexInput;

namespace Engine.Rendering;

public readonly record struct StaticMeshDescriptor<TVertex, TIndex>(
	ReadOnlyMemory<TVertex> Vertices,
	ReadOnlyMemory<TIndex> Indices,
	VertexLayoutDescription VertexLayout,
	PrimitiveTopology Topology = PrimitiveTopology.Triangles
)
	where TVertex : unmanaged
	where TIndex : unmanaged;