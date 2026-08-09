using System.Numerics;
using Box2D.NET;
using DCFApixels.DragonECS;
using DragonLib.Box2D;
using Engine.ECS;
using Engine.Threading;
using Engine.World;
using Foster.Framework;
using ImGuiNET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Constants;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Game0.Content.Demos;

/// <summary>Small Box2D simulation demo rendered with Foster's Batcher.</summary>
public sealed class Box2DDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const float ContainerLeft = -8.6f;
    private const float ContainerRight = 8.6f;
    private const float ContainerTop = -1.5f;
    private const float ContainerBottom = 4.5f;
    private static readonly int FallbackMaxWorkerCount = Math.Min(Environment.ProcessorCount, B2_MAX_WORKERS);

    private readonly record struct CameraState(Vector2 Position, float Zoom, float Rotation, float Ppu);

    private readonly record struct BodyVisual(
        B2BodyId Id,
        Vector2 HalfSize,
        float Radius,
        Color Color,
        bool IsCircle);

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Camera2D _camera = null!;
    [DI] private MyGame _game = null!;
    [DI] private JobScheduler _jobScheduler = null!;

    private readonly List<BodyVisual> _bodies = [];
    private Box2DWorld? _world;
    private CameraState _savedCamera;
    private bool _wasActive;
    private bool _paused;
    private int _spawnIndex;

    private int _boxCount = 12;
    private int _circleCount = 8;
    private int _workerCount = 1;

    public void Init()
    {
    }

    public void Destroy()
    {
        DestroyWorld();
    }

    public void Update()
    {
        HandleSceneTransition();
        if (!_wasActive || _world == null)
            return;

        DrawControls();
        if (!_paused)
            _world.Step(_game.Time.Delta, subStepCount: 4);
    }

    public void Render()
    {
        if (!_wasActive || _world == null)
            return;

        foreach (var visual in _bodies)
        {
            if (!b2Body_IsValid(visual.Id))
                continue;

            var position = b2Body_GetPosition(visual.Id).ToVector2();
            var rotation = b2Body_GetRotation(visual.Id).ToRadians();
            _batcher.PushMatrix(position, Vector2.One, rotation);
            try
            {
                if (visual.IsCircle)
                {
                    _batcher.Circle(Vector2.Zero, visual.Radius, 20, visual.Color);
                    _batcher.CircleLine(Vector2.Zero, visual.Radius, 0.04f, 20, Color.White);
                }
                else
                {
                    var size = visual.HalfSize * 2f;
                    _batcher.Rect(-visual.HalfSize, size, visual.Color);
                    _batcher.RectLine(-visual.HalfSize, size, 0.04f, Color.White);
                }
            }
            finally
            {
                _batcher.PopMatrix();
            }
        }
    }

    private void HandleSceneTransition()
    {
        var isActive = _sceneRouter.Current == RuntimeScene.Box2D;
        if (isActive && !_wasActive)
        {
            _savedCamera = new CameraState(_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = new Vector2(0f, 1.5f);
            _camera.Zoom = 1f;
            _camera.Rotation = 0f;
            _camera.PPU = 64f;
            CreateWorld();
        }
        else if (!isActive && _wasActive)
        {
            DestroyWorld();
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
        ImGui.Begin("Box2D");

        ImGui.Checkbox("Pause", ref _paused);
        int maxWorkerCount = Math.Max(1, Math.Min(_jobScheduler?.WorkerCount ?? FallbackMaxWorkerCount, B2_MAX_WORKERS));
        _workerCount = Math.Clamp(_workerCount, 1, maxWorkerCount);
        ImGui.SliderInt("Workers", ref _workerCount, 1, maxWorkerCount);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            CreateWorld();
        }

        ImGui.SliderInt("Box Count", ref _boxCount, 0, 10000);
        ImGui.SliderInt("Circle Count", ref _circleCount, 0, 10000);
        ImGui.Text($"Dynamic bodies: {_boxCount + _circleCount}");

        if (ImGui.Button("Reset simulation"))
        {
            CreateWorld();
        }

        ImGui.End();
    }

    private void CreateWorld()
    {
        DestroyWorld();
        _world = _workerCount > 1
            ? new Box2DWorld(new Vector2(0f, 9.81f), _jobScheduler, _workerCount)
            : new Box2DWorld(new Vector2(0f, 9.81f));
        _bodies.Clear();
        _spawnIndex = 0;

        CreateBox(new Vector2(0f, 5.2f), new Vector2(9f, 0.35f), B2BodyType.b2_staticBody, new Color(0x364250));
        CreateBox(new Vector2(0f, -2.2f), new Vector2(9f, 0.35f), B2BodyType.b2_staticBody, new Color(0x364250));
        CreateBox(new Vector2(-9.35f, 1.5f), new Vector2(0.35f, 3.7f), B2BodyType.b2_staticBody, new Color(0x364250));
        CreateBox(new Vector2(9.35f, 1.5f), new Vector2(0.35f, 3.7f), B2BodyType.b2_staticBody, new Color(0x364250));

        PopulateDynamicBodies();
    }

    private void PopulateDynamicBodies()
    {
        var totalCount = _boxCount + _circleCount;
        if (totalCount == 0)
            return;

        var availableWidth = ContainerRight - ContainerLeft;
        var availableHeight = ContainerBottom - ContainerTop;
        var columns = Math.Clamp(
            (int)MathF.Ceiling(MathF.Sqrt(totalCount * availableWidth / availableHeight)),
            1,
            totalCount);
        var rows = (totalCount + columns - 1) / columns;
        var cellWidth = availableWidth / columns;
        var cellHeight = availableHeight / rows;
        var bodyExtent = MathF.Min(0.42f, MathF.Min(cellWidth, cellHeight) * 0.34f);

        var boxesPlaced = 0;
        for (var index = 0; index < totalCount; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var itemsInRow = Math.Min(columns, totalCount - row * columns);
            var rowWidth = itemsInRow * cellWidth;
            var position = new Vector2(
                -rowWidth * 0.5f + cellWidth * (column + 0.5f),
                ContainerTop + cellHeight * (row + 0.5f));

            var targetBoxCount = (int)MathF.Round((index + 1f) * _boxCount / totalCount);
            if (boxesPlaced < targetBoxCount)
            {
                CreateBox(position, new Vector2(bodyExtent), B2BodyType.b2_dynamicBody, new Color(0xe78b4a));
                boxesPlaced++;
            }
            else
            {
                CreateCircle(position, bodyExtent, new Color(0x48b8c8));
            }
        }
    }

    private void CreateBox(Vector2 position, Vector2 halfSize, B2BodyType type, Color color)
    {
        if (_world == null)
            return;

        var bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = type;
        bodyDefinition.position = position.ToBox2D();
        bodyDefinition.name = type == B2BodyType.b2_staticBody ? "Static boundary" : $"Box {_spawnIndex++}";
        var body = b2CreateBody(_world.WorldId, bodyDefinition);

        var shapeDefinition = b2DefaultShapeDef();
        var polygon = b2MakeBox(halfSize.X, halfSize.Y);
        b2CreatePolygonShape(body, shapeDefinition, polygon);
        _bodies.Add(new BodyVisual(body, halfSize, 0f, color, false));
    }

    private void CreateCircle(Vector2 position, float radius, Color color)
    {
        if (_world == null)
            return;

        var bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = position.ToBox2D();
        bodyDefinition.name = $"Circle {_spawnIndex++}";
        var body = b2CreateBody(_world.WorldId, bodyDefinition);

        var shapeDefinition = b2DefaultShapeDef();
        var circle = new B2Circle(new B2Vec2(0f, 0f), radius);
        b2CreateCircleShape(body, shapeDefinition, circle);
        _bodies.Add(new BodyVisual(body, Vector2.Zero, radius, color, true));
    }

    private void DestroyWorld()
    {
        _bodies.Clear();
        _world?.Dispose();
        _world = null;
    }
}
