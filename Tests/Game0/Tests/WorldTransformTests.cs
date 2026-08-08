using System.Numerics;
using Engine.Spine;
using Engine.World;
using Foster.Framework;
using Spine;
using Xunit;

namespace Game0.EngineTests;

public sealed class WorldTransformTests
{
    [Fact]
    public void SpineTransformDefaultsToIdentity()
    {
        var skeleton = CreateSkeleton();
        var point = new Vector2(3.5f, -2f);

        AssertNear(point, skeleton.LocalToWorld(point));
        AssertNear(point, skeleton.WorldToLocal(point));
    }

    [Fact]
    public void SpineTransformRoundTripsPositionRotationAndNonUniformScale()
    {
        var skeleton = CreateSkeleton();
        skeleton.Position = new Vector2(12f, -7f);
        skeleton.Scale = new Vector2(2.5f, 0.75f);
        skeleton.Rotation = 0.63f;
        var local = new Vector2(-4f, 9f);

        var world = skeleton.LocalToWorld(local);

        AssertNear(local, skeleton.WorldToLocal(world));
    }

    [Fact]
    public void SpineTransformRoundTripsNegativeScale()
    {
        var skeleton = CreateSkeleton();
        skeleton.Position = new Vector2(-3f, 5f);
        skeleton.Scale = new Vector2(-1.5f, 2f);
        skeleton.Rotation = -0.4f;
        var local = new Vector2(8f, -6f);

        AssertNear(local, skeleton.WorldToLocal(skeleton.LocalToWorld(local)));
    }

    [Fact]
    public void SpineTransformReportsNonInvertibleScale()
    {
        var skeleton = CreateSkeleton();
        skeleton.Scale = new Vector2(0f, 1f);

        Assert.False(skeleton.TryWorldToLocal(Vector2.One, out _));
        Assert.Throws<InvalidOperationException>(() => skeleton.WorldToLocal(Vector2.One));
    }

    [Fact]
    public void CameraRoundTripsScreenAndWorldCoordinates()
    {
        var camera = new Camera2D
        {
            Position = new Vector2(5f, -3f),
            Zoom = 1.7f,
            Rotation = 0.35f,
            PPU = 48f,
            Viewport = new Point2(1920, 1080),
        };
        var world = new Vector2(-12f, 4.25f);

        var screen = camera.WorldToScreen(world);

        Assert.True(camera.TryScreenToWorld(screen, out var restored));
        AssertNear(world, restored);
        AssertNear(world, camera.ScreenToWorld(screen));
    }

    private static SpineSkeleton CreateSkeleton() => new(new SkeletonData());

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.True(
            Vector2.Distance(expected, actual) < 0.0001f,
            $"Expected {expected}, actual {actual}.");
    }
}
