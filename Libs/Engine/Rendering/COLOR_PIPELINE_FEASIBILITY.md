# 色彩管线后端调查（2026-10-03，阶段 2 开工前）

## 可行性

SDL_GPU 提供 `R8G8B8A8_UNORM_SRGB` 与 `R16G16B16A16_FLOAT`，官方列为采样和颜色附件的通用支持格式；仍沿用 Foster 的 `SDL_GPUTextureSupportsFormat` 查询实际设备。
纹理需分配完整 mip 链；上传结束后关闭 copy pass，再在命令缓冲上调用 `SDL_GenerateMipmapsForGPUTexture`。该函数不能在任何 pass 内调用。Foster 目前分开管理上传与绘制命令缓冲，生成操作应跟随上传缓冲，保持上传先于生成、生成先于绘制。

WebGL2 原生支持 `SRGB8_ALPHA8` 采样、颜色附件和半浮点纹理过滤；`RGBA16F` 颜色附件必须启用 `EXT_color_buffer_float`，缺失时 HDR 回退到 RGBA8。能力查询必须反映扩展结果。WebGL2 可生成非二次幂大小的 mip 链；深度和不可过滤/不可渲染格式不能用通用生成路径。本次只允许非多重采样颜色纹理开启 mipmap，默认关闭，保持现有 2D 行为。

当前 SDL swapchain 使用 RGBA8 UNORM，WebGL 默认 framebuffer 同样接收已编码颜色，tonemap 输出需显式做 sRGB 编码；若调用方合成到 sRGB 颜色附件，则关闭 shader 编码，由附件转换。

## 最小实施范围

- 新格式追加 enum 值，保持 Web JSON 中原有数值不变；颜色大小分别为 4/8 字节。
- `TextureFlags.GenerateMipmaps` 只用于采样纹理，纹理上传后重建 mip 链；显式 mip 过滤 sampler 默认关闭。
- glTF albedo 是 sRGB，法线是线性数据。HDR 在纹理采样后使用线性值；LDR 路径将 sRGB 采样结果重新编码以保持原先 gamma 光照观感。
- HDR 目标选择 RGBA16F，tonemap 支持 ACES/Reinhard 和曝光，LDR 保留 Batcher 合成。
- 颜色与数据用途共用一张源图时不能用同一 sRGB 解码结果处理所有用途；加载器以材质用途记录格式，后续材质扩展需要保持各用途的语义。

## 来源与代码定位

