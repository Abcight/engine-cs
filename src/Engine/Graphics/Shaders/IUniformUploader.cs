using System.Numerics;

namespace Engine.Graphics.Shaders;

public interface IUniformUploader {
	void SetFloat(string name, float value);
	void SetFloatArray(string name, ReadOnlySpan<float> values);
	void SetInt(string name, int value);
	void SetIntArray(string name, ReadOnlySpan<int> values);
	void SetUInt(string name, uint value);
	void SetUIntArray(string name, ReadOnlySpan<uint> values);
	void SetBool(string name, bool value);
	void SetBoolArray(string name, ReadOnlySpan<bool> values);
	void SetVector2(string name, Vector2 value);
	void SetVector2Array(string name, ReadOnlySpan<Vector2> values);
	void SetVector3(string name, Vector3 value);
	void SetVector3Array(string name, ReadOnlySpan<Vector3> values);
	void SetVector4(string name, Vector4 value);
	void SetVector4Array(string name, ReadOnlySpan<Vector4> values);
	void SetMatrix4(string name, Matrix4x4 value);
	void SetMatrix4Array(string name, ReadOnlySpan<Matrix4x4> values);
	void SetSampler2D(string name, int textureUnit);
	void SetSampler2DArray(string name, ReadOnlySpan<int> textureUnits);
	void SetSamplerCube(string name, int textureUnit);
	void SetSamplerCubeArray(string name, ReadOnlySpan<int> textureUnits);
}
