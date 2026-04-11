using System.Numerics;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics.Shaders;
using Engine.Graphics.VertexInput;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.OpenGL;

public sealed class OpenGlRenderPassContext : IRenderPassContext {
	private bool _disposed;
	private IndexElementType? _boundIndexElementType;
	private int _boundIndexElementSize;

	internal OpenGlRenderPassContext(OpenGlGraphicsDevice device, string? label) {
		Device = device;
		Label = label;
	}

	public IGraphicsDevice Device { get; }

	public string? Label { get; }

	internal OpenGlGraphicsDevice Owner => (OpenGlGraphicsDevice)Device;

	internal bool IsDisposed => _disposed;

	public Result<GraphicsError> BindShader<TBinding>(Shader<TBinding> shader)
		where TBinding : class, IGeneratedShaderBinding {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind shader on a disposed render pass context.");
		}

		if (shader is null) {
			return GraphicsError.InvalidArgument("Shader cannot be null.");
		}

		return shader.Bind(this);
	}

	public Result<GraphicsError> BindVertexBuffer<TVertex>(VertexBuffer<TVertex> buffer)
		where TVertex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind vertex buffer on a disposed render pass context.");
		}

		if (buffer is null) {
			return GraphicsError.InvalidArgument("Vertex buffer cannot be null.");
		}

		return buffer.Bind(this);
	}

	public Result<GraphicsError> BindIndexBuffer<TIndex>(IndexBuffer<TIndex> buffer)
		where TIndex : unmanaged {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind index buffer on a disposed render pass context.");
		}

		if (buffer is null) {
			return GraphicsError.InvalidArgument("Index buffer cannot be null.");
		}

		Result<GraphicsError> bindResult = buffer.Bind(this);
		if (bindResult.IsErr) {
			return bindResult;
		}

		_boundIndexElementType = buffer.ElementType;
		_boundIndexElementSize = buffer.ElementSizeInBytes;
		return Unit.Value;
	}

	public Result<GraphicsError> BindTexture2D(Texture2D texture, int textureUnit = 0) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot bind texture on a disposed render pass context.");
		}

		if (texture is null) {
			return GraphicsError.InvalidArgument("Texture cannot be null.");
		}

		return texture.Bind(this, textureUnit);
	}

	public Result<GraphicsError> SetVertexLayout(VertexLayoutDescription layout) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot set vertex layout on a disposed render pass context.");
		}

		if (layout is null) {
			return GraphicsError.InvalidArgument("Vertex layout cannot be null.");
		}

		if (layout.StrideBytes <= 0) {
			return GraphicsError.InvalidArgument("Vertex layout stride must be greater than zero.");
		}

		if (layout.Elements.Count == 0) {
			return GraphicsError.InvalidArgument("Vertex layout must contain at least one element.");
		}

		try {
			foreach (VertexElementDescription element in layout.Elements) {
				if (element.Location < 0) {
					return GraphicsError.InvalidArgument("Vertex attribute location cannot be negative.");
				}

				if (element.ComponentCount < 1 || element.ComponentCount > 4) {
					return GraphicsError.InvalidArgument("Vertex attribute component count must be in range [1, 4].");
				}

				if (element.OffsetBytes < 0) {
					return GraphicsError.InvalidArgument("Vertex attribute offset cannot be negative.");
				}

				GL.EnableVertexAttribArray(element.Location);

				switch (element.ElementType) {
					case VertexElementType.Float32:
						GL.VertexAttribPointer(
							element.Location,
							element.ComponentCount,
							VertexAttribPointerType.Float,
							element.Normalized,
							layout.StrideBytes,
							element.OffsetBytes
						);
						break;
					case VertexElementType.Int32:
						GL.VertexAttribIPointer(
							element.Location,
							element.ComponentCount,
							VertexAttribIntegerType.Int,
							layout.StrideBytes,
							element.OffsetBytes
						);
						break;
					case VertexElementType.UInt32:
						GL.VertexAttribIPointer(
							element.Location,
							element.ComponentCount,
							VertexAttribIntegerType.UnsignedInt,
							layout.StrideBytes,
							element.OffsetBytes
						);
						break;
					default:
						return GraphicsError.Unsupported($"Unsupported vertex element type '{element.ElementType}'.");
				}
			}

			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to configure vertex layout: {exception.Message}");
		}
	}

	public Result<GraphicsError> SetDepthTestEnabled(bool enabled) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot change depth testing on a disposed render pass context.");
		}

		try {
			if (enabled) {
				GL.Enable(EnableCap.DepthTest);
				GL.DepthFunc(DepthFunction.Less);
			} else {
				GL.Disable(EnableCap.DepthTest);
			}

			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to change depth testing state: {exception.Message}");
		}
	}

	public Result<GraphicsError> Clear(
		ClearTargets targets,
		Vector4 color,
		float depth = 1.0f,
		int stencil = 0
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot clear on a disposed render pass context.");
		}

		if (targets == ClearTargets.None) {
			return Unit.Value;
		}

		try {
			ClearBufferMask mask = ToClearBufferMask(targets);
			if ((mask & ClearBufferMask.ColorBufferBit) != 0) {
				GL.ClearColor(color.X, color.Y, color.Z, color.W);
			}

			if ((mask & ClearBufferMask.DepthBufferBit) != 0) {
				GL.ClearDepth(depth);
			}

			if ((mask & ClearBufferMask.StencilBufferBit) != 0) {
				GL.ClearStencil(stencil);
			}

			GL.Clear(mask);
			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to clear render targets: {exception.Message}");
		}
	}

	public Result<GraphicsError> DrawArrays(
		PrimitiveTopology topology,
		int vertexCount,
		int firstVertex = 0
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot draw on a disposed render pass context.");
		}

		if (vertexCount < 0) {
			return GraphicsError.InvalidArgument("Vertex count cannot be negative.");
		}

		if (firstVertex < 0) {
			return GraphicsError.InvalidArgument("First vertex cannot be negative.");
		}

		if (vertexCount == 0) {
			return Unit.Value;
		}

		try {
			GL.DrawArrays(ToPrimitiveType(topology), firstVertex, vertexCount);
			return Unit.Value;
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to issue DrawArrays: {exception.Message}");
		}
	}

	public Result<GraphicsError> DrawIndexed(
		PrimitiveTopology topology,
		int indexCount,
		int firstIndex = 0,
		int baseVertex = 0
	) {
		if (_disposed) {
			return GraphicsError.DeviceDisposed("Cannot draw on a disposed render pass context.");
		}

		if (!_boundIndexElementType.HasValue) {
			return GraphicsError.InvalidState("Cannot issue indexed draw without a bound index buffer.");
		}

		if (indexCount < 0) {
			return GraphicsError.InvalidArgument("Index count cannot be negative.");
		}

		if (firstIndex < 0) {
			return GraphicsError.InvalidArgument("First index cannot be negative.");
		}

		if (indexCount == 0) {
			return Unit.Value;
		}

		try {
			int indexOffsetInBytes = checked(firstIndex * _boundIndexElementSize);
			GL.DrawElementsBaseVertex(
				ToPrimitiveType(topology),
				indexCount,
				ToDrawElementsType(_boundIndexElementType.Value),
				(IntPtr)indexOffsetInBytes,
				baseVertex
			);

			return Unit.Value;
		} catch (OverflowException) {
			return GraphicsError.InvalidArgument("First index is too large and overflowed offset calculation.");
		} catch (Exception exception) {
			return GraphicsError.BackendFailure($"Failed to issue DrawIndexed: {exception.Message}");
		}
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		if (!string.IsNullOrWhiteSpace(Label)) {
			Owner.PopDebugGroup();
		}

		_boundIndexElementType = null;
		_boundIndexElementSize = 0;
		_disposed = true;
	}

	private static PrimitiveType ToPrimitiveType(PrimitiveTopology topology) {
		return topology switch {
			PrimitiveTopology.Points => PrimitiveType.Points,
			PrimitiveTopology.Lines => PrimitiveType.Lines,
			PrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
			PrimitiveTopology.Triangles => PrimitiveType.Triangles,
			PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
			_ => PrimitiveType.Triangles
		};
	}

	private static DrawElementsType ToDrawElementsType(IndexElementType elementType) {
		return elementType switch {
			IndexElementType.UnsignedByte => DrawElementsType.UnsignedByte,
			IndexElementType.UnsignedShort => DrawElementsType.UnsignedShort,
			IndexElementType.UnsignedInt => DrawElementsType.UnsignedInt,
			_ => DrawElementsType.UnsignedInt
		};
	}

	private static ClearBufferMask ToClearBufferMask(ClearTargets targets) {
		ClearBufferMask mask = 0;
		if ((targets & ClearTargets.Color) != 0) {
			mask |= ClearBufferMask.ColorBufferBit;
		}

		if ((targets & ClearTargets.Depth) != 0) {
			mask |= ClearBufferMask.DepthBufferBit;
		}

		if ((targets & ClearTargets.Stencil) != 0) {
			mask |= ClearBufferMask.StencilBufferBit;
		}

		return mask;
	}
}
