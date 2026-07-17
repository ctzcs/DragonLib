using System.Numerics;
using Foster.Framework;
using System.Runtime.InteropServices;
using Prowl.Quill;
using Prowl.Vector;
using Color = Foster.Framework.Color;
using FosterDrawCommand = Foster.Framework.DrawCommand;

namespace Engine.Paper;

/// <summary>
/// Prowl.Quill → Foster 画布后端。
/// <para>
/// Quill / Scribe 文字图集是 <b>单通道 SDF</b>（R=G=B=distance，A=255）。
/// 普通纹理与字体图集使用独立 sampler；UV +2 的文字标记保留到 fragment shader。
/// </para>
/// UV：纯色 AA 为 0/1；字形由 TextRenderer 做了 +2，并由专用 shader 解码。
/// </summary>
public sealed class FosterCanvasRenderer : ICanvasRenderer
{
	private const float DefaultSdfDistanceRange = 4f;

	[StructLayout(LayoutKind.Sequential)]
	private struct FragmentUniformData
	{
		public Matrix4x4 BrushTextureMatrix;
		public float DistanceRange;
		public float DpiScale;
		public Vector2 Padding;
	}

	private readonly GraphicsDevice _device;
	private readonly Mesh<PosTexColVertex, uint> _mesh;
	private readonly Material _material;
	private readonly Texture _whiteTexture;
	private readonly List<Texture> _ownedTextures = [];
	private readonly TextureSampler _sampler = new(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp);

	private PosTexColVertex[] _vertexScratch = [];
	private uint[] _indexScratch = [];

	public IDrawableTarget Target { get; set; }

	public bool SupportsBackdropBlur => false;

	public FosterCanvasRenderer(App app)
		: this(app.GraphicsDevice, app.Window)
	{
	}

	public FosterCanvasRenderer(GraphicsDevice device, IDrawableTarget target)
	{
		_device = device;
		Target = target;

		_mesh = new Mesh<PosTexColVertex, uint>(device, "Quill Mesh");
		_material = CreateMaterial(device);

		_whiteTexture = new Texture(device, 1, 1, [Color.White], name: "Quill White");
		_ownedTextures.Add(_whiteTexture);
	}

	public object CreateTexture(uint width, uint height)
	{
		var texture = new Texture(
			_device,
			(int)width,
			(int)height,
			TextureFormat.Color,
			TextureFlags.None,
			name: "Quill Atlas");
		_ownedTextures.Add(texture);
		return texture;
	}

	public Int2 GetTextureSize(object texture)
	{
		var tex = (Texture)texture;
		return new Int2(tex.Width, tex.Height);
	}

	public void SetTextureData(object texture, IntRect bounds, byte[] data)
	{
		var tex = (Texture)texture;
		var size = bounds.Size;
		if (size.X <= 0 || size.Y <= 0)
			return;

		// 原样上传 SDF（勿转成 alpha，否则丢失距离信息）
		var region = new RectInt(bounds.Min.X, bounds.Min.Y, size.X, size.Y);
		tex.SetData<byte>(data.AsSpan(0, size.X * size.Y * 4), region);
	}

	public void RenderCalls(Canvas canvas, IReadOnlyList<DrawCall> drawCalls)
	{
		if (drawCalls.Count == 0 || canvas.Vertices.Count == 0 || canvas.Indices.Count == 0)
			return;

		UploadGeometry(canvas);

		var target = Target;
		var size = target.SizeInPixels();
		if (size.X <= 0 || size.Y <= 0)
			return;

		var projection =
			Matrix4x4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, 0.1f, 1000.0f);
		_material.Vertex.SetUniformBuffer(projection);

		var pass = new FosterDrawCommand(target, _mesh, _material)
		{
			// Quill 顶点颜色已预乘；shader 的 SDF coverage 继续乘在 RGBA 上。
			BlendMode = BlendMode.Premultiply,
			CullMode = CullMode.None,
			DepthTestEnabled = false,
			DepthWriteEnabled = false,
		};

