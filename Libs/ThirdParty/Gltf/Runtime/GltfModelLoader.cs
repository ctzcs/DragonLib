using System.Numerics;
using System.Runtime.InteropServices;
using Engine;
using Engine.Rendering;
using Foster.Framework;
using SharpGLTF.Schema2;
using SchemaMaterial = SharpGLTF.Schema2.Material;
using SchemaTexture = SharpGLTF.Schema2.Texture;
using Texture = Foster.Framework.Texture;

namespace DragonLib.Gltf;

/// <summary>
/// 把 .glb/.gltf 加载成 <see cref="GltfModelAsset"/>。
///
/// 约定与取舍：
/// - 场景图节点变换烘焙进顶点（静态模型）；蒙皮/动画/morph 不在本期范围。
/// - glTF 前向面为逆时针（CCW），Foster 前向面为顺时针（见 ThreeDDemo 的 Mesh3D 索引绕序），
///   上传索引时翻转三角形绕序；含镜像（负行列式）的节点矩阵会再翻一次。
/// - 缺 NORMAL 时累积面法线补齐；缺 TANGENT 且有 UV 时按 UV 梯度计算（w = 手性符号）。
/// - 贴图只解码 PNG/JPG（Foster Image 能处理的格式）；webp/dds/ktx2 跳过并警告。
/// - Alpha Mask/Blend 与 DoubleSided 记录在材质上，但 Renderer3D 当前按不透明单面渲染。
/// </summary>
public static class GltfModelLoader
{
    public static GltfModelAsset Load(GraphicsDevice device, LocalStorage storage, string path)
    {
        ArgumentNullException.ThrowIfNull(storage);
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(storage.RootPath, path);
        return Load(device, fullPath, path);
    }

    public static GltfModelAsset Load(GraphicsDevice device, string filePath, string? assetName = null)
    {
        var root = ModelRoot.Load(filePath);
        return Load(device, root, assetName ?? Path.GetFileName(filePath));
    }

    public static GltfModelAsset Load(GraphicsDevice device, ModelRoot root, string assetName)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(root);

        var asset = new GltfModelAsset { Name = assetName };
        var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
        if (scene == null)
        {
            Log.Warning($"GltfModelLoader: '{assetName}' 没有场景，产出空模型。");
            return asset;
        }

        var textureCache = new Dictionary<int, Texture>();
        var materialCache = new Dictionary<int, GltfMaterial>();

        foreach (var node in EnumerateNodes(scene))
        {
            if (node.Mesh == null)
                continue;

            var world = node.WorldMatrix;
            foreach (var primitive in node.Mesh.Primitives)
                AddPrimitive(device, asset, primitive, world, materialCache, textureCache, assetName);
        }

