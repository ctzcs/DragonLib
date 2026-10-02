// WebAssembly entry for the msdfgen bridge.
// .NET wasm statically links NativeFileReference sources and resolves DllImport("DragonLib.Msdfgen")
// by the referenced file's name, so this file carries that name; the core sources are referenced separately.
#include "../bridge.cpp"
