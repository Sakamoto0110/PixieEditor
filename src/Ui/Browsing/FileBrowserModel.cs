namespace PixieEditor.Ui.Browsing;

internal enum SortColumn { Name, Modified, Size }

// The browser's state and rules, with no drawing: where it is, what it lists, history,
// path segments, filtering, sorting, selection and folder creation. Views read it and call it.
internal sealed class FileBrowserModel
{
    private readonly IFileSystemView Fs;
    private readonly Stack<string> BackStack = new();
    private readonly Stack<string> ForwardStack = new();
    private IReadOnlyList<BrowserEntry> All = [];
    private readonly Dictionary<string, string?> Marks = new();

    public FileBrowserModel(IFileSystemView fs, BrowserOptions options, string start, string? fallback = null)
    {
        Fs = fs;
        Options = options;
        Current = "";
        if (!Navigate(start, remember: false) && fallback != null)
            Navigate(fallback, remember: false);
    }

    public BrowserOptions Options { get; }
    public string Current { get; private set; }
    public string? Error { get; private set; }
    public string Filter { get; private set; } = "";
    public SortColumn Sort { get; private set; } = SortColumn.Name;
    public bool Descending { get; private set; }
    public IReadOnlyList<BrowserEntry> Entries { get; private set; } = [];
    public BrowserEntry? Selected { get; private set; }
    // The file chosen by opening it (file mode only).
    public string? ChosenFile { get; private set; }
    public bool CanGoBack => BackStack.Count > 0;
    public bool CanGoForward => ForwardStack.Count > 0;
    public bool CanGoUp => Current.Length > 0 && Fs.Parent(Current) != null;
    public IReadOnlyList<BrowserPlace> Places => Fs.Places();

    public string? MarkOf(BrowserEntry entry)
    {
        if (Options.Mark == null) return null;
        if (!Marks.TryGetValue(entry.FullPath, out var mark))
            Marks[entry.FullPath] = mark = Options.Mark(entry);
        return mark;
    }

    // The current path as clickable segments, from the root down.
    public IReadOnlyList<(string Label, string Path)> Segments
    {
        get
        {
            var parts = new List<(string, string)>();
            for (var path = Current; !string.IsNullOrEmpty(path); path = Fs.Parent(path)!)
            {
                var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                parts.Add((name.Length == 0 ? path : name, path));
                if (Fs.Parent(path) == null) break;
            }
            parts.Reverse();
            return parts;
        }
    }

    // Goes to a folder; in file mode a typed path to a matching file chooses it.
    public bool Navigate(string path, bool remember = true)
    {
        string full;
        try { full = Fs.FullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Error = e is ArgumentException a && a.Message.Length > 0 ? a.Message : "That is not a usable path.";
            return false;
        }

        if (Options.Mode == BrowseMode.File && Fs.FileExists(full) && Accepts(full))
        {
            ChosenFile = full;
            return true;
        }
        if (!Fs.FolderExists(full))
        {
            Error = $"There is no folder {full}.";
            return false;
        }

        IReadOnlyList<BrowserEntry> listing;
        try { listing = Fs.List(full); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = e is UnauthorizedAccessException ? $"No permission to open {full}." : e.Message;
            return false;
        }

        if (remember && Current.Length > 0 && !SamePath(Current, full))
        {
            BackStack.Push(Current);
            ForwardStack.Clear();
        }
        Current = full;
        All = listing;
        Marks.Clear();
        Selected = null;
        Error = null;
        Apply();
        return true;
    }

    public bool Up() => Fs.Parent(Current) is { } parent && Navigate(parent);
    public bool Refresh() => Navigate(Current, remember: false);

    public bool Back()
    {
        if (BackStack.Count == 0) return false;
        var target = BackStack.Pop();
        var from = Current;
        if (!Navigate(target, remember: false)) return false;
        ForwardStack.Push(from);
        return true;
    }

    public bool Forward()
    {
        if (ForwardStack.Count == 0) return false;
        var target = ForwardStack.Pop();
        var from = Current;
        if (!Navigate(target, remember: false)) return false;
        BackStack.Push(from);
        return true;
    }

