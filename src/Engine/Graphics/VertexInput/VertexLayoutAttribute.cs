namespace Engine.Graphics.VertexInput;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class VertexLayoutAttribute : Attribute;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class VertexElementAttribute : Attribute {
	public VertexElementAttribute(int location) {
		Location = location;
	}

	public int Location { get; }
}
