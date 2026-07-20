using System.Numerics;
using DCFApixels.DragonECS;
using Engine.ECS;
using ImGuiNET;

namespace Game0;


public class SceneModule : EcsModule<SceneModule>
{
    public override void Import(EcsPipeline.Builder b)
    {
        b.Add(new SceneLauncherSystem());
        b.Add(new DreamBlockDemoSystem());
    }
}

public enum RuntimeScene
{
    Main,
    DreamBlockShader,
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
        }
        else if (ImGui.Button("Back to Main"))
        {
            _sceneRouter.SwitchTo(RuntimeScene.Main);
        }

        ImGui.End();
    }
}