    public void SetFilter(string filter) { Filter = filter; Apply(); }

    public void SetSort(SortColumn column, bool descending) { Sort = column; Descending = descending; Apply(); }

    public void Select(BrowserEntry? entry) => Selected = entry;

    // Moves the selection by a number of rows, clamped to the list.
    public void MoveSelection(int delta)
    {
        if (Entries.Count == 0) return;
        var index = Selected == null ? (delta > 0 ? -1 : Entries.Count) : IndexOf(Selected);
        Selected = Entries[Math.Clamp(index + delta, 0, Entries.Count - 1)];
    }

    // Opens an entry: a folder is entered, a file is chosen (file mode).
    public void Open(BrowserEntry entry)
    {
        if (entry.Kind == EntryKind.Folder) Navigate(entry.FullPath);
        else if (Options.Mode == BrowseMode.File) ChosenFile = entry.FullPath;
    }

    // The folder a folder-mode confirm refers to: the selected folder, else the current one.
    public string ConfirmFolder => Selected is { Kind: EntryKind.Folder } folder ? folder.FullPath : Current;

    // Creates one child of the current folder and enters it. Never reuses or overwrites an entry.
    public bool CreateFolder(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.IndexOfAny(['/', '\\']) >= 0 || OperatingSystem.IsWindows() && name.EndsWith('.'))
        {
            Error = "Enter a valid folder name.";
            return false;
        }
        var comparison = Fs.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (All.Any(e => string.Equals(e.Name, name, comparison)))
        {
            Error = $"\"{name}\" already exists here.";
            return false;
        }
        var path = Fs.Combine(Current, name);
        try { Fs.CreateFolder(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Error = e is UnauthorizedAccessException ? "No permission to create a folder here." : e.Message;
            return false;
        }
        return Navigate(path);
    }

    public void ClearError() => Error = null;
    public void ClearChosenFile() => ChosenFile = null;

    private bool Accepts(string file) =>
        Options.Extensions.Count == 0 || Options.Extensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase));

    private bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar),
            Fs.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private int IndexOf(BrowserEntry entry)
    {
        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].FullPath == entry.FullPath) return i;
        return -1;
    }

    private void Apply()
    {
        IEnumerable<BrowserEntry> rows = All;
        if (!Options.ShowHidden) rows = rows.Where(e => !e.Hidden);
        if (Options.Mode == BrowseMode.Folder) rows = rows.Where(e => e.Kind == EntryKind.Folder);
        else rows = rows.Where(e => e.Kind == EntryKind.Folder || Accepts(e.FullPath));
        if (Filter.Trim() is { Length: > 0 } f) rows = rows.Where(e => e.Name.Contains(f, StringComparison.OrdinalIgnoreCase));

        IComparer<BrowserEntry> byColumn = Sort switch
        {
            SortColumn.Modified => Comparer<BrowserEntry>.Create((x, y) => Nullable.Compare(x.Modified, y.Modified)),
            SortColumn.Size => Comparer<BrowserEntry>.Create((x, y) => Nullable.Compare(x.Size, y.Size)),
            _ => Comparer<BrowserEntry>.Create((x, y) => NaturalComparer.Instance.Compare(x.Name, y.Name)),
        };
        var ordered = rows.ToList();
        // Folders always come first; the column order applies within each kind.
        ordered.Sort((x, y) =>
        {
            if (x.Kind != y.Kind) return x.Kind == EntryKind.Folder ? -1 : 1;
            var c = byColumn.Compare(x, y);
            if (c == 0) c = NaturalComparer.Instance.Compare(x.Name, y.Name);
            return Descending ? -c : c;
        });
        Entries = ordered;
        if (Selected != null && IndexOf(Selected) < 0) Selected = null;
    }
}

// Orders "B6" before "B10": digit runs compare by value, the rest case-insensitively.
internal sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
            else
            {
                var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        var rest = (x.Length - i).CompareTo(y.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }
}
