using System.Numerics;
using Box2D.NET;
using DragonLib.Box2D;
using Xunit;
using static Box2D.NET.B2Worlds;

namespace Game0.EngineTests;

public sealed class Box2DIntegrationTests
{
    [Fact]
    public void VectorAndRotationConversionsRoundTrip()
    {
        var vector = new Vector2(12.5f, -3.25f);
        const float rotation = 0.73f;

        Assert.Equal(vector, vector.ToBox2D().ToVector2());
        Assert.True(MathF.Abs(rotation - rotation.ToBox2DRotation().ToRadians()) < 0.0001f);
    }

    [Fact]
    public void WorldOwnsAndReleasesBox2DHandle()
    {
        var world = new Box2DWorld(new Vector2(0f, 9.81f));
        var worldId = world.WorldId;

        Assert.True(b2World_IsValid(worldId));
        world.Step(1f / 60f);

        world.Dispose();

        Assert.True(world.IsDisposed);
        Assert.False(b2World_IsValid(worldId));
        Assert.Throws<ObjectDisposedException>(() => world.Step(1f / 60f));
    }
}
