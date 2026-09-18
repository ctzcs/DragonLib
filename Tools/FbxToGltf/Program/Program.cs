using DragonLib.Gltf;
using Engine.Assets.Dasset;

// .dasset 烘焙 CLI：把 .glb/.gltf cook 成引擎私有二进制 .dasset（运行时二进制直读，
// SharpGLTF 不进运行时热路径）。用法：
//   dassetcompiler <input.glb|gltf> <output.dasset>   单文件模式
//   dassetcompiler --scan <dir>                        目录增量模式（.dasset 新于源文件则跳过）

if (args.Length == 2 && args[0] == "--scan")
    return ScanDirectory(args[1]);
if (args.Length == 2 && !args[0].StartsWith("--"))
    return CompileFile(args[0], args[1], force: true) ? 0 : 1;

Console.Error.WriteLine("用法: dassetcompiler <input.glb|gltf> <output.dasset> | --scan <dir>");
return 1;

static bool CompileFile(string inputPath, string outputPath, bool force)
{
    var extension = Path.GetExtension(inputPath).ToLowerInvariant();
    if (extension is not (".glb" or ".gltf"))
    {
        Console.Error.WriteLine($"输入不是 .glb/.gltf: {inputPath}");
        return false;
    }
    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"输入文件不存在: {inputPath}");
        return false;
    }

    if (!force && File.Exists(outputPath)
        && File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(inputPath))
    {
        Console.WriteLine($"Up-to-date {outputPath}");
        return true;
    }

    try
    {
        Console.WriteLine($"Cooking {inputPath} ...");
        var model = GltfModelCooker.Cook(inputPath);
        DassetWriter.Write(outputPath, model);
        Console.WriteLine($"  -> {outputPath} ({model.Primitives.Count} primitive(s), {model.Textures.Count} texture(s))");
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Cook 失败 {inputPath}: {ex.Message}");
        return false;
    }
}

static int ScanDirectory(string dir)
{
    if (!Directory.Exists(dir))
    {
        Console.Error.WriteLine($"目录不存在: {dir}");
        return 1;
    }

    var compiled = 0;
    var skipped = 0;
    var failed = 0;
    foreach (var inputPath in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
    {
        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension is not (".glb" or ".gltf"))
            continue;

        var outputPath = Path.ChangeExtension(inputPath, ".dasset");
        var upToDate = File.Exists(outputPath)
            && File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(inputPath);
        if (CompileFile(inputPath, outputPath, force: !upToDate))
        {
            if (upToDate) skipped++;
            else compiled++;
        }
        else
        {
            failed++;
        }
    }

    Console.WriteLine($"Done. Cooked {compiled}, up-to-date {skipped}, failed {failed}.");
    return failed > 0 ? 1 : 0;
}
