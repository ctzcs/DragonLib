using System.Numerics;
using Engine;
using Engine.Assets.Dasset;
using Engine.Rendering;
using Foster.Framework;
using SharpGLTF.Schema2;
using SchemaMaterial = SharpGLTF.Schema2.Material;
using SchemaTexture = SharpGLTF.Schema2.Texture;
using SkeletonAnimator = Engine.Animation.SkeletonAnimator;

namespace DragonLib.Gltf;

/// <summary>
/// 把 .glb/.gltf cook 成 <see cref="DassetModel"/>（纯数据，不碰 GraphicsDevice）。
/// 供 Tools/FbxToGltf 的 DassetCompiler 离线调用；运行时不再直接解析 glTF。
///
/// 约定与取舍（与运行时渲染端一致）：
/// - 静态 primitive：场景图节点变换烘焙进顶点。蒙皮 primitive（节点带 skin 且 primitive
///   有 JOINTS_0/WEIGHTS_0）：顶点保持 mesh bind 空间，JOINTS_0 下标按拓扑序重映射，
///   WEIGHTS_0 归一化；bind pose 下 palette ≈ 骨架挂点空间的恒等摆放。
/// - 动画：只收关节 channel，支持 LINEAR/STEP/CUBICSPLINE；一条剪辑须同属一个骨架。
/// - 绕序：正面从外侧看 CCW，SDL front_face=COUNTER_CLOCKWISE；glTF 绕序保留，
///   仅负行列式的节点矩阵翻转补偿（见 WindingTests 与 Rendering3D.Smoke）。
/// - 缺 NORMAL 时累积面法线补齐；缺 TANGENT 且有 UV 时按 UV 梯度计算（w = 手性符号）。
/// - 贴图只收 PNG/JPG 原始字节（不解码，运行时 Foster Image 解码路径不变）；webp/dds/ktx2 跳过并警告。
/// - AlphaMode/AlphaCutoff 从 glTF 材质读出写入 DassetMaterial；逐 primitive 与模型级 AABB 一并算出
///   （蒙皮 primitive 是 bind pose 包围盒，动画姿态可能超出）。
/// </summary>
public static class GltfModelCooker
{
    /// <summary>一个 primitive 的几何属性读数（positions 必有，其余可缺）。</summary>
    private sealed class PrimitiveGeometry
    {
        public required Vector3[] Positions;
        public Vector3[]? Normals;
        public Vector2[]? Uvs;
        public Vector4[]? Tangents;
        public required List<(int A, int B, int C)> Triangles;
    }

    public static DassetModel Cook(string filePath, string? assetName = null)
    {
        var root = ModelRoot.Load(filePath);
        return Cook(root, assetName ?? Path.GetFileName(filePath));
    }

