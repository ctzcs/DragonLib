# 工作目录与开发约定

- 本仓库 `DragonLib` 维护 `Libs/` 和 `Tools/`；给 AI 使用的提示词与开发约定文档放在 `Prompts/`。
- Game0 已迁到同级独立仓库 `../DragonLib.Tests/Game0`。示例、游戏资源、测试和 smoke 程序只能写在 `../DragonLib.Tests`，不要在本仓库重新创建 `Game0` 或 `Tests/Game0`。
- 两个仓库分别提交自己的改动；保留用户的未提交修改，不混入功能提交。
- 优先在 Engine 适配。确需修改 Foster 时，在独立 `../Foster` 的 `MyFoster` 分支修改、验证、推送，再同步固定提交；不直接修改本仓库的 vendored Foster。
- 用户当前要求：后续 3D 功能分阶段实现，提供独立于 ECS 的库 API，不增加 ECS 接入。示例可以使用 DragonLib.Tests 现有运行外壳。
- 按改动风险做必要验证，避免大量新测试或反复运行全量测试；shader 改动须编译四种后端产物并检查哈希。画面改动优先一次有针对性的实际 GPU 验证。
