# DragonLib Prowl text extensions

Scribe and Quill source is vendored from ProwlEngine/Anthology **2.7.0**, commit
`acdc59672777b6c747941d6d92acbb3080b1a2ba`, under the adjacent MIT LICENSE.
Paper and Origami remain the original 2.7.0 NuGet packages. Local projects retain
their package IDs and assembly versions so NuGet resolves their transitive
Scribe/Quill dependencies to these projects (do not add a second assembly copy).

Local changes:

- `FontAtlasSettings.DistanceFieldMode` defaults to `Msdf`; `DistanceRange` defaults to 4 atlas pixels.
- `FontSystem` selects dynamic MSDF or legacy SDF generation, retaining the original constructor for binary compatibility.
- `MsdfGlyphGenerator` sends Scribe's glyph-index outlines to the native msdfgen bridge. Metrics, shaping, fallback lookup, wrapping, caret positions and glyph caching remain in Scribe.
- MSDF glyph region bounds match the integer bitmap extent exactly.
- The distance range must be set before glyphs are cached, so one atlas never mixes distance scales.

See `../Msdfgen/README.md` for native build/deployment. No prebuilt font atlas,
external font parser or per-glyph process launch is required.

When upgrading Prowl, update Scribe/Quill/Paper/Origami/Vector together, reapply
the changes above and run `Tests/Paper.Msdf.Tests` and the GPU smoke sample.
