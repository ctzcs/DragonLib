using System.Diagnostics;
using System.Numerics;
using DCFApixels.DragonECS;
using Engine;
using Engine.DearImGui;
using Engine.ECS;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

internal static class Program
{
    public static void Main(string[] args)
    {
        //这里设置的是窗口大小
        using var gameContent = new MyGame(new AppConfig(
            "Game",
            "Game",
            2560,
            1440, Flags: AppFlags.GraphicsDebugging));
        gameContent.Run();

    }
}


public class MyGame : GameApp
{
    private EcsWorld _world;
    private EcsEventWorld _eventWorld;
    private EcsPipeline _pipeline;
    private Batcher _batcher;
    private Camera2D _camera;
    private Renderer _imGui;
    public MyGame(in AppConfig config) : base(in config)
    {
        _batcher = new Batcher(this.GraphicsDevice);
        _camera = new Camera2D();
        _imGui = new Renderer(this);
    }

    protected override void Startup()
    {
        _world = new EcsDefaultWorld();
        _eventWorld = new EcsEventWorld();

        _pipeline = EcsPipeline.New()
            .Inject(_world)
            .Inject(_eventWorld)
            .Inject(_batcher)
            .Inject(_camera)
            .Inject(_imGui)
            .AddModule(new SimpleModule())
            .AddModule(new TestModule())
            .AddModule(new DebugInspectorModule())
            .Add(new EcsInspectorSystem(() => { Log.Info("Register Custom Drawer");}))
            .AutoInject()
            .BuildAndInit();
        
        
    }

    protected override void Shutdown()
    {

    }

    protected override void Update()
    {
        _imGui.BeginLayout();
        if (_imGui.WantsTextInput)
            Window.StartTextInput();
        else
            Window.StopTextInput();
        _pipeline.Update();
        _imGui.EndLayout();
        
    }

    protected override void Render()
    {
        Window.Clear(Color.AliceBlue);
        _batcher.PushMatrix(_camera.GetMatrix(Window));
        _pipeline.Render();
        _batcher.Render(Window);
        _batcher.Clear();
        _imGui.Render();
    }
}




public class SimpleModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new SimpleSystem());
    }
}

public class DebugInspectorModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new CameraDebugSystem());
    }
}


public class TestModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new ManySystemsStressSystem());
    }
}

public class SimpleSystem : IUpdateSystem, IRenderSystem
{
    [DI] private EcsDefaultWorld _world;
    [DI] private EcsEventWorld _ecsEventWorld;
    [DI] private Batcher _batcher;
    [DI] private Camera2D _camera;
    public void Update()
    {
        Log.Info($"Udpate {_world.Name} {_ecsEventWorld.Name}");
    }

    public void Render()
    {
        _batcher.Quad(new Quad(new Rect(-1, -1, 2, 2)), Color.Black);
    }
}



public class CameraDebugSystem : IUpdateSystem
{
    [DI] private Camera2D _camera;

    public void Update()
    {
        ImGui.Begin("Camera");
        ImGui.DragFloat("Pos X", ref _camera.Position.X, 0.1f);
        ImGui.DragFloat("Pos Y", ref _camera.Position.Y, 0.1f);
        ImGui.DragFloat("Zoom", ref _camera.Zoom, 0.01f, 0.1f, 10f);
        ImGui.DragFloat("Rotation", ref _camera.Rotation, 0.01f, -MathF.PI, MathF.PI);
        ImGui.DragFloat("PPU", ref _camera.PPU, 0.5f, 1f, 128f);
        ImGui.End();
    }
}


public struct PositionComp : IEcsComponent
{
    public Vector2 Value;
}

public struct VelocityComp : IEcsComponent
{
    public Vector2 Value;
}

public struct TagAComp : IEcsTagComponent { }
public struct TagBComp : IEcsTagComponent { }

public sealed class MoverAspect : EcsAspect
    {
        public readonly EcsPool<PositionComp> Pos = B.IncludePool<EcsPool<PositionComp>>();
        public readonly EcsPool<VelocityComp> Vel = B.IncludePool<EcsPool<VelocityComp>>();
    }

public struct GroupTag<T> : IEcsTagComponent { }

