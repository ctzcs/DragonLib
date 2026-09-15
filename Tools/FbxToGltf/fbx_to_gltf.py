# Blender headless 脚本：把一个 FBX 文件转成 glTF 二进制（.glb，贴图内嵌）。
# 用法（由 convert.bat / convert.sh 调用）：
#   blender --background --factory-startup --python fbx_to_gltf.py -- <input.fbx> <output.glb>
# 参数经 "--" 之后的 argv 传入。

import bpy
import sys


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:]
    if len(argv) < 2:
        print("usage: blender --python fbx_to_gltf.py -- <input.fbx> <output.glb>", file=sys.stderr)
        return 1

    input_path, output_path = argv[0], argv[1]

    # 干净场景，避免默认 Cube/Light 混进导出。
    bpy.ops.wm.read_factory_settings(use_empty=True)

    bpy.ops.import_scene.fbx(filepath=input_path)

    # 导出为单个 .glb：贴图内嵌、导出 UV/法线/切线（DragonLib 加载端直接消费 TANGENT）。
    # Y-up 是 glTF 规范方向，保持默认。Blender 5.x 起 GLB 恒定内嵌贴图（无 export_images 参数）。
    bpy.ops.export_scene.gltf(
        filepath=output_path,
        export_format="GLB",
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_animations=True,
        export_skins=True,
    )
    print(f"[fbx_to_gltf] {input_path} -> {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
