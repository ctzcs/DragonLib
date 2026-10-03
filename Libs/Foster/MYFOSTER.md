# MyFoster customizations

This branch tracks the official `FosterFramework/Foster` repository through the
`upstream` remote. Its customization baseline is
`730cc6aec20068bf5cc57235b7aa70957f5d9035` (Foster 0.4.2).

DragonLib consumes a pinned copy of this branch. Framework changes belong here
first; synchronize the copy only after testing and pushing `origin/MyFoster`.
Inspect customizations with `git diff upstream/main...MyFoster -- Framework` and
`git log upstream/main..MyFoster` after fetching upstream.

## Application behavior retained from DragonLib

- `AppFlags.NoWindowFocus` requests a window that does not activate when shown.
- Update transient input state before polling each frame's SDL events. Startup
  keeps the upstream order of polling once before initializing input state.
- Rasterize SpriteFont glyphs synchronously in browsers, which cannot block on
  worker tasks. Desktop keeps the upstream parallel path and its alpha/pixel
  settings. This preserves DragonLib's existing browser font behavior.

## 3D rendering extensions

- Append sRGB RGBA8 and floating point RGBA16 formats, preserving existing enum
  values and default color texture behavior.
- Opt-in `TextureFlags.GenerateMipmaps` creates the complete mip chain and
  regenerates it after CPU uploads, outside the SDL copy pass. Depth textures,
  render target attachments, and multisampled textures cannot request this flag.
- `TextureSampler.Mipmaps` enables linear mip filtering. Existing samplers remain
  limited to their base level by default.
- Add triangle-list and line-list topology to draw commands and pipeline keys.
  The existing clockwise front-face setting remains unchanged.
- Texture memory accounting includes mip levels; mip counts use integer log2.

## Validation (2026-10-04)

`dotnet build Framework/Foster.Framework.csproj` succeeds without warnings.
DragonLib's Engine and `Rendering3D.Smoke` were built against this independent
project via `-p:FosterFrameworkProject=<path>/Framework/Foster.Framework.csproj`.
D3D12 and Vulkan GPU readbacks verify native CW culling, Engine index adaptation,
HDR value 4, ACES output 252/255, hardware sRGB decoding, mip count/memory,
static/skinned uploads, simple/cascaded shadows, shrinking line buffers, and
joint 127 in color and depth passes. Browser and Metal rendering still require
separate visual validation.
