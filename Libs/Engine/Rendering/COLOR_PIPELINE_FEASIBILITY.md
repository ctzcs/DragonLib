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
用户随后明确正面应为 CCW；修正 SDL front_face 为 COUNTER_CLOCKWISE。固定几何的四组对照在 D3D12 与 Vulkan 全部通过。永久 GPU 验证位于相邻测试仓库 `Rendering3D.Smoke/`。WebGL 已有与离屏 shader y 翻转配套的正面设置，保持该设置。
