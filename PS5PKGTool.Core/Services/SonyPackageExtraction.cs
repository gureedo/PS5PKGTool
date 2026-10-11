using PS5PKGTool.Core.Builders;
using PS5PKGTool.Core.Models;
using PS5PKGTool.Core.Parsers;

namespace PS5PKGTool.Core.Services;

public readonly record struct SonyPackageExtractProgress(long CompletedBytes, long TotalBytes, string CurrentPath);
public sealed class SonyPackageExtractResult
{
    public required string Destination { get; init; }
    public required int FileCount { get; init; }
    public required long ExtractedBytes { get; init; }
}

public static class SonyPackageExtraction
{
    public static async Task<SonyPackageExtractResult> ExtractAsync(string packagePath, string destination,
        string passcode = SonyDebugPackageCredentials.DefaultPasscode,
        IProgress<SonyPackageExtractProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        string output = Path.GetFullPath(destination);
        if (Directory.Exists(output)) throw new IOException("The extraction destination already exists: " + output);
        string fullPackage = Path.GetFullPath(packagePath);
        SonyPkgSummary package = new SonyPkgReader().Read(fullPackage, passcode);
        var game = new Ps5GameInfo
        {
            SourceKind = Ps5SourceKind.SonyPackage,
            RootPath = fullPackage,
            Package = package,
        };
        string staging = output + ".extracting." + Guid.NewGuid().ToString("N");
        long completed = 0;
        long total;
        int fileCount;
        try
        {
            Directory.CreateDirectory(staging);

            // Use the exact file set the file browser and the convert pipeline use: the inner PFS
            // merged with the readable CNT metadata (param.json, artwork, trophies, activities...),
            // with the PFS's own flat-path/AFID index tables excluded. Extract, Convert and the
            // browser therefore agree, every readable file is written, and the internal tables never
            // appear in the output.
            using IReadOnlyGameFileSystem files = GameFileSystem.Open(game, cancellationToken);
            GameFileRecord[] wanted = files.Files
                .Where(file => file.RelativePath.Length > 0)
                .ToArray();
            if (wanted.Length == 0)
                throw new InvalidDataException(
                    "The package does not expose any readable files (it may be retail or require a passcode).");

            total = wanted.Sum(file => file.Size);
            fileCount = wanted.Length;
            foreach (GameFileRecord file in wanted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = GameFileSystem.NormalizePath(file.RelativePath);
                string target = ResolveContainedPath(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var outputFile = new FileStream(target, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 1024 * 1024, FileOptions.Asynchronous);
                await using Stream input = files.OpenRead(relative);
                byte[] buffer = new byte[1024 * 1024];
                long position = 0;
                while (position < file.Size)
                {
                    int take = checked((int)Math.Min(buffer.Length, file.Size - position));
                    int read = await input.ReadAsync(buffer.AsMemory(0, take), cancellationToken).ConfigureAwait(false);
                    if (read <= 0) throw new EndOfStreamException($"Package file '{relative}' ended unexpectedly.");
                    await outputFile.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    position += read;
                    completed += read;
                    progress?.Report(new SonyPackageExtractProgress(completed, total, relative));
                }
            }

            Directory.Move(staging, output);
            return new SonyPackageExtractResult { Destination = output, FileCount = fileCount, ExtractedBytes = completed };
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (IOException) { }
            throw;
        }
    }

    private static string ResolveContainedPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Package path is unsafe.");
        string rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escapes the extraction root.");
        return target;
    }
}
