namespace PS5PKGTool.Core.Services;

public sealed class Ps5DumpLocator
{
    /// <summary>All candidate sources discovered in one directory walk, grouped by kind.</summary>
    public sealed class LocatedSources
    {
        public List<string> ParamFiles { get; } = [];
        public List<string> Packages { get; } = [];
        public List<string> Ffpfsc { get; } = [];
        public List<string> FilesystemImages { get; } = [];
        public List<string> Ffpkg { get; } = [];
    }

    /// <summary>
    /// Enumerates <paramref name="selectedPath"/> once and classifies every file by extension
    /// (plus <c>sce_sys/param.json</c> loose dumps). The walk is pruned at each loose-dump root:
    /// a directory holding <c>sce_sys/param.json</c> is a game dump whose content can never itself
    /// be a source, so it is recorded and not descended into (a dump holds tens of thousands of
    /// files). <paramref name="onDirectory"/> is invoked for each directory visited, unthrottled.
    /// </summary>
    public LocatedSources FindAll(string selectedPath, bool recursive, CancellationToken cancellationToken = default,
        Action<string>? onDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var found = new LocatedSources();

        if (File.Exists(selectedPath))
        {
            string full = Path.GetFullPath(selectedPath);
            if (Path.GetFileName(full).Equals("eboot.bin", StringComparison.OrdinalIgnoreCase))
            {
                string paramPath = Path.Combine(Path.GetDirectoryName(full)!, "sce_sys", "param.json");
                if (File.Exists(paramPath)) found.ParamFiles.Add(Path.GetFullPath(paramPath));
            }
            else
            {
                Classify(full, found);
            }

            Normalize(found);
            return found;
        }

        if (!Directory.Exists(selectedPath)) return found;

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(selectedPath));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            onDirectory?.Invoke(directory);

            // A dump root is identified by sce_sys/param.json directly under it. Record it and stop:
            // the dump content below it cannot hold another source worth walking.
            string dumpParam = Path.Combine(directory, "sce_sys", "param.json");
            if (File.Exists(dumpParam))
            {
                found.ParamFiles.Add(Path.GetFullPath(dumpParam));
                continue;
            }

