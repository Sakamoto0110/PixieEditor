namespace PixieEditor.Ui.Browsing;

internal enum BrowseMode { Folder, File }

// What one use of the browser is for. Screens configure it instead of subclassing the view.
internal sealed record BrowserOptions
{
    public BrowseMode Mode { get; init; } = BrowseMode.Folder;
    // File mode: the extensions listed, with the dot (".png"). Empty lists every file.
    public IReadOnlyList<string> Extensions { get; init; } = [];
    public IReadOnlyList<BrowserPlace> Places { get; init; } = [];
    public bool ShowHidden { get; init; }
    public bool AllowCreateFolder { get; init; } = true;
    // A short mark shown beside an entry (for example "project"), or null.
    public Func<BrowserEntry, string?>? Mark { get; init; }
}
