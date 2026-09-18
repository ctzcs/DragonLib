using System.Numerics;
using DragonLib.Gltf;
using Engine.Assets.Dasset;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// SkinningDemo「顶盖亮、侧面暗」的光照几何真值：用 cook 产物的面法线 × demo 方向光
/// 复算各面受光因子（nDotL），证明暗侧面是光照几何的必然结果（背光面只剩环境光），
/// 而不是法线数据错误。光方向常量与 SkinningDemo 的 LightUniforms 保持同步。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class SkinningLightingTests
{
    // 与 SkinningDemo.Render 的 LightDirection 同步：new Vector4(Normalize(-0.5,-1,0.35), 0)。
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.35f));

    // 与 SkinningDemo 的 Ambient 同步。
    private static readonly Vector3 Ambient = new(0.36f, 0.39f, 0.46f);

    [Fact]
    public void EachFaceBrightnessMatchesLightDirectionGeometry()
    {
        var model = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "lighting_test");
        var vertices = Assert.Single(model.Primitives).SkinVertices!;
        var light = -LightDirection; // 指向光源（shader 里的 lightDir）

        // 按面法线归类：六个朝向（±X/±Y/±Z）各取一个代表。
        var byDirection = new Dictionary<Vector3, Vector3>();
        foreach (var v in vertices)
        {
            var dir = Direction6(v.Normal);
            byDirection.TryAdd(dir, v.Normal);
        }
        Assert.Equal(6, byDirection.Count); // 四方柱 + 顶/底盖，六向齐全

        float NdotL(Vector3 normal) => MathF.Max(0f, Vector3.Dot(normal, light));

        // 顶盖受直射；+X/-Z 侧面中等受光——这些面在任何视角下都不该是黑的。
        Assert.True(NdotL(byDirection[Vector3.UnitY]) > 0.8f);
        Assert.True(NdotL(byDirection[Vector3.UnitX]) > 0.4f);
        Assert.True(NdotL(byDirection[-Vector3.UnitZ]) > 0.25f);

        // -X/+Z 侧面与底盖背光：直射为零，只剩环境光——暗灰（≈0.24）而非全黑。
        Assert.Equal(0f, NdotL(byDirection[-Vector3.UnitX]));
        Assert.Equal(0f, NdotL(byDirection[Vector3.UnitZ]));
        Assert.Equal(0f, NdotL(byDirection[-Vector3.UnitY]));

        // 背光面的最终亮度 = Ambient × albedo（shader 里的环境光项）。
        var albedo = model.Primitives[0].Material.BaseColorFactor;
        var backlitBrightness = (Ambient.X * albedo.X + Ambient.Y * albedo.Y + Ambient.Z * albedo.Z) / 3f;
        Assert.True(backlitBrightness > 0.2f && backlitBrightness < 0.35f,
            $"背光面亮度应≈0.24（暗灰），实际 {backlitBrightness:F3}");
    }

    private static Vector3 Direction6(Vector3 normal)
    {
        // 归到最近的轴对齐方向。
        var absX = MathF.Abs(normal.X);
        var absY = MathF.Abs(normal.Y);
        var absZ = MathF.Abs(normal.Z);
        if (absY >= absX && absY >= absZ)
            return normal.Y > 0f ? Vector3.UnitY : -Vector3.UnitY;
        if (absX >= absZ)
            return normal.X > 0f ? Vector3.UnitX : -Vector3.UnitX;
        return normal.Z > 0f ? Vector3.UnitZ : -Vector3.UnitZ;
    }

    // ------------------------------------------------------------------
    // 「相机转时模型像在自转」的判别实验（假设 B：光照空间错误）。
    // 正确实现（光照在世界空间）下：漫反射与相机无关，相机动只移动高光点。
    // ------------------------------------------------------------------

    // 用户截图实测的两个相机状态（yaw -2.00 / -1.70）。
    private static readonly Vector3 CameraA = new(-8.18f, 1.19f, -3.74f);
    private static readonly Vector3 CameraB = new(-8.92f, 1.05f, -1.12f);

    /// <summary>Standard3DSkinned.hlsl 的 ShadeCookTorrance 复刻（改动 shader 时保持同步）。</summary>
    private static Vector3 ShadeCookTorrance(
        Vector3 normal, Vector3 viewDir, Vector3 lightDir, Vector3 radiance,
        Vector3 albedo, float metallic, float roughness)
    {
        var halfVector = Vector3.Normalize(viewDir + lightDir);
        var nDotL = Saturate(Vector3.Dot(normal, lightDir));
        var nDotV = Saturate(Vector3.Dot(normal, viewDir));

        var f0 = Vector3.Lerp(new Vector3(0.04f), albedo, metallic);
        var f = f0 + (Vector3.One - f0) * MathF.Pow(1f - Saturate(Vector3.Dot(halfVector, viewDir)), 5f);

        var a = roughness * roughness;
        var a2 = a * a;
        var nDotH = Saturate(Vector3.Dot(normal, halfVector));
        var denom = nDotH * nDotH * (a2 - 1f) + 1f;
        var d = a2 / (MathF.PI * denom * denom);

        float GeometrySchlick(float nDot)
        {
            var k = (roughness + 1f) * (roughness + 1f) / 8f;
            return nDot / (nDot * (1f - k) + k);
        }
        var g = GeometrySchlick(nDotV) * GeometrySchlick(nDotL);

        var specular = f * (d * g / MathF.Max(4f * nDotV * nDotL, 1e-4f));
        var diffuse = (Vector3.One - f) * (1f - metallic) * albedo;
        return (diffuse + specular) * radiance * nDotL;
    }

    private static float Saturate(float v) => Math.Clamp(v, 0f, 1f);

    [Fact]
    public void DiffuseBrightnessIsCameraIndependent()
    {
        // 柱子 -Z 侧面中点（受光面；相机 A/B 都在 -Z 侧，该面可见）。
        var point = new Vector3(0f, 1.5f, -0.25f);
        var normal = -Vector3.UnitZ;
        var lightDir = -LightDirection;
        var diffuseColor = new Vector3(1.00f, 0.95f, 0.88f); // 与 SkinningDemo 的 Diffuse 同步
        var albedo = new Vector3(0.72f, 0.58f, 0.50f);
        const float metallic = 0f;
        const float roughness = 0.85f;

        Vector3 Shade(Vector3 camera)
        {
            var viewDir = Vector3.Normalize(camera - point);
            return ShadeCookTorrance(normal, viewDir, lightDir, diffuseColor, albedo, metallic, roughness)
                + Ambient * albedo;
        }

        var correctA = Shade(CameraA);
        var correctB = Shade(CameraB);

        // 判别核心：漫反射项与相机无关——nDotL 固定，diffuse 两次完全相等；
        // 相机只动高光点（specular 经 viewDir），总亮度差异必须小于高光量级。
        var diffA = correctA - Ambient * albedo;
        var diffB = correctB - Ambient * albedo;
        Assert.True(Vector3.Distance(diffA, diffB) < 0.05f,
            $"漫反射随相机变化了（{Length3(diffA):F3} → {Length3(diffB):F3}）——光照空间有鬼。");

        // 对照（假设 B 的形态：法线被错带进 view 空间而光留在世界空间——空间不一致）：
        // 用 +X 法线（侧向分量对相机朝向敏感）：等效 nDotL 随相机变化（本例 Δ≈0.13），
        // 与正确实现的「严格不变」形成判别。
        var wrongNormalSource = Vector3.UnitX;
        float WrongNDotL(Vector3 camera)
        {
            var view = Matrix4x4.CreateLookAt(camera, new Vector3(0f, 1.5f, 0f), Vector3.UnitY);
            var wrongNormal = Vector3.Normalize(Vector3.TransformNormal(wrongNormalSource, view));
            return Saturate(Vector3.Dot(wrongNormal, lightDir));
        }
        var wrongDelta = MathF.Abs(WrongNDotL(CameraA) - WrongNDotL(CameraB));
        Assert.True(wrongDelta > 0.1f,
            $"对照组（光照空间不一致）nDotL 应随相机显著变化（实际 Δ={wrongDelta:F3}）——若此断言失败，本实验无判别力。");
    }

    private static float Length3(Vector3 v) => v.Length();
}
