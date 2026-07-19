using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Foster.Framework;

namespace Engine;

public partial class LocalStorage(string rootPath) : StorageContainer
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public override bool Writable => true;

    private string Full(string path) => Path.Combine(_root, path);

    public override bool FileExists(string path) => File.Exists(Full(path));

    public override bool DirectoryExists(string path) => Directory.Exists(Full(path));

    public override Stream OpenRead(string path) => File.OpenRead(Full(path));

    public override IEnumerable<string> EnumerateDirectory(string? path = null, string? searchPattern = null, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var dir = Full(path ?? string.Empty);
        var pattern = searchPattern ?? "*";
        return Directory.Exists(dir)
            ? Directory.EnumerateFileSystemEntries(dir, pattern, searchOption)
                .Select(p => Path.GetRelativePath(_root, p))
            : [];
    }

    public override bool CreateDirectory(string path)
    {
        Directory.CreateDirectory(Full(path));
        return true;
    }

    public override Stream Create(string path)
    {
        var full = Full(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return File.Create(full);
    }

    public override bool Remove(string path)
    {
        var full = Full(path);
        if (File.Exists(full)) { File.Delete(full); return true; }
        if (Directory.Exists(full)) { Directory.Delete(full, true); return true; }
        return false;
    }

    public override void Dispose(bool disposing) {}
}


public class StorageUtils
{
    /// <summary>
    /// 注意，这里需要设置Game.csproj, 这样Game.csproj所在目录就成了当前目录
    /// 在PropertyGroup中加上<RunWorkingDirectory>$(MSBuildProjectDirectory)</RunWorkingDirectory>
    /// </summary>
    public static LocalStorage GetDevGameRoot => new LocalStorage(Directory.GetCurrentDirectory());

    /// <summary>
    /// 指向Build之后的exe目录
    /// </summary>
    public static LocalStorage GetReleaseGameRoot => new LocalStorage(AppContext.BaseDirectory);
}


/*
1. FileSystem Save in:
%AppData\GameName\xxx.json

2. GetDevGameRoot Save in:
- Game
    - Resources
        - Config
            - DevGame.json
    - Game.csproj
StorageUtils.GetDevGameRoot.SaveJson("Resources/Config/DevGame.json",data);

3. GetReleaseGameRoot Save in:
- bin
    - Net10.0
        - Release.json
        - game.exe
StorageUtils.GetReleaseGameRoot.SaveJson("Release.json", data);
*/
