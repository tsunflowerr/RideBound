using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Bundles;

public sealed record BundleSourceComponentSelection(
    string ComponentId,
    IReadOnlyList<string> RelativeRoots);

public static class BundleSourceInventoryCapture
{
    private static readonly HashSet<string> ExcludedDirectoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".vs",
            "bin",
            "obj",
            "TestResults",
        };

    public static BundleSourceInventory Capture(
        string repositoryRoot,
        IReadOnlyList<BundleSourceComponentSelection> selections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(selections);
        var root = Path.GetFullPath(repositoryRoot);
        var rootInfo = new DirectoryInfo(root);

        if (!rootInfo.Exists
            || (rootInfo.Attributes & FileAttributes.ReparsePoint) != 0
            || selections.Count == 0
            || selections.Any(
                selection => selection is null
                    || !StrictBundlePath.IsArtifactId(selection.ComponentId)
                    || selection.RelativeRoots is null
                    || selection.RelativeRoots.Count == 0)
            || selections.Select(selection => selection.ComponentId)
                .Distinct(StringComparer.Ordinal).Count() != selections.Count)
        {
            throw new ArgumentException("Source inventory selection is invalid.", nameof(selections));
        }

        var commitBytes = RunGit(root, ["rev-parse", "--verify", "HEAD"]);
        var commit = Encoding.ASCII.GetString(commitBytes).Trim();
        var statusBefore = RunGit(
            root,
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"]);
        var entries = new List<BundleSourceInventoryEntry>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selection in selections.OrderBy(value => value.ComponentId, StringComparer.Ordinal))
        {
            foreach (var relativeRoot in selection.RelativeRoots.Order(StringComparer.Ordinal))
            {
                if (!StrictBundlePath.IsSafeRelativePath(relativeRoot, requireDataPrefix: false))
                {
                    throw new ArgumentException("Source selection path is not portable.", nameof(selections));
                }

                var selectedPath = ResolveInside(root, relativeRoot);

                if (File.Exists(selectedPath))
                {
                    AddFile(root, selection.ComponentId, selectedPath, seenPaths, entries);
                }
                else if (Directory.Exists(selectedPath))
                {
                    AddDirectory(root, selection.ComponentId, selectedPath, seenPaths, entries);
                }
                else
                {
                    throw new IOException("Selected source root is missing.");
                }
            }
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Source inventory selection resolved no files.");
        }

        var ordered = entries
            .OrderBy(value => value.ComponentId, StringComparer.Ordinal)
            .ThenBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();

        foreach (var entry in ordered)
        {
            var fullPath = ResolveInside(root, entry.RelativePath);
            var identity = Pin(fullPath);

            if (identity.LengthBytes != entry.LengthBytes || identity.Sha256 != entry.Sha256)
            {
                throw new IOException("Source file changed during inventory capture.");
            }
        }

        var statusAfter = RunGit(
            root,
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"]);

        if (!statusBefore.SequenceEqual(statusAfter))
        {
            throw new IOException("Git working-tree state changed during source inventory capture.");
        }

        var result = new BundleSourceInventory(
            "1.0.0",
            commit,
            statusBefore.Length != 0,
            Convert.ToHexStringLower(SHA256.HashData(statusBefore)),
            ordered);
        BundleSourceInventoryIdentity.ValidateInventory(result);
        return result;
    }

    private static void AddDirectory(
        string repositoryRoot,
        string componentId,
        string selectedRoot,
        ISet<string> seenPaths,
        ICollection<BundleSourceInventoryEntry> entries)
    {
        var rootInfo = new DirectoryInfo(selectedRoot);

        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Selected source directory is a reparse point.");
        }

        var pending = new Stack<DirectoryInfo>();
        pending.Push(rootInfo);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var child in directory.EnumerateFileSystemInfos())
            {
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Source inventory encountered a reparse point.");
                }

                if (child is DirectoryInfo childDirectory)
                {
                    if (!ExcludedDirectoryNames.Contains(childDirectory.Name))
                    {
                        pending.Push(childDirectory);
                    }
                }
                else if (child is FileInfo)
                {
                    AddFile(repositoryRoot, componentId, child.FullName, seenPaths, entries);
                }
                else
                {
                    throw new IOException("Source inventory encountered an unsupported entry.");
                }
            }
        }
    }

    private static void AddFile(
        string repositoryRoot,
        string componentId,
        string fullPath,
        ISet<string> seenPaths,
        ICollection<BundleSourceInventoryEntry> entries)
    {
        var relative = Path.GetRelativePath(repositoryRoot, fullPath).Replace('\\', '/');

        if (!StrictBundlePath.IsSafeRelativePath(relative, requireDataPrefix: false)
            || !seenPaths.Add(relative))
        {
            throw new InvalidDataException(
                "Source file path is unsafe or selected by more than one component.");
        }

        var identity = Pin(fullPath);
        entries.Add(
            new BundleSourceInventoryEntry(
                componentId,
                relative,
                identity.LengthBytes,
                identity.Sha256));
    }

    private static (long LengthBytes, string Sha256) Pin(string path)
    {
        var info = new FileInfo(path);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Source file is missing or unsafe.");
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.SequentialScan);
        var length = stream.Length;
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (stream.Position != length || stream.Length != length)
        {
            throw new IOException("Source file changed while hashed.");
        }

        return (length, hash);
    }

    private static byte[] RunGit(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new IOException("Could not start git for source inventory capture.");
        using var output = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new IOException(
                string.IsNullOrWhiteSpace(error)
                    ? "Git source inventory command failed."
                    : "Git source inventory command failed with a safe diagnostic.");
        }

        return output.ToArray();
    }

    private static string ResolveInside(string root, string relative)
    {
        var fullRoot = root + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Source selection escapes the repository root.");
        }

        return fullPath;
    }
}