            // One directory read per directory (attributes come from the scan, so classifying a file
            // or a subdirectory costs no extra I/O).
            foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    if (recursive && !IsScratchDirectory(entry.Name)) pending.Push(entry.FullName);
                }
                else
                {
                    Classify(entry.FullName, found);
                }
            }
        }

        Normalize(found);
        return found;
    }

    private static void Classify(string fullPath, LocatedSources found)
    {
        string name = Path.GetFileName(fullPath);
        if (name.Equals("param.json", StringComparison.OrdinalIgnoreCase))
        {
            string? sceSys = Path.GetDirectoryName(fullPath);
            if (sceSys is not null && Path.GetFileName(sceSys).Equals("sce_sys", StringComparison.OrdinalIgnoreCase))
                found.ParamFiles.Add(fullPath);
            return;
        }

        string extension = Path.GetExtension(fullPath);
        if (extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase)) found.Packages.Add(fullPath);
        else if (extension.Equals(".ffpfsc", StringComparison.OrdinalIgnoreCase)) found.Ffpfsc.Add(fullPath);
        else if (extension.Equals(".exfat", StringComparison.OrdinalIgnoreCase)) found.FilesystemImages.Add(fullPath);
        else if (extension.Equals(".ffpkg", StringComparison.OrdinalIgnoreCase)) found.Ffpkg.Add(fullPath);
    }

    /// <summary>
    /// True for a directory the application itself creates as scratch space: image/convert staging
    /// (<c>PS5PKGTool-image-&lt;guid&gt;</c>, <c>PS5PKGTool\pkg-image</c>, <c>PS5PKGTool\convert</c>),
    /// exFAT/FFPKG edit workspaces (<c>.PS5PKGTool-exfat-edit-*</c>, <c>.PS5PKGTool-ffpkg-edit-*</c>)
    /// and preview output. When the workspace sits inside a library folder these would otherwise be
    /// walked as if they were game content.
    /// </summary>
    internal static bool IsScratchDirectory(string name) =>
        name.StartsWith("PS5PKGTool", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(".PS5PKGTool", StringComparison.OrdinalIgnoreCase);

    private static void Normalize(LocatedSources found)
    {
        Deduplicate(found.ParamFiles);
        Deduplicate(found.Packages);
        Deduplicate(found.Ffpfsc);
        Deduplicate(found.FilesystemImages);
        Deduplicate(found.Ffpkg);
    }

    private static void Deduplicate(List<string> paths)
    {
        if (paths.Count < 2) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        paths.RemoveAll(path => !seen.Add(path));
        paths.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> FindFfpfscFiles(string selectedPath, bool recursive,
        CancellationToken cancellationToken = default) => FindFiles(selectedPath, recursive, ".ffpfsc", cancellationToken);

    public IReadOnlyList<string> FindFilesystemImageFiles(string selectedPath, bool recursive,
        CancellationToken cancellationToken = default) => FindFiles(selectedPath, recursive, ".exfat", cancellationToken);

    public IReadOnlyList<string> FindFfpkgFiles(string selectedPath, bool recursive,
        CancellationToken cancellationToken = default) => FindFiles(selectedPath, recursive, ".ffpkg", cancellationToken);

    public IReadOnlyList<string> FindPackageFiles(string selectedPath, bool recursive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        if (File.Exists(selectedPath))
            return Path.GetExtension(selectedPath).Equals(".pkg", StringComparison.OrdinalIgnoreCase)
                ? [Path.GetFullPath(selectedPath)]
                : [];
        if (!Directory.Exists(selectedPath)) return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var results = new List<string>();
        foreach (string path in Directory.EnumerateFiles(selectedPath, "*.pkg", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(Path.GetFullPath(path));
        }
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private static IReadOnlyList<string> FindFiles(string selectedPath, bool recursive, string extension,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        if (File.Exists(selectedPath))
            return Path.GetExtension(selectedPath).Equals(extension, StringComparison.OrdinalIgnoreCase)
                ? [Path.GetFullPath(selectedPath)]
                : [];
        if (!Directory.Exists(selectedPath)) return [];
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var results = new List<string>();
        foreach (string path in Directory.EnumerateFiles(selectedPath, "*" + extension, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(Path.GetFullPath(path));
        }
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    public IReadOnlyList<string> FindParamFiles(string selectedPath, bool recursive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(selectedPath))
        {
            string fileName = Path.GetFileName(selectedPath);
            string? parentDirectory = Path.GetDirectoryName(selectedPath);
            if (fileName.Equals("param.json", StringComparison.OrdinalIgnoreCase) &&
                parentDirectory is not null &&
                Path.GetFileName(parentDirectory).Equals("sce_sys", StringComparison.OrdinalIgnoreCase))
                results.Add(Path.GetFullPath(selectedPath));
            else if (fileName.Equals("eboot.bin", StringComparison.OrdinalIgnoreCase))
                AddDirect(parentDirectory!, results);
            return results.ToArray();
        }
        if (!Directory.Exists(selectedPath)) return [];

        AddDirect(selectedPath, results);
        if (!recursive || results.Count > 0) return results.ToArray();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        foreach (string file in Directory.EnumerateFiles(selectedPath, "param.json", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? parentDirectory = Path.GetDirectoryName(file);
            if (parentDirectory is not null &&
                Path.GetFileName(parentDirectory).Equals("sce_sys", StringComparison.OrdinalIgnoreCase))
                results.Add(Path.GetFullPath(file));
        }
        return results.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddDirect(string root, HashSet<string> results)
    {
        string paramPath = Path.Combine(root, "sce_sys", "param.json");
        if (File.Exists(paramPath)) results.Add(Path.GetFullPath(paramPath));
    }
}
