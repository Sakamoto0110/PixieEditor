using PixieEditor.Ui.Browsing;

// The browser model against an in-memory file system (deterministic, any OS), then the portable
// local implementation against a temporary folder. No window is created.
static class Program
{
    static int Passed;

    static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL " + name);
        Passed++;
        Console.WriteLine($"PASS {name}");
    }

    static int Main()
    {
        FakeModel();
        LocalFileSystem();
        Console.WriteLine($"{Passed} checks passed");
        return 0;
    }

    static void FakeModel()
    {
        var fs = new FakeFs(caseInsensitive: true);
        fs.Folder("/r");
        fs.Folder("/r/B10"); fs.Folder("/r/B6"); fs.Folder("/r/a"); fs.Folder("/r/.hidden");
        fs.Folder("/r/Game"); fs.File("/r/Game/Game.pixie", 10);
        fs.File("/r/notes.txt", 5); fs.File("/r/pic.png", 2048);
        fs.Folder("/r/B6/deep");

        var folders = new FileBrowserModel(fs, new BrowserOptions { Mark = e => e.Name == "Game" ? "project" : null }, "/r");
        Check(folders.Current == "/r", "starts in the requested folder");
        Check(string.Join(",", folders.Entries.Select(e => e.Name)) == "a,B6,B10,Game", "folder mode: folders only, hidden left out, natural order");
        Check(folders.MarkOf(folders.Entries.Single(e => e.Name == "Game")) == "project", "entries carry the configured mark");

        folders.SetSort(SortColumn.Name, descending: true);
        Check(folders.Entries[0].Name == "Game" && folders.Entries[^1].Name == "a", "sort can be reversed");
        folders.SetSort(SortColumn.Name, descending: false);

        folders.SetFilter("b");
        Check(string.Join(",", folders.Entries.Select(e => e.Name)) == "B6,B10", "filter is a case-insensitive substring");
        folders.SetFilter("");

        folders.MoveSelection(1);
        Check(folders.Selected?.Name == "a", "Down selects the first row");
        folders.MoveSelection(10);
        Check(folders.Selected?.Name == "Game", "selection is clamped to the list");
        Check(folders.ConfirmFolder == "/r/Game", "a selected folder is the confirm target");

        folders.Open(folders.Entries.Single(e => e.Name == "B6"));
        Check(folders.Current == "/r/B6" && folders.CanGoBack && !folders.CanGoForward, "opening a folder enters it and records history");
        Check(folders.ConfirmFolder == "/r/B6", "without a selection the current folder is the target");
        Check(folders.Back() && folders.Current == "/r" && folders.CanGoForward, "Back returns and enables Forward");
        Check(folders.Forward() && folders.Current == "/r/B6", "Forward goes again");
        Check(folders.Up() && folders.Current == "/r", "Up goes to the parent");
        Check(string.Join("|", folders.Segments.Select(s => s.Path)) == "/|/r", "path segments run from the root");

        Check(!folders.Navigate("/nowhere") && folders.Current == "/r" && folders.Error != null, "a missing folder keeps the current one and reports why");
        fs.Deny("/r/a");
        Check(!folders.Navigate("/r/a") && folders.Current == "/r" && folders.Error!.Contains("permission"), "an unreadable folder is reported, not entered");

        var before = fs.Count;
        Check(!folders.CreateFolder("b10") && fs.Count == before && folders.Error!.Contains("already exists"), "New folder refuses an existing name regardless of case");
        Check(!folders.CreateFolder("notes.txt") && fs.Count == before, "New folder refuses the name of an existing file");
        Check(!folders.CreateFolder("  ") && !folders.CreateFolder("..") && !folders.CreateFolder("x/y") && fs.Count == before, "New folder refuses empty, parent and nested names");
        Check(folders.CreateFolder("New one") && folders.Current == "/r/New one" && fs.Count == before + 1, "New folder creates once and enters it");

        var files = new FileBrowserModel(fs, new BrowserOptions { Mode = BrowseMode.File, Extensions = [".png"] }, "/r");
        Check(string.Join(",", files.Entries.Select(e => e.Name)) == "a,B6,B10,Game,New one,pic.png", "file mode: folders first, then matching files");
        files.Open(files.Entries.Single(e => e.Name == "pic.png"));
        Check(files.ChosenFile == "/r/pic.png", "opening a file chooses it");
        var typed = new FileBrowserModel(fs, new BrowserOptions { Mode = BrowseMode.File, Extensions = [".png"] }, "/r");
        Check(typed.Navigate("/r/pic.png") && typed.ChosenFile == "/r/pic.png", "a typed file path chooses the file");
        Check(!typed.Navigate("/r/notes.txt") && typed.ChosenFile == "/r/pic.png", "a typed path to another kind of file is not chosen");

        var fallback = new FileBrowserModel(fs, new BrowserOptions(), "/missing", "/r");
        Check(fallback.Current == "/r", "a missing start falls back");
        Check(NaturalComparer.Instance.Compare("img2", "img10") < 0 && NaturalComparer.Instance.Compare("v010", "v9") > 0, "natural comparer orders numbers by value");
    }

    static void LocalFileSystem()
    {
        var root = Path.Combine(Path.GetTempPath(), "pixie-folder-browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fs = new LocalFileSystemView();
            Directory.CreateDirectory(Path.Combine(root, "Projects 2"));
            Directory.CreateDirectory(Path.Combine(root, "Projects 10"));
            File.WriteAllText(Path.Combine(root, "keep.txt"), "preserve");
            var model = new FileBrowserModel(fs, new BrowserOptions(), root);
            Check(string.Join(",", model.Entries.Select(e => e.Name)) == "Projects 2,Projects 10", "local listing in natural order");
            Check(!model.CreateFolder("keep.txt") && File.ReadAllText(Path.Combine(root, "keep.txt")) == "preserve", "local: an existing file is never replaced");
            Check(!model.CreateFolder("Projects 2"), "local: an existing folder is refused");
            Check(model.CreateFolder("Ação å") && Directory.Exists(Path.Combine(root, "Ação å")), "local: non-ASCII folder names are created");
            Check(Throws(() => fs.CreateFolder(Path.Combine(root, "Projects 10"))), "local: the file system layer itself refuses an existing folder");
            Check(fs.Places().Count > 0 && fs.Places().All(p => Directory.Exists(p.Path)), "local: places exist");
            Check(fs.FullPath("~") == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "local: ~ is the home folder");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (IOException) { return true; }
    }
}

