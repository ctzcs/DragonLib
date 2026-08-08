using System.Numerics;
using Foster.Framework;
using Spine;

namespace DragonLib.Spine;

/// <summary>Owns a Spine skeleton instance and its animation state.</summary>
public sealed class SpineSkeleton
{
    private Transform _worldTransform = Transform.Identity;

    public Skeleton Skeleton { get; }
    public AnimationState AnimationState { get; }

    public Vector2 Position
    {
        get => _worldTransform.Position;
        set => _worldTransform.Position = value;
    }

    public Vector2 Scale
    {
        get => _worldTransform.Scale;
        set => _worldTransform.Scale = value;
    }

    public float Rotation
    {
        get => _worldTransform.Rotation;
        set => _worldTransform.Rotation = value;
    }

    public Matrix3x2 WorldMatrix => _worldTransform.Matrix;

    public SpineSkeleton(SkeletonData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Skeleton = new Skeleton(data);
        AnimationState = new AnimationState(new AnimationStateData(data));
        Skeleton.UpdateWorldTransform(Physics.None);
    }

    public TrackEntry SetAnimation(int track, string name, bool loop = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return AnimationState.SetAnimation(track, name, loop);
    }

    public void ClearTrack(int track) => AnimationState.ClearTrack(track);

    public Vector2 LocalToWorld(Vector2 localPosition)
        => Vector2.Transform(localPosition, WorldMatrix);

    public Vector2 WorldToLocal(Vector2 worldPosition)
    {
        if (!TryWorldToLocal(worldPosition, out var localPosition))
            throw new InvalidOperationException("The Spine world transform is not invertible.");

        return localPosition;
    }

    public bool TryWorldToLocal(Vector2 worldPosition, out Vector2 localPosition)
    {
        if (!Matrix3x2.Invert(WorldMatrix, out var inverse))
        {
            localPosition = default;
            return false;
        }

        localPosition = Vector2.Transform(worldPosition, inverse);
        return true;
    }

    public void Update(float deltaSeconds, Physics physics = Physics.None)
    {
        if (deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        AnimationState.Update(deltaSeconds);
        AnimationState.Apply(Skeleton);
        Skeleton.UpdateWorldTransform(physics);
    }
}
