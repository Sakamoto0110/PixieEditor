namespace PixieEditor.Ui.Browsing;

// The machine's own file system through portable .NET only (System.IO and Environment).
// The one platform branch is how roots are named: drive letters on Windows, "/" and common
// mount folders elsewhere; both come from DriveInfo, which every .NET platform implements.
internal sealed class LocalFileSystemView : IFileSystemView
{
    public bool CaseInsensitive { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public string FullPath(string path)
    {
        path = path.Trim().Trim('"');
        if (path.Length == 0) throw new ArgumentException("Enter a path.");
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            path = Path.Combine(Home, path.Length > 2 ? path[2..] : "");
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }

    public bool FolderExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<BrowserEntry> List(string folder)
    {
        var entries = new List<BrowserEntry>();
        foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", new EnumerationOptions
                 {
                     IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = 0,
                     ReturnSpecialDirectories = false,
                 }))
        {
            try
            {
                var link = info.LinkTarget != null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
                var hidden = info.Attributes.HasFlag(FileAttributes.Hidden) || info.Name.StartsWith('.');
                if (info is DirectoryInfo)
                    entries.Add(new BrowserEntry(info.Name, info.FullName, EntryKind.Folder, null, info.LastWriteTime, hidden, link));
                else
                    entries.Add(new BrowserEntry(info.Name, info.FullName, EntryKind.File, ((FileInfo)info).Length, info.LastWriteTime, hidden, link));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // An entry that vanished or cannot be read is left out rather than failing the listing.
            }
        }
        return entries;
    }

    public string Combine(string folder, string name) => Path.Combine(folder, name);

    public string? Parent(string path) => Directory.GetParent(path)?.FullName;

    public IReadOnlyList<BrowserPlace> Places()
    {
        var places = new List<BrowserPlace>();
        void Add(string label, string? path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) &&
                !places.Any(p => string.Equals(p.Path, path, CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                places.Add(new BrowserPlace(label, path));
        }

        Add("Home", Home);
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add("Downloads", Path.Combine(Home, "Downloads"));
        foreach (var drive in SafeDrives())
            Add(drive.Label, drive.Path);
        return places;
    }

    public void CreateFolder(string path)
    {
        // Directory.CreateDirectory succeeds silently on an existing folder, so refuse first.
        if (Path.Exists(path)) throw new IOException("A file or folder with that name already exists.");
        Directory.CreateDirectory(path);
    }

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static IEnumerable<BrowserPlace> SafeDrives()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }
        foreach (var drive in drives)
        {
            bool ready;
            try { ready = drive.IsReady; } catch (IOException) { ready = false; }
            if (!ready) continue;
            var root = drive.RootDirectory.FullName;
            if (OperatingSystem.IsWindows())
                yield return new BrowserPlace(root.TrimEnd('\\'), root);
            else if (root == "/" || root.StartsWith("/media/", StringComparison.Ordinal) ||
                     root.StartsWith("/mnt/", StringComparison.Ordinal) || root.StartsWith("/Volumes/", StringComparison.Ordinal))
                yield return new BrowserPlace(root == "/" ? "/" : Path.GetFileName(root.TrimEnd('/')), root);
        }
    }
}
