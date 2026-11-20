using PixieEditor.Ui.Browsing;

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

// Compatibility entry point for existing screens; FileBrowserModel is the sole state owner.
internal sealed class FolderBrowser : IFolderBrowser
{
    public FolderBrowser(string start, string? extension = null, IEnumerable<BrowserPlace>? places = null)
    {
        Extension = extension;
        Model = new FileBrowserModel(new LocalFileSystemView(), new BrowserOptions
        {
            Mode = extension == null ? BrowseMode.Folder : BrowseMode.File,
            Extensions = extension == null ? [] : [extension],
            Mark = entry => entry.Kind == EntryKind.Folder && IsProject(entry.FullPath) ? "project" : null,
            Places = places?.Select(place => new PixieEditor.Ui.Browsing.BrowserPlace(place.Label, place.Path)).ToArray() ?? [],
        }, start, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    internal FileBrowserModel Model { get; }
    public string Current => Model.Current;
    public string Target => Model.ConfirmFolder;
    public string? ChosenFile => Model.ChosenFile;
    public string? Error => Model.Error;
    public string? Extension { get; }
    public bool CurrentIsProject => IsProject(Current);
    public IReadOnlyList<BrowserEntry> Entries => Model.Entries.Select(entry => new BrowserEntry(
        entry.FullPath, entry.Name, entry.Kind == EntryKind.File ? BrowserEntryKind.File
            : Model.MarkOf(entry) == "project" ? BrowserEntryKind.Project : BrowserEntryKind.Folder)).ToArray();
    public IReadOnlyList<BrowserPlace> Places => Model.Places.Select(place => new BrowserPlace(place.Label, place.Path)).ToArray();
    public bool CanGoBack => Model.CanGoBack;
    public bool CanGoForward => Model.CanGoForward;
    public bool CanGoUp => Model.CanGoUp;
    public bool Go(string path) => Model.Navigate(path);
    public bool Back() => Model.Back();
    public bool Forward() => Model.Forward();
    public bool Up() => Model.Up();
    public bool Refresh() => Model.Refresh();
    public bool CreateFolder(string name) => Model.CreateFolder(name);
    public void ClearError() => Model.ClearError();

    public static bool IsProject(string folder)
    {
        try { return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.pixie").Any(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return false; }
    }
}
