using System.Numerics;
using Engine.Graphics.VertexInput;

namespace ExampleGame.Shared;

[VertexLayout]
internal readonly partial struct PositionColorVertex {
	[VertexElement(0)] public readonly Vector3 Position;
	[VertexElement(1)] public readonly Vector3 Color;

	public PositionColorVertex(float x, float y, float z, float r, float g, float b) {
		Position = new Vector3(x, y, z);
		Color = new Vector3(r, g, b);
	}
}