    public static DassetModel Cook(ModelRoot root, string assetName)
    {
        ArgumentNullException.ThrowIfNull(root);

        var model = new DassetModel { Bounds = DassetBounds.Empty };
        var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
        if (scene == null)
        {
            Log.Warning($"GltfModelCooker: '{assetName}' 没有场景，产出空模型。");
            return model;
        }

        var textureIndices = new Dictionary<int, int>();
        var materialCache = new Dictionary<int, DassetMaterial>();

        // skin → (骨架下标, 关节旧下标→新下标)；node → (骨架下标, 关节新下标)，动画 channel 映射用。
        var skeletonsBySkin = new Dictionary<Skin, (int Index, int[] JointRemap)>();
        var nodeToJoint = new Dictionary<Node, (int Skin, int Joint)>();

        foreach (var node in EnumerateNodes(scene))
        {
            if (node.Mesh == null)
                continue;

            var skinIndex = -1;
            int[]? jointRemap = null;
            if (node.Skin != null)
            {
                (skinIndex, jointRemap) = GetOrAddSkeleton(node.Skin, model, skeletonsBySkin, nodeToJoint, assetName);
            }

            var world = node.WorldMatrix;
            foreach (var primitive in node.Mesh.Primitives)
            {
                var skinned = skinIndex >= 0
                    && primitive.GetVertexAccessor("JOINTS_0") != null
                    && primitive.GetVertexAccessor("WEIGHTS_0") != null;

                var cooked = skinned
                    ? CookSkinnedPrimitive(primitive, skinIndex, jointRemap!, model, materialCache, textureIndices, assetName)
                    : CookPrimitive(primitive, world, model, materialCache, textureIndices, assetName);
                if (cooked != null)
                {
                    model.Primitives.Add(cooked);
                    model.Bounds.Encapsulate(cooked.Bounds);
                }
            }
        }

        CookAnimations(root, model, nodeToJoint, assetName);
        return model;
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

    /// <summary>把 skin 转成拓扑序骨架（父先于子），返回骨架下标与「旧 joint 下标→新下标」映射。</summary>
    private static (int Index, int[] JointRemap) GetOrAddSkeleton(
        Skin skin,
        DassetModel model,
        Dictionary<Skin, (int Index, int[] JointRemap)> cache,
        Dictionary<Node, (int Skin, int Joint)> nodeToJoint,
        string assetName)
    {
        if (cache.TryGetValue(skin, out var existing))
            return existing;

        var joints = skin.Joints;
        var ibms = skin.InverseBindMatrices;

        // 按节点深度稳定排序（父先子后），运行时一次线性扫描即可传播全局矩阵。
        var order = Enumerable.Range(0, joints.Count)
            .OrderBy(i => NodeDepth(joints[i]), Comparer<int>.Default)
            .ToArray();
        var oldToNew = new int[joints.Count];
        for (var i = 0; i < order.Length; i++)
            oldToNew[order[i]] = i;

        var skinIndex = model.Skeletons.Count;
        var nodeToOldIndex = new Dictionary<Node, int>();
        for (var i = 0; i < joints.Count; i++)
            nodeToOldIndex.TryAdd(joints[i], i);

        var skeleton = new DassetSkeleton();
        foreach (var oldIndex in order)
        {
            var node = joints[oldIndex];
            var parentIndex = -1;
            var parent = node.VisualParent;
            if (parent != null && nodeToOldIndex.TryGetValue(parent, out var parentOld))
                parentIndex = oldToNew[parentOld];

            var local = node.LocalTransform;
            skeleton.Joints.Add(new DassetJoint
            {
                Name = node.Name ?? $"joint{oldIndex}",
                ParentIndex = parentIndex,
                BindTranslation = local.Translation,
                BindRotation = local.Rotation,
                BindScale = local.Scale,
                InverseBindMatrix = oldIndex < ibms.Count ? ibms[oldIndex] : Matrix4x4.Identity,
            });
            nodeToJoint.TryAdd(node, (skinIndex, oldToNew[oldIndex]));
        }

        if (skeleton.Joints.Count > SkeletonAnimator.MaxJoints)
        {
            Log.Warning($"GltfModelCooker: '{assetName}' 的骨架有 {skeleton.Joints.Count} 个关节，"
                + $"超出 shader 上限 {SkeletonAnimator.MaxJoints}，渲染时多余关节的权重会落到关节 0。");
        }

        model.Skeletons.Add(skeleton);
        cache.Add(skin, (skinIndex, oldToNew));
        return (skinIndex, oldToNew);
    }

    private static int NodeDepth(Node node)
    {
        var depth = 0;
        var current = node.VisualParent;
        while (current != null)
        {
            depth++;
            current = current.VisualParent;
        }
        return depth;
    }

    private static void CookAnimations(
        ModelRoot root,
        DassetModel model,
        Dictionary<Node, (int Skin, int Joint)> nodeToJoint,
        string assetName)
    {
        foreach (var animation in root.LogicalAnimations)
        {
            var clip = new DassetAnimationClip
            {
                Name = animation.Name ?? $"clip{animation.LogicalIndex}",
                SkinIndex = -1,
                Duration = animation.Duration,
            };

            foreach (var channel in animation.Channels)
            {
                var target = channel.TargetNode;
                if (target == null || !nodeToJoint.TryGetValue(target, out var mapping))
                    continue; // 目标不是关节（如骨架挂点自身的动画）：MVP 不收。

                if (clip.SkinIndex < 0)
                    clip.SkinIndex = mapping.Skin;
                else if (clip.SkinIndex != mapping.Skin)
                {
                    Log.Warning($"GltfModelCooker: '{assetName}' 剪辑 '{clip.Name}' 的 channel 跨骨架，已跳过。");
                    continue;
                }

                var cookedChannel = new DassetAnimationChannel
                {
                    JointIndex = mapping.Joint,
                    Path = channel.TargetNodePath switch
                    {
                        PropertyPath.translation => DassetAnimPath.Translation,
                        PropertyPath.rotation => DassetAnimPath.Rotation,
                        _ => DassetAnimPath.Scale,
                    },
                };
                switch (channel.TargetNodePath)
                {
                    case PropertyPath.translation: ReadKeys(channel.GetTranslationSampler(), cookedChannel); break;
                    case PropertyPath.rotation: ReadKeys(channel.GetRotationSampler(), cookedChannel); break;
                    case PropertyPath.scale: ReadKeys(channel.GetScaleSampler(), cookedChannel); break;
                }
                if (cookedChannel.Times.Length > 0)
                    clip.Channels.Add(cookedChannel);
            }

            if (clip.Channels.Count > 0)
                model.Clips.Add(clip);
        }
    }

    private static Vector4 ToVector<T>(T value) => value switch
    {
        Quaternion q => new(q.X, q.Y, q.Z, q.W),
        Vector3 v => new(v, 0),
        _ => throw new InvalidDataException("Unsupported animation key type."),
    };

    private static void ReadKeys<T>(IAnimationSampler<T>? sampler, DassetAnimationChannel channel)
    {
        if (sampler == null) return;
        channel.Interpolation = sampler.InterpolationMode switch
        {
            AnimationInterpolationMode.STEP => DassetInterpolation.Step,
            AnimationInterpolationMode.CUBICSPLINE => DassetInterpolation.CubicSpline,
            _ => DassetInterpolation.Linear,
        };
        var times = new List<float>(); var values = new List<Vector4>();
        var ins = new List<Vector4>(); var outs = new List<Vector4>();
        if (channel.Interpolation == DassetInterpolation.CubicSpline)
        {
            foreach (var key in sampler.GetCubicKeys())
            {
                times.Add(key.Key); ins.Add(ToVector(key.Value.Item1));
                values.Add(ToVector(key.Value.Item2)); outs.Add(ToVector(key.Value.Item3));
            }
        }
        else
            foreach (var key in sampler.GetLinearKeys()) { times.Add(key.Key); values.Add(ToVector(key.Value)); }
        channel.Times = times.ToArray(); channel.Values = values.ToArray();
        channel.InTangents = ins.ToArray(); channel.OutTangents = outs.ToArray();
    }

    private static DassetPrimitive? CookPrimitive(
        MeshPrimitive primitive,
        Matrix4x4 world,
        DassetModel model,
        Dictionary<int, DassetMaterial> materialCache,
        Dictionary<int, int> textureIndices,
        string assetName)
    {
        var geometry = ReadGeometry(primitive, assetName);
        if (geometry == null)
            return null;

        var count = geometry.Positions.Length;
        var vertices = new PositionNormalUvVertex[count];

        var normalMatrix = world;
        if (Matrix4x4.Invert(world, out var inverse))
            normalMatrix = Matrix4x4.Transpose(inverse);

        for (var i = 0; i < count; i++)
        {
            var normal = geometry.Normals?[i] ?? Vector3.Zero;
            var tangent = geometry.Tangents?[i] ?? Vector4.Zero;
            vertices[i] = new PositionNormalUvVertex
            {
                Position = Vector3.Transform(geometry.Positions[i], world),
                Normal = normal,
                Uv = geometry.Uvs?[i] ?? Vector2.Zero,
                Tangent = tangent,
            };
        }

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

        // 正面 = 外侧 CCW（见头部注释）：正常矩阵保留 glTF 绕序；镜像（负行列式）反转了绕序，翻一次补偿。
        var flipWinding = world.GetDeterminant() < 0f;

        return new DassetPrimitive
        {
            Vertices = vertices,
            Indices = BuildIndices(geometry, flipWinding),
            Bounds = ComputeBounds(geometry.Positions, world),
            Material = CookMaterial(primitive.Material, model, materialCache, textureIndices),
        };
    }

    /// <summary>蒙皮 primitive：顶点保持 mesh bind 空间（不烘焙节点矩阵），JOINTS_0 重映射为拓扑序下标，WEIGHTS_0 归一化。</summary>
    private static DassetPrimitive? CookSkinnedPrimitive(
        MeshPrimitive primitive,
        int skinIndex,
        int[] jointRemap,
        DassetModel model,
        Dictionary<int, DassetMaterial> materialCache,
        Dictionary<int, int> textureIndices,
        string assetName)
    {
        var geometry = ReadGeometry(primitive, assetName);
        if (geometry == null)
            return null;

        var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0")!.AsVector4Array();
        var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0")!.AsVector4Array();

        var count = geometry.Positions.Length;
        var vertices = new PositionNormalUvSkinVertex[count];
        for (var i = 0; i < count; i++)
        {
            var rawJoints = jointsAccessor[i];
            var weights = weightsAccessor[i];

            // 权重归一化（源数据可能是非归一化的 byte/short 解码值）；全零权重兜底给关节 0。
            var weightSum = weights.X + weights.Y + weights.Z + weights.W;
            weights = weightSum > 1e-6f ? weights / weightSum : Vector4.UnitX;

            vertices[i] = new PositionNormalUvSkinVertex
            {
                Position = geometry.Positions[i],
                Normal = geometry.Normals?[i] ?? Vector3.UnitY,
                Uv = geometry.Uvs?[i] ?? Vector2.Zero,
                Tangent = geometry.Tangents?[i] ?? Vector4.UnitX,
                Joints = PackJoints(rawJoints, jointRemap),
                Weights = weights,
            };
        }

        // 蒙皮顶点不烘焙矩阵：绕序原样保留（外侧 CCW）。
        return new DassetPrimitive
        {
            SkinVertices = vertices,
            SkinIndex = skinIndex,
            Indices = BuildIndices(geometry, flipWinding: false),
            Bounds = ComputeBounds(geometry.Positions, Matrix4x4.Identity),
            Material = CookMaterial(primitive.Material, model, materialCache, textureIndices),
        };
    }

    private static uint PackJoints(Vector4 joints, int[] remap)
    {
        var packed = 0u;
        for (var i = 0; i < 4; i++)
        {
            var joint = (int)(i switch { 0 => joints.X, 1 => joints.Y, 2 => joints.Z, _ => joints.W });
            var mapped = joint >= 0 && joint < remap.Length ? remap[joint] : 0;
            packed |= (uint)Math.Min(mapped, 255) << (i * 8);
        }
        return packed;
    }

    /// <summary>读 primitive 的几何属性；positions 缺失/为空返回 null。缺法线/切线时按三角形累积补齐。</summary>
    private static PrimitiveGeometry? ReadGeometry(MeshPrimitive primitive, string assetName)
    {
        if (primitive.DrawPrimitiveType is not (PrimitiveType.TRIANGLES or PrimitiveType.TRIANGLE_STRIP or PrimitiveType.TRIANGLE_FAN))
        {
            Log.Warning($"GltfModelCooker: '{assetName}' 跳过非三角面片 primitive（{primitive.DrawPrimitiveType}）。");
            return null;
        }

        var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions == null || positions.Count == 0)
        {
            Log.Warning($"GltfModelCooker: '{assetName}' 的 primitive 没有 POSITION，已跳过。");
            return null;
        }

        var geometry = new PrimitiveGeometry
        {
            Positions = [.. positions],
            Normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array() is { } normals ? [.. normals] : null,
            Uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array() is { } uvs ? [.. uvs] : null,
            Tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array() is { } tangents ? [.. tangents] : null,
            Triangles = BuildTriangleList(primitive),
        };

        if (geometry.Normals == null)
            geometry.Normals = AccumulateSmoothNormals(geometry.Positions, geometry.Triangles);
        if (geometry.Tangents == null && geometry.Uvs != null)
            geometry.Tangents = AccumulateTangents(geometry.Positions, geometry.Uvs, geometry.Triangles);

        return geometry;
    }

