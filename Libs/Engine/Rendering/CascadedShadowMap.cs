using System.Numerics;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>四级方向光阴影，2×2 atlas；光空间原点固定，中心按 texel 对齐，避免移动相机时漂移。</summary>
public sealed class CascadedShadowMap : IDisposable
{
    private readonly ShadowMap _map;
    public Matrix4x4[] Matrices { get; } = new Matrix4x4[4];
    public Vector4 Splits { get; private set; }
    public float Lambda = .6f;
    public float MaxDistance = 100f;
    public float BlendFraction = .1f;
    public bool DebugColors;
    public int TileSize { get; }
    public Target Target => _map.Target;
    public Texture DepthTexture => _map.DepthTexture;

    public CascadedShadowMap(GraphicsDevice device, int tileSize = 1024)
    {
        if (tileSize <= 0) throw new ArgumentOutOfRangeException(nameof(tileSize));
        TileSize = tileSize;
        _map = new ShadowMap(device, checked(tileSize * 2));
        _map.Resize(tileSize * 2);
    }

    public static Vector4 CalculateSplits(float near, float far, float lambda)
    {
        if (!(near > 0 && far > near)) throw new ArgumentOutOfRangeException(nameof(near));
        lambda = Math.Clamp(lambda, 0, 1);
        var result = new Vector4();
        for (var i = 1; i <= 4; i++)
        {
            var t = i / 4f;
            result[i - 1] = (1 - lambda) * (near + (far - near) * t) + lambda * near * MathF.Pow(far / near, t);
        }
        result.W = far;
        return result;
    }

    public static float Snap(float coordinate, float texelSize)
    {
        if (!(texelSize > 0)) throw new ArgumentOutOfRangeException(nameof(texelSize));
        return MathF.Round(coordinate / texelSize) * texelSize;
    }

    public RectInt Viewport(int cascade) => cascade is >= 0 and < 4
        ? new RectInt((cascade % 2) * TileSize, (cascade / 2) * TileSize, TileSize, TileSize)
        : throw new ArgumentOutOfRangeException(nameof(cascade));

    public void Update(Camera3D camera, Vector3 direction)
    {
        camera.Update();
        var near = MathF.Max(.001f, camera.NearClip);
        var far = MathF.Max(near + .001f, MathF.Min(camera.FarClip, MaxDistance));
        Splits = CalculateSplits(near, far, Lambda);
        var forward = camera.Forward;
        var right = camera.Right;
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var tan = MathF.Tan(Math.Clamp(camera.FieldOfView, .1f, MathF.PI - .1f) * .5f);
        var dir = direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : -Vector3.UnitY;
        var lightUp = MathF.Abs(dir.Y) > .99f ? Vector3.UnitZ : Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, dir, lightUp);
        var start = near;
        for (var i = 0; i < 4; i++)
        {
            var end = Splits[i];
            var fitStart = start;
            if (i > 0)
            {
                var previousStart = i == 1 ? near : Splits[i - 2];
                fitStart -= (start - previousStart) * Math.Clamp(BlendFraction, 0, .5f);
            }
            // 包围球半径只依赖分段距离和 FOV，避免相机旋转改变正交宽度。
            var center = camera.Position + forward * ((fitStart + end) * .5f);
            var radius = MathF.Sqrt(MathF.Pow((end - fitStart) * .5f, 2) + end * end * tan * tan * (1 + camera.AspectRatio * camera.AspectRatio));
            radius = MathF.Ceiling(radius * 16f) / 16f;
            // 留一个 texel 的边缘，吸收 snapping 的半 texel 偏移。
            radius *= TileSize / MathF.Max(1f, TileSize - 2f);
            var c = Vector3.Transform(center, view);
            var texel = 2 * radius / TileSize;
            c.X = Snap(c.X, texel); c.Y = Snap(c.Y, texel);
            // 向光源方向留出 caster 空间；屏幕外的投影物仍进入阴影 pass。
            var projection = Matrix4x4.CreateOrthographicOffCenter(c.X - radius, c.X + radius,
                c.Y - radius, c.Y + radius, -c.Z - radius * 3, -c.Z + radius * 3);
            Matrices[i] = view * projection;
            start = end;
        }
    }

    public void Clear() => _map.Clear();
    public void Dispose() => _map.Dispose();
}