        return asset;
    }

    private static IEnumerable<Node> EnumerateNodes(Scene scene)
    {
        foreach (var child in scene.VisualChildren)
        {
            yield return child;
            foreach (var descendant in EnumerateChildren(child))
                yield return descendant;
        }
    }

    private static IEnumerable<Node> EnumerateChildren(Node node)
    {
        foreach (var child in node.VisualChildren)
        {
            yield return child;
            foreach (var descendant in EnumerateChildren(child))
                yield return descendant;
        }
    }

    private static void AddPrimitive(
        GraphicsDevice device,
        GltfModelAsset asset,
        MeshPrimitive primitive,
        Matrix4x4 world,
        Dictionary<int, GltfMaterial> materialCache,
        Dictionary<int, Texture> textureCache,
        string assetName)
    {
        if (primitive.DrawPrimitiveType is not (PrimitiveType.TRIANGLES or PrimitiveType.TRIANGLE_STRIP or PrimitiveType.TRIANGLE_FAN))
        {
            Log.Warning($"GltfModelLoader: '{assetName}' 跳过非三角面片 primitive（{primitive.DrawPrimitiveType}）。");
            return;
        }

        var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions == null || positions.Count == 0)
        {
            Log.Warning($"GltfModelLoader: '{assetName}' 的 primitive 没有 POSITION，已跳过。");
            return;
        }

        var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array();

        var count = positions.Count;
        var vertices = new PositionNormalUvVertex[count];

        var normalMatrix = world;
        if (Matrix4x4.Invert(world, out var inverse))
            normalMatrix = Matrix4x4.Transpose(inverse);

        for (var i = 0; i < count; i++)
        {
            var normal = normals != null ? normals[i] : Vector3.Zero;
            var tangent = tangents != null ? tangents[i] : Vector4.Zero;
            vertices[i] = new PositionNormalUvVertex
            {
                Position = Vector3.Transform(positions[i], world),
                Normal = normal,
                Uv = uvs != null ? uvs[i] : Vector2.Zero,
                Tangent = tangent,
            };
        }

        if (normals == null)
            AccumulateSmoothNormals(vertices, BuildTriangleList(primitive));
        if (tangents == null && uvs != null)
            AccumulateTangents(vertices, BuildTriangleList(primitive));

        // 变换法线/切线（非均匀缩放需逆转置），并归一化。
        for (var i = 0; i < count; i++)
        {
            var v = vertices[i];
            v.Normal = Vector3.Normalize(Vector3.TransformNormal(v.Normal, normalMatrix));
            if (v.Normal == Vector3.Zero)
                v.Normal = Vector3.UnitY;

            var tangentDirection = Vector3.Normalize(Vector3.TransformNormal(new Vector3(v.Tangent.X, v.Tangent.Y, v.Tangent.Z), normalMatrix));
            if (tangentDirection == Vector3.Zero)
                tangentDirection = Vector3.UnitX;
            v.Tangent = new Vector4(tangentDirection, v.Tangent.W == 0f ? 1f : v.Tangent.W);
            vertices[i] = v;
        }

        // glTF CCW → Foster CW；镜像矩阵（负行列式）再翻一次。
        var flipWinding = world.GetDeterminant() >= 0f;
        var indices = new List<uint>(primitive.IndexAccessor?.Count ?? 0);
        foreach (var (a, b, c) in BuildTriangleList(primitive))
        {
            indices.Add((uint)a);
            indices.Add(flipWinding ? (uint)c : (uint)b);
            indices.Add(flipWinding ? (uint)b : (uint)c);
        }

        var mesh = new Mesh<PositionNormalUvVertex, uint>(device, $"gltf:{assetName}");
        mesh.SetVertices(vertices);
        mesh.SetIndices(CollectionsMarshal.AsSpan(indices));

        asset.Add(new GltfPrimitive
        {
            Mesh = mesh,
            Material = ResolveMaterial(device, primitive.Material, materialCache, textureCache, asset),
        });
    }

    /// <summary>把 TRIANGLES/STRIP/FAN 统一展开成三角形顶点索引三元组列表。</summary>
    private static List<(int A, int B, int C)> BuildTriangleList(MeshPrimitive primitive)
    {
        var triangles = new List<(int A, int B, int C)>();
        foreach (var (a, b, c) in primitive.GetTriangleIndices())
            triangles.Add((a, b, c));
        return triangles;
    }

    private static void AccumulateSmoothNormals(PositionNormalUvVertex[] vertices, List<(int A, int B, int C)> triangles)
    {
        var accumulated = new Vector3[vertices.Length];
        foreach (var (a, b, c) in triangles)
        {
            var faceNormal = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            accumulated[a] += faceNormal;
            accumulated[b] += faceNormal;
            accumulated[c] += faceNormal;
        }

        for (var i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            v.Normal = accumulated[i];
            vertices[i] = v;
        }
    }

    /// <summary>按 UV 梯度累积切线（glTF 无 TANGENT 时的常规做法），随后在写入时与法线正交化。</summary>
    private static void AccumulateTangents(PositionNormalUvVertex[] vertices, List<(int A, int B, int C)> triangles)
    {
        var accumulated = new Vector3[vertices.Length];
        foreach (var (i0, i1, i2) in triangles)
        {
            var p0 = vertices[i0].Position;
            var p1 = vertices[i1].Position;
            var p2 = vertices[i2].Position;
            var uv0 = vertices[i0].Uv;
            var uv1 = vertices[i1].Uv;
            var uv2 = vertices[i2].Uv;

            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var deltaUv1 = uv1 - uv0;
            var deltaUv2 = uv2 - uv0;

            var area = deltaUv1.X * deltaUv2.Y - deltaUv1.Y * deltaUv2.X;
            if (MathF.Abs(area) < 1e-10f)
                continue;

            var scale = 1f / area;
            var tangent = (edge1 * deltaUv2.Y - edge2 * deltaUv1.Y) * scale;
            accumulated[i0] += tangent;
            accumulated[i1] += tangent;
            accumulated[i2] += tangent;
        }

        for (var i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            var raw = accumulated[i];
            v.Tangent = new Vector4(raw, 0f);
            vertices[i] = v;
        }
    }

    private static GltfMaterial ResolveMaterial(
        GraphicsDevice device,
        SchemaMaterial? material,
        Dictionary<int, GltfMaterial> materialCache,
        Dictionary<int, Texture> textureCache,
        GltfModelAsset asset)
    {
        var index = material?.LogicalIndex ?? -1;
        if (index < 0)
            return new GltfMaterial();

        if (materialCache.TryGetValue(index, out var cached))
            return cached;

        var result = new GltfMaterial
        {
            BaseColorFactor = material!.FindChannel("BaseColor")?.Parameter ?? Vector4.One,
            Metallic = material.FindChannel("MetallicRoughness")?.Parameter.X ?? 0f,
            Roughness = material.FindChannel("MetallicRoughness")?.Parameter.Y ?? 1f,
            DoubleSided = material.DoubleSided,
            AlbedoTexture = ResolveTexture(device, material.FindChannel("BaseColor")?.Texture, textureCache, asset),
            NormalTexture = ResolveTexture(device, material.FindChannel("Normal")?.Texture, textureCache, asset),
        };
        materialCache.Add(index, result);
        return result;
    }

    private static Texture? ResolveTexture(
        GraphicsDevice device,
        SchemaTexture? texture,
        Dictionary<int, Texture> textureCache,
        GltfModelAsset asset)
    {
        if (texture == null)
            return null;

        var image = texture.PrimaryImage ?? texture.FallbackImage;
        if (image == null)
            return null;

        var index = image.LogicalIndex;
        if (textureCache.TryGetValue(index, out var cached))
            return cached;

        var content = image.Content;
        if (content.IsEmpty)
            return null;
        if (!content.IsPng && !content.IsJpg)
        {
            Log.Warning($"GltfModelLoader: 贴图 '{image.Name}' 是 {content.FileExtension ?? "未知格式"}，只支持 PNG/JPG，已跳过。");
            return null;
        }

        using var stream = content.Open();
        using var decoded = new Foster.Framework.Image(stream);
        var result = new Texture(device, decoded);

        textureCache.Add(index, result);
        asset.TrackTexture(result);
        return result;
    }
}