    /// <summary>把 TRIANGLES/STRIP/FAN 统一展开成三角形顶点索引三元组列表。</summary>
    private static List<(int A, int B, int C)> BuildTriangleList(MeshPrimitive primitive)
    {
        var triangles = new List<(int A, int B, int C)>();
        foreach (var (a, b, c) in primitive.GetTriangleIndices())
            triangles.Add((a, b, c));
        return triangles;
    }

    private static uint[] BuildIndices(PrimitiveGeometry geometry, bool flipWinding)
    {
        var indices = new List<uint>(geometry.Triangles.Count * 3);
        foreach (var (a, b, c) in geometry.Triangles)
        {
            indices.Add((uint)a);
            indices.Add(flipWinding ? (uint)c : (uint)b);
            indices.Add(flipWinding ? (uint)b : (uint)c);
        }
        return indices.ToArray();
    }

    private static DassetBounds ComputeBounds(Vector3[] positions, Matrix4x4 transform)
    {
        var bounds = DassetBounds.Empty;
        foreach (var position in positions)
            bounds.Encapsulate(Vector3.Transform(position, transform));
        return bounds;
    }

    /// <summary>累积面法线得到平滑顶点法线（glTF 缺 NORMAL 时的常规补齐）。</summary>
    private static Vector3[] AccumulateSmoothNormals(Vector3[] positions, List<(int A, int B, int C)> triangles)
    {
        var accumulated = new Vector3[positions.Length];
        foreach (var (a, b, c) in triangles)
        {
            var faceNormal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            accumulated[a] += faceNormal;
            accumulated[b] += faceNormal;
            accumulated[c] += faceNormal;
        }

        for (var i = 0; i < accumulated.Length; i++)
            accumulated[i] = Vector3.Normalize(accumulated[i]) is { } n && n != Vector3.Zero ? n : Vector3.UnitY;
        return accumulated;
    }

