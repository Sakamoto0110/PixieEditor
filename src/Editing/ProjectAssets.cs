namespace PixieEditor.Editing;

// The files a project's components name (images, fonts) are inside its folder, named by their path
// relative to it, with '/'. A file from elsewhere is copied into the project's folder for its kind
// first (images/ for a PNG, fonts/ for a TrueType font), so the project stays whole when it is opened
// on another machine.
internal static class ProjectAssets
{
    internal sealed record Catalog(string[] Paths, string[] Problems);
    internal sealed record ImportResult(int Copied, int Existing, int Skipped, string[] Problems);

    private static readonly HashSet<string> ProjectMetadataFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".local", ".pixie-code", ".vs", "bin", "build", "obj",
    };

    private static int revision;
    public static int Revision => Volatile.Read(ref revision);

    // The folder of the project that files of a kind are copied into, by the kind's extension
    // without the dot, as a field's asset kind names it.
    public static string FolderFor(string kind) => kind.ToLowerInvariant() switch
    {
        "png" => "images",
        "ttf" => "fonts",
        _ => "assets",
    };

    // Whether `file` is of the asset kind `kind` ("png", "ttf").
    public static bool IsKind(string file, string kind) =>
        Path.GetExtension(file).Equals("." + kind, StringComparison.OrdinalIgnoreCase);

    // Whether `file` is an image a sprite can draw: a PNG.
    public static bool IsImage(string file) => IsKind(file, "png");

    public static bool IsAtlas(string file) => file.EndsWith(".atlas.json", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupported(string file) => IsImage(file) || IsKind(file, "ttf");

    // The path a component names for `file`, and the file it copied into the project to get it, if it
    // did. A file already inside the project is used where it is; one outside is copied into the
    // folder for its kind, under its own name, or a numbered one when another file has it, and an
    // identical file already there is used instead of a second copy. Without a project, the file's
    // full path.
    public static (string Path, string? CopiedTo) Import(string? projectFolder, string file) =>
        Import(projectFolder, file, "");

    private static (string Path, string? CopiedTo) Import(string? projectFolder, string file, string subfolder)
    {
        var full = System.IO.Path.GetFullPath(file);
        if (projectFolder == null)
            return (full.Replace('\\', '/'), null);

        var project = System.IO.Path.GetFullPath(projectFolder);
        if (Inside(project, full))
        {
            RejectLinkedPath(project, full);
            return (Relative(project, full), null);
        }

        var kind = System.IO.Path.GetExtension(full).TrimStart('.');
        var folder = System.IO.Path.Combine(project, FolderFor(kind), subfolder);
        RejectLinkedPath(project, folder);
        Directory.CreateDirectory(folder);
        var name = System.IO.Path.GetFileNameWithoutExtension(full);
        var extension = System.IO.Path.GetExtension(full).ToLowerInvariant();
        string? pending = null;
        try
        {
            for (var n = 1; ; ++n)
            {
                var target = System.IO.Path.Combine(folder, n == 1 ? name + extension : $"{name} {n}{extension}");
                RejectLink(target);
                if (Directory.Exists(target))
                    continue;
                if (File.Exists(target))
                {
                    if (SameContent(target, pending ?? full))
                        return (Relative(project, target), null);
                    continue;
                }

                if (pending == null)
                {
                    pending = System.IO.Path.Combine(folder, $".pixie-import-{Guid.NewGuid():N}.tmp");
                    File.Copy(full, pending);
                }
                try { File.Move(pending, target); }
                catch (IOException) when (File.Exists(target) || Directory.Exists(target))
                {
                    // Another import filled this name after the existence check. Recheck its contents.
                    --n;
                    continue;
                }
                pending = null;
                Interlocked.Increment(ref revision);
                var path = Relative(project, target);
                return (path, path);
            }
        }
        finally
        {
            if (pending != null)
                File.Delete(pending);
        }
    }

    public static Catalog Browse(string projectFolder)
    {
        var root = Path.GetFullPath(projectFolder);
        RejectLink(root);
        var problems = new List<string>();
        var paths = Files(root, excludeProjectMetadata: true, problems)
            .Where(file => IsSupported(file) || IsAtlas(file))
            .Select(file => Relative(root, file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(paths, problems.ToArray());
    }

    // Preserve the collection's source folder and subfolders within each destination kind.
    public static ImportResult ImportFolder(string projectFolder, string sourceFolder)
    {
        var project = Path.GetFullPath(projectFolder);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));
        var collection = Path.GetFileName(root);
        var sourceInProject = Inside(project, root);
        if (sourceInProject)
            RejectLinkedPath(project, root);
        else
            RejectLink(root);
        var problems = new List<string>();
        var copied = 0;
        var existing = 0;
        var skipped = 0;
        // Take the file list before writing so an ancestor of the project can also be imported.
        var files = Files(root, excludeProjectMetadata: Path.GetRelativePath(project, root) == ".", problems,
                skipFolder: sourceInProject ? null : project)
            .ToArray();
        foreach (var file in files)
        {
            if (!IsSupported(file))
            {
                ++skipped;
                continue;
            }

            var relativeFolder = Path.GetDirectoryName(Path.GetRelativePath(root, file)) ?? "";
            try
            {
                if (Import(project, file, Path.Combine(collection, relativeFolder)).CopiedTo != null)
                    ++copied;
                else
                    ++existing;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{Relative(root, file)}: {e.Message}");
            }
        }
        return new(copied, existing, skipped, problems.ToArray());
    }

    // The folder a chooser of files of `kind` starts in: the project's folder for that kind when it
    // exists, else the project.
    public static string? StartFolder(string? projectFolder, string kind)
    {
        if (projectFolder == null)
            return null;
        var folder = System.IO.Path.Combine(projectFolder, FolderFor(kind));
        return Directory.Exists(folder) ? folder : projectFolder;
    }

    // The first file of `kind` in the project, by path, looking in its folder for that kind first, as a
    // project-relative path; null when it has none. The build's folders are not looked in.
    public static string? First(string? projectFolder, string kind)
    {
        if (projectFolder == null || !Directory.Exists(projectFolder))
            return null;
        var project = System.IO.Path.GetFullPath(projectFolder);
        var preferred = System.IO.Path.Combine(project, FolderFor(kind));
        try
        {
            var files = Directory.EnumerateFiles(project, "*." + kind, SearchOption.AllDirectories)
                .Where(f => IsKind(f, kind))
                .Select(f => Relative(project, f))
                .Where(f => !f.StartsWith("build/", StringComparison.OrdinalIgnoreCase) &&
                            !f.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.StartsWith(Relative(project, preferred) + "/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);
            return files.FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Whether the file a project-relative `path` names exists.
    public static bool Exists(string? projectFolder, string path) =>
        File.Exists(projectFolder == null ? path : System.IO.Path.Combine(projectFolder, path));

    internal static string AtlasAssetFile(string projectFolder, string path, string suffix)
    {
        if (!IsAtlasAssetPath(path, suffix))
            throw new IOException($"Expected a project-relative {suffix} path: {path}");
        var root = Path.GetFullPath(projectFolder);
        var file = Path.Combine(root, path);
        RejectLinkedPath(root, file);
        return file;
    }

    internal static bool IsAtlasAssetPath(string path, string suffix) =>
        path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
        path[(path.LastIndexOf('/') + 1)..].Length > suffix.Length &&
        !path.Any(ch => ch < 32 || ch == 127 || "\\:<>\u0022|?*".Contains(ch)) &&
        path.Split('/').All(part => part.Length != 0 && !part.EndsWith('.') && !part.EndsWith(' ') &&
            !IsDeviceName(part.Split('.')[0].TrimEnd(' ')));

    private static bool IsDeviceName(string stem)
    {
        var upper = stem.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) ||
                                  upper.StartsWith("LPT", StringComparison.Ordinal)) &&
            "123456789¹²³".Contains(upper[3]);
    }

    private static IEnumerable<string> Files(string root, bool excludeProjectMetadata, List<string> problems,
        string? skipFolder = null)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.TryPop(out var folder))
        {
            string[] files;
            try
            {
                foreach (var child in Directory.GetDirectories(folder, "*", options))
                    if ((skipFolder == null || !Path.GetRelativePath(skipFolder, child).Equals(".", StringComparison.Ordinal)) &&
                        (!excludeProjectMetadata || folder != root || !ProjectMetadataFolders.Contains(Path.GetFileName(child))))
                        folders.Push(child);
                files = Directory.GetFiles(folder, "*", options);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{Relative(root, folder)}: {e.Message}");
                continue;
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static bool Inside(string folder, string file)
    {
        var relative = System.IO.Path.GetRelativePath(folder, file);
        var outside = relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) ||
                      relative.StartsWith("..\\", StringComparison.Ordinal);
        return !outside && !System.IO.Path.IsPathRooted(relative);
    }

    private static string Relative(string folder, string file) => System.IO.Path.GetRelativePath(folder, file).Replace('\\', '/');

    private static void RejectLinkedPath(string root, string file)
    {
        RejectLink(root);
        var current = root;
        foreach (var part in Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            RejectLink(current);
        }
    }

    private static void RejectLink(string path)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"Asset paths cannot pass through linked files or folders: {path}");
    }

    private static bool SameContent(string a, string b)
    {
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        var remaining = first.Length;
        if (remaining != second.Length)
            return false;

        Span<byte> firstBuffer = stackalloc byte[4096];
        Span<byte> secondBuffer = stackalloc byte[4096];
        while (remaining > 0)
        {
            var size = (int)Math.Min(remaining, firstBuffer.Length);
            first.ReadExactly(firstBuffer[..size]);
            second.ReadExactly(secondBuffer[..size]);
            if (!firstBuffer[..size].SequenceEqual(secondBuffer[..size]))
                return false;
            remaining -= size;
        }
        return true;
    }
}
