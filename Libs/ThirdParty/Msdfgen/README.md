# msdfgen native bridge

`core/` and `msdfgen.h` are unmodified upstream **msdfgen v1.12.1** source:
https://github.com/Chlumsky/msdfgen/tree/v1.12.1
Copyright Viktor Chlumsky; MIT license in `LICENSE.txt`.

`bridge.cpp` is DragonLib's narrow C ABI. It accepts Scribe's already-parsed
line/quadratic/cubic glyph outline and produces top-down RGBA8 MSDF pixels.
Generation uses edge coloring, overlapping-contour support, nonzero winding
sign correction and MSDF error correction. There is no FreeType/Skia dependency.
Pixels are linear distance data, not sRGB or premultiplied color.

## Windows x64

The checked-in `runtimes/win-x64/native/DragonLib.Msdfgen.dll` is built with
the static MSVC runtime. Scribe.csproj copies it transitively to application
build and publish outputs. Rebuild with CMake and Visual Studio C++ tools:

```powershell
./Libs/ThirdParty/Msdfgen/build.ps1
```

## WebAssembly (Foster.Web)

A browser-wasm app imports `Msdfgen.Web.targets`. It adds `web/DragonLib.Msdfgen.cpp`
(includes `bridge.cpp`) and `core/*.cpp` as `NativeFileReference`, so the wasm-tools
workload's emcc statically links them into `dotnet.native.wasm`. .NET wasm resolves
`DllImport("DragonLib.Msdfgen")` by that file name; no Scribe change is needed.
Publishing relinks the native runtime, so the first web publish takes longer.

## Other platforms

Only Windows x64 binaries are included and verified. The bridge itself is portable:

```sh
cmake -S Libs/ThirdParty/Msdfgen -B build/msdfgen -DCMAKE_BUILD_TYPE=Release
cmake --build build/msdfgen --parallel
```

Deploy the resulting `libDragonLib.Msdfgen.so` / `libDragonLib.Msdfgen.dylib`
beside Scribe.dll and add the corresponding RID-conditioned copy item to
Scribe.csproj. Windows ARM64 similarly needs its own native build/copy item.
Missing native binaries report a clear error instead of silently rendering SDF.
