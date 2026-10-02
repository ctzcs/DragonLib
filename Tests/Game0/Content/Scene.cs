using System.Numerics;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.World;
using Game0.Content.Demos;
using ImGuiNET;

namespace Game0;


public class SceneModule : EcsModule<SceneModule>
{
    public override void Import(EcsPipeline.Builder b)
    {
        b.Add(new SceneLauncherSystem());
        b.Add(new Transform3DSystem());
        b.Add(new AnimationSystem());
        b.Add(new DreamBlockDemoSystem());
        b.Add(new FishSdfDemoSystem());
        b.Add(new MachineGunSdfDemoSystem());
        b.Add(new GutWallDemoSystem());
        b.Add(new SpineDemoSystem());
        b.Add(new Box2DDemoSystem());
        b.Add(new DualCamera2DDemoSystem());
        b.Add(new ThreeDDemoSystem());
        b.Add(new GltfModelDemoSystem());
        b.Add(new GltfSceneDemoSystem());
        b.Add(new LightSandboxDemoSystem());
        b.Add(new RadianceCascadesDemoSystem());
        b.Add(new SkinningDemoSystem());
        b.Add(new EntitiesDemo());
    }
}

public enum RuntimeScene
{
    Main,
    DreamBlockShader,
    FishSdfShader,
    MachineGunSdfShader,
    SpineBoy,
    Box2D,
    DualCamera2D,
    ThreeD,
    GltfModel,
    GltfScene,
    LightSandbox,
    RadianceCascades2D,
    Skinning,
    GutWall,
}

public sealed class SceneLauncherSystem : IUpdateSystem
{
    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;

    public void Update()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 16f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(240f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Scene Tests");

        if (_sceneRouter.Current == RuntimeScene.Main)
        {
            if (ImGui.Button("Dream Block Shader"))
                _sceneRouter.SwitchTo(RuntimeScene.DreamBlockShader);

            if (ImGui.Button("Fish SDF Shader"))
                _sceneRouter.SwitchTo(RuntimeScene.FishSdfShader);

            if (ImGui.Button("Machine Gun SDF Shader"))
                _sceneRouter.SwitchTo(RuntimeScene.MachineGunSdfShader);

            if (ImGui.Button("Procedural Gut Wall"))
                _sceneRouter.SwitchTo(RuntimeScene.GutWall);

            if (ImGui.Button("Spine Boy"))
                _sceneRouter.SwitchTo(RuntimeScene.SpineBoy);

            if (ImGui.Button("Box2D"))
                _sceneRouter.SwitchTo(RuntimeScene.Box2D);

            if (ImGui.Button("Dual Camera 2D"))
                _sceneRouter.SwitchTo(RuntimeScene.DualCamera2D);

            if (ImGui.Button("3D Mountain Demo"))
                _sceneRouter.SwitchTo(RuntimeScene.ThreeD);

            if (ImGui.Button("glTF Model Demo"))
                _sceneRouter.SwitchTo(RuntimeScene.GltfModel);

            if (ImGui.Button("glTF Scene (ECS)"))
                _sceneRouter.SwitchTo(RuntimeScene.GltfScene);

            if (ImGui.Button("Lighting Sandbox"))
                _sceneRouter.SwitchTo(RuntimeScene.LightSandbox);

            if (ImGui.Button("Radiance Cascades 2D"))
                _sceneRouter.SwitchTo(RuntimeScene.RadianceCascades2D);

            if (ImGui.Button("Skinning (GPU + ECS)"))
                _sceneRouter.SwitchTo(RuntimeScene.Skinning);
        }
        else if (ImGui.Button("Back to Main"))
        {
            _sceneRouter.SwitchTo(RuntimeScene.Main);
        }

        ImGui.End();
    }
}
