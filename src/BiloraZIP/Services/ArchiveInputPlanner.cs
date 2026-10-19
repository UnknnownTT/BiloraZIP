using System.IO;

namespace BiloraZIP.Services;

internal static class ArchiveInputPlanner
{
    // Validate all roots before beginning any writes. Reject reparse points to avoid unsafe cycles.
    public static (List<(string Source, string Name)> Files, List<string> Directories) Build(
        IReadOnlyCollection<string> inputs, string destination)
    {
        if (inputs.Count == 0) throw new ArgumentException("Select at least one file or folder.");
        string output = Path.GetFullPath(destination);
        var files = new List<(string Source, string Name)>();
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in inputs)
        {
            string source = Path.GetFullPath(input);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new IOException("Selected items must have distinct names at the archive root: " + name);
            if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase))
                throw new IOException("An archive cannot contain itself.");
            if (Directory.Exists(source))
            {
                if (output.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Save the archive outside selected folders.");
                RejectLink(source);
                dirs.Add(name + "/");
                // Explicit DFS: inspect links BEFORE traversing. Skipping reparse
                // points silently can create incomplete backups without warning.
                VisitDirectory(source, name, files, dirs);
            }
            else if (File.Exists(source))
            {
                RejectLink(source);
                files.Add((source, name));
            }
            else throw new FileNotFoundException("Selected source no longer exists.", source);
        }
        var keys = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files) if (!keys.Add(file.Name)) throw new IOException("Duplicate archive path: " + file.Name);
        return (files, dirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void VisitDirectory(string directory, string archivePrefix,
        List<(string Source, string Name)> files, HashSet<string> dirs)
    {
        foreach (string child in Directory.EnumerateFileSystemEntries(directory).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            RejectLink(child);
            string name = archivePrefix + "/" + Path.GetFileName(child);
            if (Directory.Exists(child))
            {
                dirs.Add(name + "/");
                VisitDirectory(child, name, files, dirs);
            }
            else if (File.Exists(child)) files.Add((child, name));
            else throw new IOException("Unsupported or inaccessible filesystem item: " + child);
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Links and junctions are not added automatically: " + path);
    }
}
