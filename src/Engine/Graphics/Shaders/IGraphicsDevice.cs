using Engine.Graphics.Resources;

namespace Engine.Graphics.Shaders;

public interface IGraphicsDevice : IDisposable {
	Result<IRenderPassContext, GraphicsError> BeginRenderPass(string? label = null);

	Result<VertexBuffer<TVertex>, GraphicsError> CreateVertexBuffer<TVertex>(
		ReadOnlySpan<TVertex> vertices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TVertex : unmanaged;

	Result<IndexBuffer<TIndex>, GraphicsError> CreateIndexBuffer<TIndex>(
		ReadOnlySpan<TIndex> indices,
		BufferUsage usage = BufferUsage.StaticDraw,
		string? label = null
	)
		where TIndex : unmanaged;

	Result<Texture2D, GraphicsError> CreateTexture2D(
		Texture2DDescriptor descriptor,
		ReadOnlySpan<byte> pixels,
		string? label = null
	);

	Result<Texture2D, GraphicsError> LoadTexture2DFromFile(
		string path,
		Texture2DLoadOptions? options = null,
		string? label = null
	) {
		if (string.IsNullOrWhiteSpace(path)) {
			return GraphicsError.InvalidArgument("Texture path cannot be null or empty.");
		}

		Texture2DLoadOptions loadOptions = options ?? new Texture2DLoadOptions();
		Result<DecodedImage2D, GraphicsError> decodeResult = ImageDecoders.DecodeFile(path, loadOptions.FlipVertically);
		if (decodeResult.TryErr() is { Error: var decodeError }) {
			return decodeError;
		}

		if (decodeResult.TryOk() is not { Value: var decoded }) {
			return GraphicsError.Unexpected("Image decoder returned an invalid result state.");
		}

		TextureMinFilter minFilter = loadOptions.GenerateMipmaps
			? loadOptions.MinFilter
			: loadOptions.MinFilter switch {
				TextureMinFilter.NearestMipmapNearest => TextureMinFilter.Nearest,
				TextureMinFilter.LinearMipmapLinear => TextureMinFilter.Linear,
				_ => loadOptions.MinFilter
			};

		Texture2DDescriptor descriptor = new(decoded.Width, decoded.Height, decoded.Format) {
			GenerateMipmaps = loadOptions.GenerateMipmaps,
			MinFilter = minFilter,
			MagFilter = loadOptions.MagFilter,
			WrapU = loadOptions.WrapU,
			WrapV = loadOptions.WrapV
		};

		string resolvedLabel = string.IsNullOrWhiteSpace(label) ? Path.GetFileName(path) : label;
		return CreateTexture2D(descriptor, decoded.Pixels, resolvedLabel);
	}

	Result<ShaderLoadSuccess<TBinding>, ShaderLoadReport> LoadShader<TBinding>()
		where TBinding : class, IGeneratedShaderBinding, new();

	Result<TBackend, GraphicsError> GetBackend<TBackend>()
		where TBackend : class;
}