		var indexOffset = 0;
		for (var i = 0; i < drawCalls.Count; i++)
		{
			var call = drawCalls[i];
			var elementCount = call.ElementCount;
			if (elementCount <= 0)
			{
				indexOffset += elementCount;
				continue;
			}

			// 官方 Quill renderer 使用两个独立 sampler：普通 brush 与 font atlas。
			pass.FragmentSamplers[0] = new BoundSampler(call.Texture as Texture ?? _whiteTexture, _sampler);
			pass.FragmentSamplers[1] = new BoundSampler(call.FontAtlas as Texture ?? _whiteTexture, _sampler);

			_material.Fragment.SetUniformBuffer(new FragmentUniformData
			{
				// Prowl 的 TextureMatrix 是列向量约定（平移在第 4 列），而本管线走
				// 「C# 行主序上传 → HLSL 列主序读取」隐含一次转置。对投影矩阵（System.Numerics
				// 行向量约定）恰好抵消正确，但对这个列向量矩阵会被转错，导致 brush UV 丢失平移、
				// 图片被挤到 [0,1] 之外。上传前显式转置一次抵消。
				BrushTextureMatrix = Matrix4x4.Transpose(call.Brush.TextureMatrix),
				DistanceRange = DefaultSdfDistanceRange,
				DpiScale = MathF.Max(canvas.FramebufferScale, 0.000001f),
			});
			pass.IndexOffset = indexOffset;
			pass.IndexCount = elementCount;
			pass.Scissor = TryGetScissor(call, size);

			_device.Draw(pass);
			indexOffset += elementCount;
		}
	}

	public void Dispose()
	{
		_mesh.Dispose();
		_material.Vertex.Shader?.Dispose();
		_material.Fragment.Shader?.Dispose();
		foreach (var texture in _ownedTextures)
		{
			if (!texture.IsDisposed)
				texture.Dispose();
		}
		_ownedTextures.Clear();
	}

	private void UploadGeometry(Canvas canvas)
	{
		var vertexCount = canvas.Vertices.Count;
		var indexCount = canvas.Indices.Count;

		if (vertexCount > _vertexScratch.Length)
			_vertexScratch = new PosTexColVertex[vertexCount];
		if (indexCount > _indexScratch.Length)
			_indexScratch = new uint[indexCount];

		for (var i = 0; i < vertexCount; i++)
		{
			var v = canvas.Vertices[i];
			_vertexScratch[i] = new PosTexColVertex(
				new Vector2(v.x, v.y),
				new Vector2(v.u, v.v),
				new Color(v.r, v.g, v.b, v.a));
		}

		for (var i = 0; i < indexCount; i++)
			_indexScratch[i] = canvas.Indices[i];

		_mesh.SetVertices(_vertexScratch.AsSpan(0, vertexCount));
		_mesh.SetIndices(_indexScratch.AsSpan(0, indexCount));
	}

	private static Material CreateMaterial(GraphicsDevice device)
	{
		var extension = device.Driver.GetShaderExtension();
		var assembly = typeof(FosterCanvasRenderer).Assembly;

		return new Material(
			vertexShader: new Shader(
				device,
				ShaderStage.Vertex,
				Calc.ReadEmbeddedBytes(assembly, $"Shaders/QuillCanvas.vertex.{extension}"),
				uniformBufferCount: 1,
				entryPoint: "vertex_main",
				name: "QuillCanvasVertex"),
			fragmentShader: new Shader(
				device,
				ShaderStage.Fragment,
				Calc.ReadEmbeddedBytes(assembly, $"Shaders/QuillCanvas.fragment.{extension}"),
				samplerCount: 2,
				uniformBufferCount: 1,
				entryPoint: "fragment_main",
				name: "QuillCanvasFragment"));
	}

	private static RectInt? TryGetScissor(in DrawCall call, Point2 targetSize)
	{
		call.GetScissor(out var invMatrix, out var extent);
		if (extent.X < 0 || extent.Y < 0)
			return null;

		var toScreen = invMatrix.Invert();
		Span<Vector2> corners =
		[
			TransformPoint(toScreen, -extent.X, -extent.Y),
			TransformPoint(toScreen, extent.X, -extent.Y),
			TransformPoint(toScreen, extent.X, extent.Y),
			TransformPoint(toScreen, -extent.X, extent.Y),
		];

		var minX = corners[0].X;
		var minY = corners[0].Y;
		var maxX = minX;
		var maxY = minY;
		for (var i = 1; i < 4; i++)
		{
			minX = MathF.Min(minX, corners[i].X);
			minY = MathF.Min(minY, corners[i].Y);
			maxX = MathF.Max(maxX, corners[i].X);
			maxY = MathF.Max(maxY, corners[i].Y);
		}

		var left = (int)MathF.Floor(minX);
		var top = (int)MathF.Floor(minY);
		var right = (int)MathF.Ceiling(maxX);
		var bottom = (int)MathF.Ceiling(maxY);

		left = Math.Clamp(left, 0, targetSize.X);
		top = Math.Clamp(top, 0, targetSize.Y);
		right = Math.Clamp(right, 0, targetSize.X);
		bottom = Math.Clamp(bottom, 0, targetSize.Y);

		var w = right - left;
		var h = bottom - top;
		if (w <= 0 || h <= 0)
			return new RectInt(0, 0, 0, 0);

		return new RectInt(left, top, w, h);
	}

	private static Vector2 TransformPoint(Float4x4 m, float x, float y)
	{
		var c0 = m.c0;
		var c1 = m.c1;
		var c3 = m.c3;
		return new Vector2(
			c0.X * x + c1.X * y + c3.X,
			c0.Y * x + c1.Y * y + c3.Y);
	}
}
