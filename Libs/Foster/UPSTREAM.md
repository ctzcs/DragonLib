# Foster source pin

This directory mirrors the tracked files of `ctzcs/Foster` at a fixed commit,
plus this provenance file. Implement customizations in the independent Foster
repository on `MyFoster`, validate and push them, then synchronize this copy.

| Source | Revision |
| --- | --- |
| Official upstream | `https://github.com/FosterFramework/Foster` |
| Upstream baseline | `730cc6aec20068bf5cc57235b7aa70957f5d9035` (Foster 0.4.2) |
| Fork / branch | `https://github.com/ctzcs/Foster`, `MyFoster` |
| Pinned fork revision | `08c3d7f999c9ea4cfd730ad3b608d5ef6bff4517` |
| Application customizations | `0d2d5bb` (no-focus startup and input frame ordering) |
| Graphics customizations | `c66d962` (HDR/sRGB formats, mipmaps, line topology) |
| Browser font compatibility | `08c3d7f` (synchronous browser rasterization) |
| Synchronized | 2026-10-04 |

The upstream CW front-face setting is retained. Engine uploads CCW asset indices
through `MeshUpload3D` to adapt to this setting. See `MYFOSTER.md` for extension
details and validation. Native libraries and compiled framework shaders are
copied from the same pinned revision; generated `bin`/`obj` files are not source.

To inspect custom changes in the independent Foster repository:

```powershell
git fetch upstream
git log 730cc6a..08c3d7f --oneline
git diff 730cc6a 08c3d7f -- Framework
```

Use the recorded revisions for reproducible comparisons. Comparing to a newly
fetched `upstream/main` also shows subsequent upstream changes until MyFoster is
updated and tested against that new baseline.

[Review the pinned customizations](https://github.com/ctzcs/Foster/compare/730cc6aec20068bf5cc57235b7aa70957f5d9035...08c3d7f999c9ea4cfd730ad3b608d5ef6bff4517).