- [SDL_GPUTextureFormat](https://wiki.libsdl.org/SDL3/SDL_GPUTextureFormat)：格式及采样/颜色附件支持。
- [SDL_GenerateMipmapsForGPUTexture](https://wiki.libsdl.org/SDL3/SDL_GenerateMipmapsForGPUTexture)：完整 mip 链与 pass 外调用。
- [WebGL2 规范](https://registry.khronos.org/webgl/specs/latest/2.0/)：sRGB、半浮点与 NPOT mipmap。
- [EXT_color_buffer_float](https://registry.khronos.org/webgl/extensions/EXT_color_buffer_float/)：RGBA16F 颜色附件扩展。
- `Libs/Foster/Framework/Internal/GraphicsDeviceSDL.cs`：纹理上传、copy pass 与 sampler。
- `Libs/Foster.Web/Framework/GraphicsDeviceWeb.cs`、`Libs/Foster.Web/Web/foster.js`：格式映射、WebGL 纹理和 framebuffer。

运行时画面验证仍需在桌面和实际浏览器中确认；构建通过不能证明目标设备的扩展可用。

## 执行记录与实际差异

已实现格式、mipmap、sRGB 用途区分、HDR 回退、ACES/Reinhard 合成及 GltfScene 的 HDR/曝光开关。
颜色和数据用途共享源图时分别上传，防止法线与 MR 数据被 sRGB 解码。
桌面和 browser 构建通过；101 项实体/渲染测试和 3 项 Engine 测试通过；GLSL ES 使用 glslangValidator 校验，五组 shader 哈希清单通过。

额外 D3D12 GPU smoke 读回 HDR 亮度 4（未钳到 1），ACES 合成峰值 252/255，4×4 sRGB 贴图生成三层 mip。
但这个验证暴露现有绕序冲突：同一个朝 +Z 的 CCW 三角形，相机从 +Z 看它，Cull.Back 不绘制，Cull.Front 绘制。
换用迁移前相同的 SPIR-V→DXIL 构建路径仍可复现，排除了新 shader 去重或 HDR 改动。
Rendering README 对 SDL front-face 的解释与实际设备结果不符。计划要求 CCW，也要求未注明的设计取舍先询问；已询问用户选择保留 CCW 修正后端，还是保留后端并调整资产约定。
阶段 2 曾按 CCW 要求把 SDL front_face 改为 COUNTER_CLOCKWISE，四组对照在 D3D12 与 Vulkan 通过。
用户随后决定优先遵循 Foster 框架：已恢复上游 CLOCKWISE，改由 Engine 的 MeshUpload3D 在桌面上传时转换索引，Web 保留 CCW。
资产文件、CPU 几何和法线保持原值。永久 GPU 验证位于相邻测试仓库 `Rendering3D.Smoke/`，分别锁定原生 CW 设置和 Engine 上传适配，另检查静态/蒙皮加载及源索引不变。

## 阶段性回退记录（2026-10-04，已被后续 MyFoster 同步取代）

用户明确 `Libs/Foster` 为第三方库，本计划对它的六个文件修改全部恢复到计划前提交 `0466793`。
恢复范围包括纹理格式/flags、Texture mip 逻辑、TextureSampler、DrawCommand 图元扩展和 SDL 后端。
对该提交比较 `Libs/Foster` 的差异为零；本轮未新增提交，也未改写此前阶段提交。
桌面 Engine 使用 LDR 普通 Color 纹理，不支持本计划新增的 HDR/硬件 sRGB/mipmap；调试线由 Engine 展开三角形带。
Web 后端获准保留：五个共享类型的扩展移入 Foster.Web/Framework 同名覆盖文件，Web 自身的 GPU/JS 扩展继续使用。
Engine 的 TextureSupport3D 按构建目标区分能力，CCW 源索引仍通过 MeshUpload3D 适配桌面 Foster 的默认 CW。

### 动画 palette 的 Vulkan 约束（阶段 6 补充）

SDL 3.4.0 的 [Vulkan 后端源码](https://github.com/libsdl-org/SDL/blob/release-3.4.0/src/gpu/vulkan/SDL_gpu_vulkan.c) 定义 `MAX_UBO_SECTION_SIZE = 4096`，绑定 uniform descriptor 时将 range 固定为该值。实测单块 8KB palette 在 D3D12 正常，在 Vulkan 无法读取第 127 号关节。为保持 128 关节能力和 WebGL2 兼容，将 palette 拆为 vertex b2/b3 各 64 个矩阵（4KB），顶点阶段总共四个槽位。颜色和蒙皮深度的第 127 号关节读回在两驱动均通过。

## 当前实现：通过 MyFoster 恢复完整能力（2026-10-04）

用户授权必要定制先在独立 Foster 仓库的 MyFoster 分支提交、验证并推送，再同步到 DragonLib。
当前同步版本为 `08c3d7f999c9ea4cfd730ad3b608d5ef6bff4517`，上游基线为 Foster 0.4.2 的 `730cc6a`；来源与比较链接见 [Foster/UPSTREAM.md](../../Foster/UPSTREAM.md)。
应用配置/输入时序、HDR/sRGB/mipmap/线段拓扑、浏览器字体兼容分别保留独立提交。
`Libs/Foster` 的 168 个来源文件与该版本逐字节一致，仅增加本地来源记录；不在该副本直接维护定制。
桌面恢复 HDR、硬件 sRGB、mipmap 和原生调试线；Web 复用共享图形类型，独立适配新版 ContentStorage 和 GraphicsDevice 接口。
Foster 默认 CW 正面保持不变，Engine 上传 CCW 模型索引时适配；资产与 CPU 几何仍为 CCW。

桌面解决方案、Engine browser 和独立 WebDemo 构建通过；128 项 CPU 测试及六组 shader 哈希校验通过。
D3D12/Vulkan 使用同步后的 Foster 0.4.2 / SDL 3.4.12，HDR 读回 4、硬件 sRGB 误差小于 0.01、4×4 mip 链三层共 84 字节、ACES 峰值 252。
半白半黑 sRGB 贴图通过大 UV 梯度强制采样最小 mip，读回 2.015625（线性期望 2，容差 0.03），确认实际生成与 mip 过滤路径。
八组绕序/剔除、静态/蒙皮加载、源索引不变、简单/CSM 阴影、线段减少后的缓冲计数、关节 127 颜色/深度验证均通过。
浏览器/Metal 实际画面仍待验证。