// An in-memory file system with "/" paths, so the model can be checked the same way on every OS.
sealed class FakeFs(bool caseInsensitive) : IFileSystemView
{
    private readonly Dictionary<string, (EntryKind Kind, long Size)> Items = new(caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) { ["/"] = (EntryKind.Folder, 0) };
    private readonly HashSet<string> Denied = [];

    public bool CaseInsensitive => caseInsensitive;
    public int Count => Items.Count;
    public void Folder(string p) => Items[p] = (EntryKind.Folder, 0);
    public void File(string p, long size) => Items[p] = (EntryKind.File, size);
    public void Deny(string p) => Denied.Add(p);

    public string FullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Enter a path.");
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }

    public bool FolderExists(string path) => Items.TryGetValue(path, out var i) && i.Kind == EntryKind.Folder;
    public bool FileExists(string path) => Items.TryGetValue(path, out var i) && i.Kind == EntryKind.File;

    public IReadOnlyList<BrowserEntry> List(string folder)
    {
        if (Denied.Contains(folder)) throw new UnauthorizedAccessException();
        var prefix = folder == "/" ? "/" : folder + "/";
        return Items.Where(kv => kv.Key != folder && kv.Key.StartsWith(prefix, StringComparison.Ordinal) && !kv.Key[prefix.Length..].Contains('/'))
            .Select(kv =>
            {
                var name = kv.Key[prefix.Length..];
                return new BrowserEntry(name, kv.Key, kv.Value.Kind, kv.Value.Kind == EntryKind.File ? kv.Value.Size : null,
                    new DateTime(2026, 1, 1), name.StartsWith('.'), false);
            }).ToList();
    }

    public string Combine(string folder, string name) => folder == "/" ? "/" + name : folder + "/" + name;

    public string? Parent(string path)
    {
        if (path == "/") return null;
        var i = path.LastIndexOf('/');
        return i <= 0 ? "/" : path[..i];
    }

    public IReadOnlyList<BrowserPlace> Places() => [new("Root", "/")];

    public void CreateFolder(string path)
    {
        if (Items.ContainsKey(path)) throw new IOException("exists");
        Items[path] = (EntryKind.Folder, 0);
    }
}
