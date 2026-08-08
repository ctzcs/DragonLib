# DragonLib.Spine

Foster rendering and animation integration for the official `spine-csharp` runtime.

The official runtime is included directly through the `spine-csharp/spine-csharp.csproj` project reference. The runtime project must be from the same major/minor version as the Spine data exported by the editor. The runtime license and Spine Editor license requirements apply to applications using this module.

The renderer supports region attachments, mesh attachments, animation state updates, skins, atlas pages, normal/additive/multiply/screen blending, and official `SkeletonClipping` polygon clipping. Two-color tinting still requires a Foster-specific vertex format and shader; the current renderer intentionally falls back to the slot light color for that case.
