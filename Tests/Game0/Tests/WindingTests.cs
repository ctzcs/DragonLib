using System.Numerics;
using DragonLib.Gltf;
using Engine.Animation;
using Engine.Assets.Dasset;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// 绕序约定：Foster 在 SDL_GPU 上 front_face = CLOCKWISE（像素坐标，y 向下），
/// 而 SDL NDC y+ 向上（D3D 风格，wiki.libsdl.org/SDL3/CategoryGPU）——NDC→像素有一次 y 翻转，
/// 所以「像素 CW」等价于「相机视角 CCW」。即 Foster 的正面与 glTF 一样是从外侧看逆时针（CCW）。
/// 以下测试断言 cook 产物的每个三角形叉积与顶点法线（外法线）同向：
/// 若 cooker 多翻了一次（历史 bug），点积会全部变负。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class WindingTests
{
    private static string CubeGlbPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../Resources/Models/fbx_cube.glb"));

    [Fact]
    public void CookedCubeTrianglesFaceOutward()
    {
        var model = GltfModelCooker.Cook(CubeGlbPath);
        var primitive = Assert.Single(model.Primitives);

        AssertWindingMatchesNormals(primitive.Vertices, primitive.Indices);
    }

    [Fact]
    public void MirroredNodeTrianglesStillFaceOutward()
    {
        // 同一个立方体 mesh 挂两个节点：正常 + 镜像（scale -1）。
        // 镜像矩阵会反转绕序，cooker 必须翻回来补偿，两面墙的叉积都应朝外。
        var mesh = BuildUnitCubeMesh();
        var scene = new SceneBuilder("mirror");
        scene.AddRigidMesh(mesh, new NodeBuilder("plain"));
        scene.AddRigidMesh(mesh, new NodeBuilder("mirrored").WithLocalScale(new Vector3(-1f, 1f, 1f)));

        var model = GltfModelCooker.Cook(scene.ToGltf2(), "mirror_test");

        Assert.Equal(2, model.Primitives.Count);
        foreach (var primitive in model.Primitives)
            AssertWindingMatchesNormals(primitive.Vertices, primitive.Indices);
    }

    private static void AssertWindingMatchesNormals(PositionNormalUvVertex[] vertices, uint[] indices)
    {
        Assert.True(indices.Length >= 3 && indices.Length % 3 == 0);
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            var cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            Assert.True(Vector3.Dot(cross, a.Normal) > 0f,
                $"三角形 {i / 3} 的叉积 {cross} 与顶点法线 {a.Normal} 反向（绕序错）。");
            Assert.True(Vector3.Dot(cross, b.Normal) > 0f);
            Assert.True(Vector3.Dot(cross, c.Normal) > 0f);
        }
    }

    [Fact]
    public void SkinnedProceduralColumnTrianglesFaceOutward()
    {
        // 蒙皮路径同样要满足外侧 CCW：ProceduralSkinnedModel 的四方柱手排顶点序。
        var model = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "procedural_skin");
        var primitive = Assert.Single(model.Primitives);
        var vertices = primitive.SkinVertices!;
        var indices = primitive.Indices;

        Assert.True(indices.Length >= 3 && indices.Length % 3 == 0);
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            var cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            Assert.True(Vector3.Dot(cross, a.Normal) > 0f,
                $"蒙皮三角形 {i / 3} 的叉积 {cross} 与顶点法线 {a.Normal} 反向（绕序错）。");
        }
    }

    [Fact]
    public void AnimatedJointGlobalsMatchSharpGltfOfficialEvaluation()
    {
        // 第三方真值：SharpGLTF 官方的 Node.GetWorldMatrix(animation, time) 与我们
        // SkeletonAnimator 的「采样 → 拓扑传播」结果逐元素一致（挂点在原点，armature 空间 = scene 空间）。
        var model = ProceduralSkinnedModel.Build();
        var animation = model.LogicalAnimations[0];
        var officialMid = model.LogicalNodes.First(n => n.Name == "mid").GetWorldMatrix(animation, 0.5f);
        var officialTip = model.LogicalNodes.First(n => n.Name == "tip").GetWorldMatrix(animation, 0.5f);

        var cooked = GltfModelCooker.Cook(model, "compare");
        var skeleton = cooked.Skeletons[0];
        Span<JointPose> pose = stackalloc JointPose[skeleton.Joints.Count];
        SkeletonAnimator.SamplePose(skeleton, cooked.Clips[0], 0.5f, pose);

        // 与 ComputePalette 相同的传播规则：global = local * parentGlobal。
        Span<Matrix4x4> globals = stackalloc Matrix4x4[skeleton.Joints.Count];
        for (var i = 0; i < skeleton.Joints.Count; i++)
        {
            var local = Matrix4x4.CreateScale(pose[i].Scale)
                * Matrix4x4.CreateFromQuaternion(pose[i].Rotation)
                * Matrix4x4.CreateTranslation(pose[i].Translation);
            globals[i] = skeleton.Joints[i].ParentIndex >= 0
                ? local * globals[skeleton.Joints[i].ParentIndex]
                : local;
        }

        AssertMatrixApprox(officialMid, globals[1], 1e-4f);
        AssertMatrixApprox(officialTip, globals[2], 1e-4f);
    }

    [Fact]
    public void SkinnedColumnVisibleFacesMatchCameraExpectation()
    {
        // 渲染视角模拟：SkinningDemo 初始相机在 (+x,+y,+z) 象限看原点——按 SDL 规则
        // （NDC CCW = 正面 = 叉积朝向相机可见），近侧面（+x/+z 两侧）应可见，远侧面（-x/-z）应剔除。
        // 若结果反过来，说明渲染端把内外判反了；若符合预期，则几何与约定一致。
        var model = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "procedural_skin");
        var primitive = Assert.Single(model.Primitives);
        var vertices = primitive.SkinVertices!;
        var indices = primitive.Indices;

        var camera = new Vector3(4.85f, 2.66f, 7.09f);
        var visibleToward = 0; // 法线含 +x/+z 分量（近侧）
        var visibleAway = 0;   // 法线含 -x/-z 分量（远侧）

        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]].Position;
            var b = vertices[indices[i + 1]].Position;
            var c = vertices[indices[i + 2]].Position;
            var faceCenter = (a + b + c) / 3f;
            var cross = Vector3.Cross(b - a, c - a);

            // 正面 ⟺ 叉积（三角形法向）朝向相机。
            var facingCamera = Vector3.Dot(cross, camera - faceCenter) > 0f;
            if (!facingCamera)
                continue;

            var outward = new Vector2(faceCenter.X, faceCenter.Z);
            if (Vector2.Dot(outward, Vector2.Normalize(new Vector2(camera.X, camera.Z))) > 0f)
                visibleToward++;
            else
                visibleAway++;
        }

        Assert.True(visibleToward > 0, "近侧没有可见三角形——内外判反了。");
        Assert.Equal(0, visibleAway);
    }

    [Fact]
    public void CameraOrbitChangesSkinnedVertexNdc()
    {
        // 端到端投影断言：完整复刻提交路径的 CPU 侧公式
        // （Renderer3D.SubmitDirect: WVP = World * ViewProjection；shader mul(WVP, skinned) ≡ v * WVP；
        // skinned = Σ w_j × v × palette[j]）。相机 orbit（yaw 0.6 → 1.2）后，
        // 蒙皮顶点的屏幕 x 必须显著移动——若不变，说明 view 没进变换链。
        var model = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "ndc_test");
        var skeleton = model.Skeletons[0];
        var clip = model.Clips[0];
        var palette = new Matrix4x4[skeleton.Joints.Count];
        SkeletonAnimator.ComputePalette(skeleton, clip, 0.2f, palette);

        var entityWorld = Matrix4x4.CreateTranslation(2.2f, 0f, 0f);

        // 顶环顶点（绑 tip，多权重槽位里第一个权重大）。
        var vertex = model.Primitives[0].SkinVertices!.First(v => v.Position.Y > 2.99f);
        var skinned = Vector3.Zero;
        for (var j = 0; j < 4; j++)
        {
            var jointIndex = (int)(vertex.Joints >> (j * 8)) & 0xFF;
            var weight = j switch { 0 => vertex.Weights.X, 1 => vertex.Weights.Y, 2 => vertex.Weights.Z, _ => vertex.Weights.W };
            skinned += Vector3.Transform(vertex.Position, palette[jointIndex]) * weight;
        }

        float ScreenX(float yaw)
        {
            var camera = new Camera3D
            {
                Target = new Vector3(0f, 1.5f, 0f),
                ViewportSize = new Point2(1280, 720),
            };
            camera.Position = new Vector3(0f, 1.5f, 0f) + new Vector3(MathF.Sin(yaw) * 8.8f, 2.6f, MathF.Cos(yaw) * 8.8f);
            camera.Update();

            var clipPos = Vector4.Transform(new Vector4(skinned, 1f), entityWorld * camera.ViewProjection);
            return (clipPos.X / clipPos.W + 1f) * 0.5f * 1280f;
        }

        var x0 = ScreenX(0.6f);
        var x1 = ScreenX(1.2f);
        // view 缺失时两次结果完全相等；正确参与时本顶点应移动 ~62px（解析复核见提交说明）。
        Assert.True(MathF.Abs(x0 - x1) > 30f,
            $"相机 orbit 后蒙皮顶点屏幕 x 几乎不变（{x0} → {x1}）——view 没进变换链。");
    }

    [Fact]
    public void SkinnedColumnGeometryFacesAwayFromAxis()
    {
        // 几何真值：不依赖顶点法线属性——直接验证每个三角形叉积背离柱体中轴（y 轴），
        // 且顶点法线属性本身也背离中轴。叉积/法线都朝里时「自洽但内外颠倒」会被这个测试抓住。
        var model = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "procedural_skin");
        var primitive = Assert.Single(model.Primitives);
        var vertices = primitive.SkinVertices!;
        var indices = primitive.Indices;

        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]].Position;
            var b = vertices[indices[i + 1]].Position;
            var c = vertices[indices[i + 2]].Position;

            var faceCenter = (a + b + c) / 3f;
            var cross = Vector3.Cross(b - a, c - a);

            var crossHorizontal = MathF.Sqrt(cross.X * cross.X + cross.Z * cross.Z);
            if (MathF.Abs(cross.Y) >= crossHorizontal)
            {
                // 顶/底盖（叉积竖直）：应朝上（顶盖）或朝下（底盖）。
                var expectedUp = faceCenter.Y > 1.5f;
                Assert.True(expectedUp ? cross.Y > 0f : cross.Y < 0f,
                    $"盖面三角形 {i / 3}（面心 {faceCenter}）叉积 {cross} 朝向错误。");
            }
            else
            {
                // 侧面（叉积水平）：背离中轴（y 轴）。
                var horizontal = new Vector3(faceCenter.X, 0f, faceCenter.Z);
                Assert.True(horizontal.LengthSquared() > 1e-6f
                    && Vector3.Dot(cross, Vector3.Normalize(horizontal)) > 0f,
                    $"三角形 {i / 3}（面心 {faceCenter}）叉积 {cross} 不背离中轴（朝里）。");
            }

            foreach (var v in new[] { vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]] })
            {
                var vHorizontal = new Vector3(v.Position.X, 0f, v.Position.Z);
                var normalHorizontal = MathF.Sqrt(v.Normal.X * v.Normal.X + v.Normal.Z * v.Normal.Z);
                if (MathF.Abs(v.Normal.Y) >= normalHorizontal)
                {
                    // 盖面顶点（法线竖直）：底盖朝下、顶盖朝上。
                    var expectedUp = v.Position.Y > 1.5f;
                    Assert.True(expectedUp ? v.Normal.Y > 0f : v.Normal.Y < 0f,
                        $"盖面顶点 {v.Position} 的法线 {v.Normal} 朝向错误。");
                }
                else
                {
                    Assert.True(vHorizontal.LengthSquared() > 1e-6f
                        && Vector3.Dot(v.Normal, Vector3.Normalize(vHorizontal)) > 0f,
                        $"顶点 {v.Position} 的法线 {v.Normal} 朝里。");
                }
            }
        }
    }

    /// <summary>外侧 CCW 的单位立方体（glTF 约定的正确资产），顶点法线 = 面法线。</summary>
    private static MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> BuildUnitCubeMesh()
    {
        var material = new MaterialBuilder("cube").WithDoubleSide(false).WithMetallicRoughness(0f, 1f);
        var mesh = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>("cube");
        var primitive = mesh.UsePrimitive(material);
        const float h = 0.5f;

        AddFace(primitive, Vector3.UnitX,
            new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h));
        AddFace(primitive, -Vector3.UnitX,
            new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h));
        AddFace(primitive, Vector3.UnitY,
            new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h), new Vector3(-h, h, -h));
        AddFace(primitive, -Vector3.UnitY,
            new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h));
        AddFace(primitive, Vector3.UnitZ,
            new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h));
        AddFace(primitive, -Vector3.UnitZ,
            new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h));
        return mesh;
    }

    private static void AddFace(
        IPrimitiveBuilder primitive,
        Vector3 normal,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        // 保证 (a,b,c) 叉积朝外（传入点序不符时交换 b/d）。
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
            (b, d) = (d, b);

        var va = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
            new VertexPositionNormal(a, normal), new VertexTexture1(Vector2.Zero));
        var vb = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
            new VertexPositionNormal(b, normal), new VertexTexture1(Vector2.Zero));
        var vc = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
            new VertexPositionNormal(c, normal), new VertexTexture1(Vector2.Zero));
        var vd = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
            new VertexPositionNormal(d, normal), new VertexTexture1(Vector2.Zero));
        primitive.AddTriangle(va, vb, vc);
        primitive.AddTriangle(va, vc, vd);
    }

    private static void AssertMatrixApprox(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
    {
        var e = ToArray(expected);
        var a = ToArray(actual);
        for (var i = 0; i < 16; i++)
            Assert.True(MathF.Abs(e[i] - a[i]) < tolerance,
                $"矩阵元素 [{i}] 期望 {e[i]}，实际 {a[i]}。\n期望:\n{expected}\n实际:\n{actual}");
    }

    private static float[] ToArray(Matrix4x4 m)
        => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
}
