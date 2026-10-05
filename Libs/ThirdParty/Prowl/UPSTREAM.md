# Anthology source baseline

- Repository: https://github.com/ProwlEngine/Anthology
- Version: **3.6.7**
- Commit: **5df3a74f8a4d2357f8d6a1dbef1e72fc0fbf46e0**
- Imported: 2026-10-05
- Source archive SHA-256: `3cf785aa8de5db9f7ebe101542927b6b1b2eaeff300e7dda66840da216480344`
- Previous UI baseline: **2.7.0**, `acdc59672777b6c747941d6d92acbb3080b1a2ba`.

## Source layout

For Vector, Echo, Scribe, Quill, Paper, Origami, Clay, Motion, Recast, Aperture,
Rosetta, Scaffold, Quire, Unwrapper and Photonic, upstream `<module>/<module>/`
is flattened into the corresponding directory here. `Echo/Echo.SourceGenerator/`
becomes `Echo.SourceGenerator/`. Ember retains its `Ember/`, `Contracts/` and
`Analyzers/` subdirectories under `Ember/`.

Module README files are preserved as `README.upstream.md`. Their original
relative links and commands describe the upstream repository layout, not this
flattened checkout. Samples, test fixtures and upstream test projects are not
vendored. Retrieve them from the pinned commit when comparing upstream behavior.
Clay/Motion/Photonic retain their module build settings. All project references
are remapped to the local layout. The local root build settings import DragonLib's
platform policy, pin version/TFMs, disable packaging and Quill shader generation,
and suppress missing public XML documentation warnings (CS1591) for vendor code.

## Local source changes

- `Scribe/FontSystem.cs`: selectable MSDF/SDF generation, range validation and
  immutable range after glyph generation; regenerate the decoration band when
  the range changes before glyph creation.
- `Scribe/Sdf/MsdfGlyphGenerator.cs`: DragonLib native msdfgen bridge, retained
  from the previous local fork.
- `Scribe/Sdf/SdfScanlineGenerator.cs`: result buffer comment covers MSDF RGB.
- `Quill/TextRenderer.cs`: MSDF atlas settings and FontSystem constructor wiring.
- `Scribe/Scribe.csproj`: DragonLib regression-test visibility and native library /
  license deployment, in addition to the common project layout adaptations.
- `Quill/External/LibTessDotNet/Mesh.cs` and `Tess.cs`: trailing whitespace in
  imported comment lines removed; no behavior changes.

Foster-specific migration is in `../../Engine/Paper/FosterCanvasRenderer.cs`.
UI layout/interaction C# sources in Paper and Origami are upstream code.

## License notices

The root MIT `LICENSE` and available per-module license files are preserved.
Recast retains `Recast/LICENSE.txt` and its source headers;
Quill's embedded LibTessDotNet source retains its own license and headers.
Retain the relevant notices when redistributing modules or derived binaries.

## Upgrade validation (2026-10-05)

All 19 local projects and both Engine targets compile. The upstream Paper,
Quill, Scribe, Scaffold and Quire suites were run from the pinned archive with
their project references redirected to these local libraries: 411 tests passed.
The sibling DragonLib.Tests MSDF suite passed 12 tests. GPU readbacks at 1x and
2x DPI check text, rich text, wrapping and input-field clipping on D3D12.
Optional modules are source/build-ready; their game integration and runtime
behavior, including browser AOT, have not been validated by this upgrade.
