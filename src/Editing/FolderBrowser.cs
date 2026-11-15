namespace PixieEditor.Editing;

internal enum BrowserEntryKind { Folder, Project, File }
internal sealed record BrowserEntry(string Path, string Name, BrowserEntryKind Kind);
internal sealed record BrowserPlace(string Label, string Path);

// One navigation session. The caller owns how to present and confirm a choice.
internal interface IFolderBrowser
{
    string Current { get; }
    string? ChosenFile { get; }
    string? Error { get; }
    string? Extension { get; }
    bool CurrentIsProject { get; }
    IReadOnlyList<BrowserEntry> Entries { get; }
    IReadOnlyList<BrowserPlace> Places { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    bool CanGoUp { get; }
    bool Go(string path);
    bool Back();
    bool Forward();
    bool Up();
    bool Refresh();
    bool CreateFolder(string name);
    void ClearError();
}

internal sealed class FolderBrowser : IFolderBrowser
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private readonly Stack<string> Previous = new();
    private readonly Stack<string> Next = new();

    public FolderBrowser(string start, string? extension = null, IEnumerable<BrowserPlace>? places = null)
    {
        Extension = extension;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var initialPlaces = new List<BrowserPlace>
        {
            new("Home", home),
            new("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            new("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
        };
        if (places != null) initialPlaces.AddRange(places);
        if (OperatingSystem.IsWindows())
            initialPlaces.AddRange(DriveInfo.GetDrives().Where(drive => Directory.Exists(drive.Name))
                .Select(drive => new BrowserPlace(drive.Name, drive.Name)));
        Places = Array.AsReadOnly(initialPlaces.Where(place => Directory.Exists(place.Path))
            .DistinctBy(place => Path.TrimEndingDirectorySeparator(Path.GetFullPath(place.Path)), PathComparer).ToArray());

        if (!OpenFolder(start) && Current.Length == 0)
        {
            var startError = Error;
            if (OpenFolder(home)) Error = startError;
        }
    }

    public string Current { get; private set; } = "";
    public string? ChosenFile { get; private set; }
    public string? Error { get; private set; }
    public string? Extension { get; }
    public bool CurrentIsProject { get; private set; }
    public IReadOnlyList<BrowserEntry> Entries { get; private set; } = Array.Empty<BrowserEntry>();
    public IReadOnlyList<BrowserPlace> Places { get; }
    public bool CanGoBack => Previous.Count > 0;
    public bool CanGoForward => Next.Count > 0;
    public bool CanGoUp => Current.Length > 0 && Directory.GetParent(Current) != null;

    public static bool IsProject(string folder)
    {
        try { return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.pixie").Any(); }
        catch (Exception error) when (IsPathError(error)) { return false; }
    }

    public bool Go(string path)
    {
        try
        {
            var full = Resolve(path);
            if (File.Exists(full))
            {
                if (!MatchesFile(full))
                    return Fail(Extension == null ? "Choose a folder." : $"Choose a file with the {Extension} extension.");
                ChosenFile = full;
                Error = null;
                return true;
            }
            var old = Current;
            if (!OpenFolder(full)) return false;
            if (old.Length > 0 && !PathComparer.Equals(old, Current))
            {
                Previous.Push(old);
                Next.Clear();
            }
            return true;
        }
        catch (Exception error) when (IsPathError(error)) { return Fail(error.Message); }
    }

    public bool Back() => Traverse(Previous, Next);
    public bool Forward() => Traverse(Next, Previous);
    public bool Up() => CanGoUp && Go(Directory.GetParent(Current)!.FullName);
    public bool Refresh() => Current.Length > 0 && OpenFolder(Current);
    public void ClearError() => Error = null;

    public bool CreateFolder(string name)
    {
        name = name.Trim();
        if (Current.Length == 0) return Fail("Choose a parent folder first.");
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            (OperatingSystem.IsWindows() && (name.EndsWith('.') || IsWindowsDeviceName(name))))
            return Fail("Enter a valid folder name.");
        try
        {
            var folder = Path.Combine(Current, name);
            if (Path.Exists(folder)) return Fail("A file or folder with that name already exists.");
            Directory.CreateDirectory(folder);
            return Go(folder);
        }
        catch (Exception error) when (IsPathError(error)) { return Fail(error.Message); }
    }

    private bool Traverse(Stack<string> from, Stack<string> to)
    {
        var old = Current;
        if (!from.TryPeek(out var path) || !OpenFolder(path)) return false;
        from.Pop();
        to.Push(old);
        return true;
    }

    private bool OpenFolder(string path)
    {
        try
        {
            var full = Resolve(path);
            var folders = new List<BrowserEntry>();
            var files = new List<BrowserEntry>();
            var isProject = false;
            foreach (var item in new DirectoryInfo(full).EnumerateFileSystemInfos())
            {
                if (item is DirectoryInfo)
                {
                    if ((item.Attributes & FileAttributes.Hidden) == 0)
                        folders.Add(new(item.FullName, item.Name, IsProject(item.FullName) ? BrowserEntryKind.Project : BrowserEntryKind.Folder));
                }
                else
                {
                    isProject |= item.Extension.Equals(".pixie", PathComparison);
                    if (MatchesFile(item.Name))
                        files.Add(new(item.FullName, item.Name, BrowserEntryKind.File));
                }
            }
            Entries = Array.AsReadOnly(folders.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Concat(files.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)).ToArray());
            Current = full;
            CurrentIsProject = isProject;
            ChosenFile = null;
            Error = null;
            return true;
        }
        catch (Exception error) when (IsPathError(error)) { return Fail(error.Message); }
    }

    private bool MatchesFile(string path) => Extension == ".atlas.json"
        ? ProjectAssets.IsAtlas(path)
        : Extension != null && Path.GetExtension(path).Equals(Extension, StringComparison.OrdinalIgnoreCase);

    private string Resolve(string path)
    {
        if (OperatingSystem.IsWindows() && path.Length == 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
            path += Path.DirectorySeparatorChar;
        return Path.TrimEndingDirectorySeparator(Current.Length == 0 ? Path.GetFullPath(path) : Path.GetFullPath(path, Current));
    }

    private bool Fail(string message)
    {
        ChosenFile = null;
        Error = message;
        return false;
    }

    private static bool IsPathError(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static bool IsWindowsDeviceName(string name)
    {
        var stem = name.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                "123456789¹²³".Contains(stem[3]));
    }
}
