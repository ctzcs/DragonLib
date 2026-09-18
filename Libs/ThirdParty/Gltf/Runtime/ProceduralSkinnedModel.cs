using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace DragonLib.Gltf;

/// <summary>
/// 程序化蒙皮示例模型：三节关节链（root → mid → tip）+ 一根绑了蒙皮权重的四方柱，
/// 一条 "wave" 剪辑（mid/tip 绕 Z 来回摆动，LINEAR，1 秒）。
/// 供单元测试（cook/读写/采样 round-trip）与 SkinningDemo 共用——不依赖 Blender 或外部资产。
/// </summary>
public static class ProceduralSkinnedModel
{
    public const string ClipName = "wave";

    /// <summary>三节关节的本地绑定平移（root 在原点，mid/tip 各上移 1）。</summary>
    public static readonly Vector3 RootTranslation = Vector3.Zero;
    public static readonly Vector3 MidTranslation = Vector3.UnitY;
    public static readonly Vector3 TipTranslation = Vector3.UnitY;

    /// <summary>mid 关节的摆动幅度（弧度）。</summary>
    public const float MidSwingRadians = 0.6f;

    public static ModelRoot Build()
    {
        // 骨架：root(0,0,0) → mid(0,1,0) → tip(0,1,0)（本地平移，相对父级）。
        var jointRoot = new NodeBuilder("root").WithLocalTranslation(RootTranslation);
        var jointMid = jointRoot.CreateNode("mid").WithLocalTranslation(MidTranslation);
        var jointTip = jointMid.CreateNode("tip").WithLocalTranslation(TipTranslation);

        // wave 剪辑：mid 绕 Z 摆 ±MidSwingRadians，tip 反向小摆；首尾同姿态保证循环平滑。
        jointMid.WithLocalRotation(ClipName, new Dictionary<float, Quaternion>
        {
            [0f] = Quaternion.Identity,
            [0.5f] = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MidSwingRadians),
            [1f] = Quaternion.Identity,
        });
        jointTip.WithLocalRotation(ClipName, new Dictionary<float, Quaternion>
        {
            [0f] = Quaternion.Identity,
            [0.5f] = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.4f),
            [1f] = Quaternion.Identity,
        });

        var mesh = BuildColumnMesh();

        var scene = new SceneBuilder("skinned");
        scene.AddSkinnedMesh(mesh, new (NodeBuilder, Matrix4x4)[]
        {
            (jointRoot, jointRoot.GetInverseBindMatrix(Matrix4x4.Identity)),
            (jointMid, jointMid.GetInverseBindMatrix(Matrix4x4.Identity)),
            (jointTip, jointTip.GetInverseBindMatrix(Matrix4x4.Identity)),
        });
        return scene.ToGltf2();
    }

    /// <summary>四方柱：y∈[0,3] 分 5 环，环权重沿关节链过渡（底部 root，顶部 tip）。</summary>
    private static MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexJoints4> BuildColumnMesh()
    {
        var material = new MaterialBuilder("column")
            .WithDoubleSide(false)
            .WithMetallicRoughness(0f, 0.85f)
            .WithBaseColor(new Vector4(0.72f, 0.58f, 0.5f, 1f));

        var mesh = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexJoints4>("column");
        var primitive = mesh.UsePrimitive(material);

        // 每环：(y, (关节, 权重) 绑定)。环 2 与 3 混权，弯曲时有平滑过渡。
        var rings = new (float Y, (int Joint, float Weight)[] Bindings)[]
        {
            (0.0f, [(0, 1f)]),
            (0.75f, [(0, 0.75f), (1, 0.25f)]),
            (1.5f, [(1, 0.8f), (0, 0.2f)]),
            (2.25f, [(1, 0.5f), (2, 0.5f)]),
            (3.0f, [(2, 1f)]),
        };

        // 方形截面的四个角与面法线（棱角处用法线朝外对角方向，视觉可接受的演示模型）。
        var corners = new[]
        {
            new Vector3(-0.25f, 0f, -0.25f),
            new Vector3(0.25f, 0f, -0.25f),
            new Vector3(0.25f, 0f, 0.25f),
            new Vector3(-0.25f, 0f, 0.25f),
        };

        for (var ring = 0; ring < rings.Length - 1; ring++)
        {
            var (y0, bind0) = rings[ring];
            var (y1, bind1) = rings[ring + 1];
            for (var side = 0; side < 4; side++)
            {
                var c0 = corners[side];
                var c1 = corners[(side + 1) % 4];
                var normal = Vector3.Normalize(new Vector3(c0.X + c1.X, 0f, c0.Z + c1.Z));
                var a = Vertex(c0, y0, normal, bind0);
                var b = Vertex(c1, y0, normal, bind0);
                var c = Vertex(c1, y1, normal, bind1);
                var d = Vertex(c0, y1, normal, bind1);
                // 外侧 CCW（叉积与面法线同向），与其他资产路径的正面约定一致。
                primitive.AddTriangle(a, c, b);
                primitive.AddTriangle(a, d, c);
            }
        }

        // 顶/底盖封口：开放管子单面渲染时能看穿管腔（视觉像"内外不分"），封成封闭棱柱。
        // 底盖绑 root、顶盖绑 tip（与相邻环权重一致）；法线 ±Y，绕序保证叉积朝外。
        var bottom0 = Vertex(corners[0], rings[0].Y, -Vector3.UnitY, rings[0].Bindings);
        var bottom1 = Vertex(corners[1], rings[0].Y, -Vector3.UnitY, rings[0].Bindings);
        var bottom2 = Vertex(corners[2], rings[0].Y, -Vector3.UnitY, rings[0].Bindings);
        var bottom3 = Vertex(corners[3], rings[0].Y, -Vector3.UnitY, rings[0].Bindings);
        primitive.AddTriangle(bottom0, bottom1, bottom2);
        primitive.AddTriangle(bottom0, bottom2, bottom3);

        var topRing = rings[^1];
        var top0 = Vertex(corners[0], topRing.Y, Vector3.UnitY, topRing.Bindings);
        var top1 = Vertex(corners[1], topRing.Y, Vector3.UnitY, topRing.Bindings);
        var top2 = Vertex(corners[2], topRing.Y, Vector3.UnitY, topRing.Bindings);
        var top3 = Vertex(corners[3], topRing.Y, Vector3.UnitY, topRing.Bindings);
        primitive.AddTriangle(top0, top2, top1);
        primitive.AddTriangle(top0, top3, top2);

        return mesh;

        static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4> Vertex(
            Vector3 corner, float y, Vector3 normal, (int Joint, float Weight)[] bindings)
            => new(
                new VertexPositionNormal(new Vector3(corner.X, y, corner.Z), normal),
                new VertexTexture1(new Vector2(0f, y / 3f)),
                bindings);
    }
}