public struct G0 { } public struct G1 { } public struct G2 { } public struct G3 { }
public struct G4 { } public struct G5 { } public struct G6 { } public struct G7 { }
public struct G8 { } public struct G9 { }

public sealed class GroupAspect<T> : EcsAspect where T : struct
{
    public readonly EcsPool<PositionComp> Pos = B.IncludePool<EcsPool<PositionComp>>();
    public readonly EcsPool<VelocityComp> Vel = B.IncludePool<EcsPool<VelocityComp>>();
    public readonly EcsTagPool<GroupTag<T>> Tag = B.IncludePool<EcsTagPool<GroupTag<T>>>();
}

public class ManySystemsStressSystem : IEcsPreInit, IUpdateSystem
{
    [DI] private EcsDefaultWorld _world;

    private int _noiseEntities = 50_000;
    private int _entitiesPerGroup = 100;
    private int _matched;
    private double _totalMs;
    private double _perGroupUs;

    public void PreInit() => Rebuild();

    private void Rebuild()
    {
        var span = _world.ToSpan();
        for (int i = span.Count - 1; i >= 0; i--)
            _world.DelEntity(span[i]);

        var posPool = _world.GetPool<PositionComp>();
        var velPool = _world.GetPool<VelocityComp>();
        var rng = new Random(42);

        for (int i = 0; i < _noiseEntities; i++)
        {
            var e = _world.NewEntity();
            posPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            if (i % 2 == 0)
                velPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
        }

        AddGroup<G0>(rng); AddGroup<G1>(rng); AddGroup<G2>(rng); AddGroup<G3>(rng); AddGroup<G4>(rng);
        AddGroup<G5>(rng); AddGroup<G6>(rng); AddGroup<G7>(rng); AddGroup<G8>(rng); AddGroup<G9>(rng);
    }

    private void AddGroup<T>(Random rng) where T : struct
    {
        var posPool = _world.GetPool<PositionComp>();
        var velPool = _world.GetPool<VelocityComp>();
        var tagPool = _world.GetPool<GroupTag<T>>();

        for (int i = 0; i < _entitiesPerGroup; i++)
        {
            var e = _world.NewEntity();
            posPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            velPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            tagPool.Add(e);
        }
    }

    private int RunGroup<T>() where T : struct
    {
        int count = 0;
        foreach (var e in _world.Where(out GroupAspect<T> a))
        {
            ref var p = ref a.Pos.Get(e);
            ref var v = ref a.Vel.Get(e);
            p.Value += v.Value * 0.016f;
            count++;
        }
        return count;
    }

    public void Update()
    {
        var sw = Stopwatch.StartNew();
        int matched = 0;
        matched += RunGroup<G0>(); matched += RunGroup<G1>(); matched += RunGroup<G2>();
        matched += RunGroup<G3>(); matched += RunGroup<G4>(); matched += RunGroup<G5>();
        matched += RunGroup<G6>(); matched += RunGroup<G7>(); matched += RunGroup<G8>();
        matched += RunGroup<G9>();
        sw.Stop();

        _totalMs = sw.Elapsed.TotalMilliseconds;
        _perGroupUs = _totalMs * 1000.0 / 10.0;
        _matched = matched;

        ImGui.Begin("Many Systems Stress");
        ImGui.Text($"Noise Entities   : {_noiseEntities}");
        ImGui.Text($"Groups (Systems) : 10");
        ImGui.Text($"Entities per Grp : {_entitiesPerGroup}");
        ImGui.Text($"Matched Total    : {_matched}");
        ImGui.Separator();
        ImGui.Text($"Total  : {_totalMs:F3} ms");
        ImGui.Text($"Per Sys: {_perGroupUs:F2} us");
        ImGui.Text($"FPS    : {ImGui.GetIO().Framerate:F1}");
        ImGui.Separator();
        ImGui.DragInt("Noise Entities", ref _noiseEntities, 1000, 0, 1_000_000);
        ImGui.DragInt("Entities/Group", ref _entitiesPerGroup, 10, 1, 10000);
        if (ImGui.Button("Rebuild"))
            Rebuild();
        ImGui.End();
    }
}