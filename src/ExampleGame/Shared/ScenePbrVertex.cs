using System.Numerics;
using Engine.Graphics.VertexInput;

namespace ExampleGame.Shared;

[VertexLayout]
internal readonly partial struct ScenePbrVertex {
	[VertexElement(0)] public readonly Vector3 Position;
	[VertexElement(1)] public readonly Vector3 Normal;
	[VertexElement(2)] public readonly Vector2 TexCoord;

	public ScenePbrVertex(
		float px,
		float py,
		float pz,
		float nx,
		float ny,
		float nz,
		float u,
		float v
	) {
		Position = new Vector3(px, py, pz);
		Normal = new Vector3(nx, ny, nz);
		TexCoord = new Vector2(u, v);
	}
}