    /// <summary>按 UV 梯度累积切线（glTF 无 TANGENT 时的常规做法），w 留 0（写入侧补手性符号）。</summary>
    private static Vector4[] AccumulateTangents(Vector3[] positions, Vector2[] uvs, List<(int A, int B, int C)> triangles)
    {
        var accumulated = new Vector3[positions.Length];
        foreach (var (i0, i1, i2) in triangles)
        {
            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var uv0 = uvs[i0];
            var uv1 = uvs[i1];
            var uv2 = uvs[i2];

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

        var result = new Vector4[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var raw = accumulated[i];
            var direction = raw == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(raw);
            result[i] = new Vector4(direction, 1f);
        }
        return result;
    }

    private static DassetMaterial CookMaterial(
        SchemaMaterial? material,
        DassetModel model,
        Dictionary<int, DassetMaterial> materialCache,
        Dictionary<int, int> textureIndices)
    {
        var index = material?.LogicalIndex ?? -1;
        if (index < 0)
            return new DassetMaterial();

        if (materialCache.TryGetValue(index, out var cached))
            return cached;

        var result = new DassetMaterial
        {
            BaseColorFactor = material!.FindChannel("BaseColor")?.Color ?? Vector4.One,
            Metallic = material.FindChannel("MetallicRoughness")?.GetFactor("MetallicFactor") ?? 0f,
            Roughness = material.FindChannel("MetallicRoughness")?.GetFactor("RoughnessFactor") ?? 1f,
            DoubleSided = material.DoubleSided,
            AlphaMode = material.Alpha switch
            {
                AlphaMode.MASK => DassetAlphaMode.Mask,
                AlphaMode.BLEND => DassetAlphaMode.Blend,
                _ => DassetAlphaMode.Opaque,
            },
            AlphaCutoff = material.AlphaCutoff,
            AlbedoTextureIndex = CookTexture(material.FindChannel("BaseColor")?.Texture, model, textureIndices),
            NormalTextureIndex = CookTexture(material.FindChannel("Normal")?.Texture, model, textureIndices),
            NormalScale = material.FindChannel("Normal")?.GetFactor("NormalScale") ?? 1f,
            MetallicRoughnessTextureIndex = CookTexture(material.FindChannel("MetallicRoughness")?.Texture, model, textureIndices),
            OcclusionTextureIndex = CookTexture(material.FindChannel("Occlusion")?.Texture, model, textureIndices),
            OcclusionStrength = material.FindChannel("Occlusion")?.GetFactor("OcclusionStrength") ?? 1f,
            EmissiveTextureIndex = CookTexture(material.FindChannel("Emissive")?.Texture, model, textureIndices),
            EmissiveFactor = new Vector3((material.FindChannel("Emissive")?.Color ?? Vector4.Zero).X,
                (material.FindChannel("Emissive")?.Color ?? Vector4.Zero).Y,
                (material.FindChannel("Emissive")?.Color ?? Vector4.Zero).Z),
        };
        materialCache.Add(index, result);
        return result;
    }

    /// <summary>把 glTF image 的原始字节收进贴图表（按 image 去重），返回表内下标；不收则返回 -1。</summary>
    private static int CookTexture(
        SchemaTexture? texture,
        DassetModel model,
        Dictionary<int, int> textureIndices)
    {
        if (texture == null)
            return -1;

        var image = texture.PrimaryImage ?? texture.FallbackImage;
        if (image == null)
            return -1;

        var index = image.LogicalIndex;
        if (textureIndices.TryGetValue(index, out var existing))
            return existing;

        var content = image.Content;
        if (content.IsEmpty)
            return -1;

        DassetTextureCodec codec;
        if (content.IsPng)
            codec = DassetTextureCodec.Png;
        else if (content.IsJpg)
            codec = DassetTextureCodec.Jpg;
        else
        {
            Log.Warning($"GltfModelCooker: 贴图 '{image.Name}' 是 {content.FileExtension ?? "未知格式"}，只支持 PNG/JPG，已跳过。");
            return -1;
        }

        // image.Content 是 MemoryImage 包装，.Content 才是文件字节。
        var bytes = content.Content.ToArray();

        var entry = new DassetTextureEntry
        {
            Name = image.Name ?? $"image{index}",
            Codec = codec,
            Bytes = bytes,
        };
        model.Textures.Add(entry);
        textureIndices.Add(index, model.Textures.Count - 1);
        return model.Textures.Count - 1;
    }
}
