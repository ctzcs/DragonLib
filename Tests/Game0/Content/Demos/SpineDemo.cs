using System.Numerics;
using System.Text;
using DCFApixels.DragonECS;
using Engine;
using Engine.ECS;
using Engine.Spine;
using Engine.World;
using Foster.Framework;
using ImGuiNET;
using Spine;

namespace Game0.Content.Demos;

public sealed class SpineDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string AssetDirectory = "Resources/Spine/SpineBoy";
    private const string AtlasPath = AssetDirectory + "/spineboy-pma.atlas";
    private const string SkeletonPath = AssetDirectory + "/spineboy-ess.json";
    private const float SkeletonDataScale = 0.025f;

    private readonly record struct CameraState(Vector2 Position, float Zoom, float Rotation, float Ppu);

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Camera2D _camera = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private readonly SpineBatchRenderer _renderer = new();
    private StorageContainer? _storage;
    private SpineFosterTextureLoader? _textureLoader;
    private Atlas? _atlas;
    private SpineSkeleton? _spineBoy;
    private Bone? _gunBone;
    private string[] _animationNames = [];
    private Vector2 _skeletonBoundsCenter;
    private CameraState _savedCamera;
    private int _selectedAnimation;
    private bool _wasActive;
    private bool _paused;
    private bool _aimEnabled = true;
    private float _timeScale = 1f;
    private float _scale = 1f;

    public void Init()
    {
        var storage = StorageUtils.GetDevGameRoot;
        _storage = storage;
        _textureLoader = new SpineFosterTextureLoader(
            _game.GraphicsDevice,
            path => storage.OpenRead(NormalizeContainerPath(path)),
            // spineboy-pma.png is already premultiplied; do not multiply it a second time.
            premultiplyAlpha: false);

        using (var atlasStream = storage.OpenRead(AtlasPath))
        using (var atlasReader = new StreamReader(atlasStream, Encoding.UTF8))
            _atlas = new Atlas(atlasReader, AssetDirectory, _textureLoader);

        SkeletonData skeletonData;
        using (var skeletonStream = storage.OpenRead(SkeletonPath))
            skeletonData = SpineSkeletonLoader.LoadJson(skeletonStream, _atlas, SkeletonDataScale);

        _spineBoy = new SpineSkeleton(skeletonData);
        _gunBone = _spineBoy.Skeleton.FindBone("gun");
        _spineBoy.Position = Vector2.Zero;
        _spineBoy.Scale = Vector2.One;

        // Spine uses Y-up while DragonLib's Camera2D uses Y-down.
        _spineBoy.Skeleton.ScaleY = -1f;
        _spineBoy.Skeleton.UpdateWorldTransform(Physics.None);

        _skeletonBoundsCenter = new Vector2(
            skeletonData.X + skeletonData.Width * 0.5f,
            -(skeletonData.Y + skeletonData.Height * 0.5f)) * SkeletonDataScale;

        _animationNames = new string[skeletonData.Animations.Count];
        for (var i = 0; i < _animationNames.Length; i++)
            _animationNames[i] = skeletonData.Animations.Items[i].Name;

        _selectedAnimation = Array.IndexOf(_animationNames, "aim");
        if (_selectedAnimation < 0)
            _selectedAnimation = Array.IndexOf(_animationNames, "idle");
        if (_selectedAnimation < 0)
            _selectedAnimation = 0;

        PlaySelectedAnimation();
    }

    public void Destroy()
    {
        _atlas?.Dispose();
        _atlas = null;

        _textureLoader?.Dispose();
        _textureLoader = null;
        _storage?.Dispose();
        _storage = null;
        _spineBoy = null;
        _gunBone = null;
        _animationNames = [];
    }

    public void Update()
    {
        HandleSceneTransition();
        if (!_wasActive || _spineBoy == null)
            return;

        DrawControls();
        if (!_paused)
            _spineBoy.Update(_game.Time.Delta * _timeScale);

        if (_aimEnabled)
            ApplyAim();
    }

    public void Render()
    {
        if (!_wasActive || _spineBoy == null)
            return;

        _renderer.Draw(_batcher, _spineBoy);
    }

    private void HandleSceneTransition()
    {
        var isActive = _sceneRouter.Current == RuntimeScene.SpineBoy;
        if (isActive && !_wasActive)
        {
            _savedCamera = new CameraState(_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = _spineBoy?.LocalToWorld(_skeletonBoundsCenter) ?? Vector2.Zero;
            _camera.Zoom = 1f;
            _camera.Rotation = 0f;
            _camera.PPU = 32f;
        }
        else if (!isActive && _wasActive)
        {
            _camera.Position = _savedCamera.Position;
            _camera.Zoom = _savedCamera.Zoom;
            _camera.Rotation = _savedCamera.Rotation;
            _camera.PPU = _savedCamera.Ppu;
        }

        _wasActive = isActive;
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Spine Boy");

        if (_animationNames.Length > 0 && ImGui.BeginCombo("Animation", _animationNames[_selectedAnimation]))
        {
            for (var i = 0; i < _animationNames.Length; i++)
            {
                var selected = i == _selectedAnimation;
                if (ImGui.Selectable(_animationNames[i], selected))
                {
                    _selectedAnimation = i;
                    PlaySelectedAnimation();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.Checkbox("Pause", ref _paused);
        ImGui.Checkbox("Aim with mouse", ref _aimEnabled);
        ImGui.SliderFloat("Speed", ref _timeScale, 0.1f, 3f);
        if (ImGui.SliderFloat("Scale", ref _scale, 0.25f, 2f) && _spineBoy != null)
        {
            _spineBoy.Scale = new Vector2(_scale);
            _camera.Position = _spineBoy.LocalToWorld(_skeletonBoundsCenter);
        }
        if (ImGui.Button("Restart animation"))
            PlaySelectedAnimation();

        ImGui.End();
    }

    private void PlaySelectedAnimation()
    {
        if (_spineBoy == null || _animationNames.Length == 0)
            return;

        _spineBoy.SetAnimation(0, _animationNames[_selectedAnimation], loop: true);
    }

    private void ApplyAim()
    {
        if (_spineBoy == null || _gunBone == null ||
            ImGui.GetIO().WantCaptureMouse ||
            !_camera.TryScreenToWorld(_input.Mouse.Position, out var targetWorld) ||
            !_spineBoy.TryWorldToLocal(targetWorld, out var target))
            return;

        var gunLocal = new Vector2(
            _gunBone.AppliedPose.WorldX,
            _gunBone.AppliedPose.WorldY);

        var desiredAngle = MathF.Atan2(
            target.Y - gunLocal.Y,
            target.X - gunLocal.X) * 180f / MathF.PI;
        var delta = WrapDegrees(desiredAngle - _gunBone.AppliedPose.WorldRotationX);

        // AnimationState.Apply runs before this method, so this delta is reapplied
        // every frame on top of the current animation pose.
        _gunBone.Pose.Rotation += delta;
        _spineBoy.Skeleton.UpdateWorldTransform(Physics.None);
    }

    private static float WrapDegrees(float value)
    {
        value %= 360f;
        if (value > 180f)
            value -= 360f;
        else if (value < -180f)
            value += 360f;
        return value;
    }

    private static string NormalizeContainerPath(string path) => path.Replace('\\', '/');
}
