using PixieEditor.Ui.Browsing;

namespace PixieEditor.Ui;

// The Editor's folder (and file) picker, kept as the familiar entry point for its screens. The
// work is done by the reusable browser in Ui/Browsing: a model over IFileSystemView and an ImGui
// view. Folders that hold a project are marked. Given a file extension, it lists those files too
// and opening one chooses it (ChosenFile).
internal sealed class FolderBrowser
{
    private readonly FileBrowserView View;

    public FolderBrowser(string start, string? extension = null)
        : this(new LocalFileSystemView(), start, extension)
    {
    }

    internal FolderBrowser(IFileSystemView fs, string start, string? extension = null)
    {
        var options = new BrowserOptions
        {
            Mode = extension == null ? BrowseMode.Folder : BrowseMode.File,
            Extensions = extension == null ? [] : [extension],
            Mark = e => e.Kind == EntryKind.Folder && IsProject(e.FullPath) ? "project" : null,
        };
        Model = new FileBrowserModel(fs, options, start, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        View = new FileBrowserView(Model);
    }

    internal FileBrowserModel Model { get; }

    public string Current => Model.Current;

    // The file opened, or typed and confirmed with Enter, when the browser lists files.
    public string? ChosenFile => Model.ChosenFile;

    // The folder a confirm button refers to: the selected folder, else the current one.
    public string Target => Model.ConfirmFolder;

    // Whether the current folder holds a project.
    public bool CurrentIsProject => IsProject(Current);

    public static bool IsProject(string folder)
    {
        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.pixie").Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Go(string path) => Model.Navigate(path);

    // Creates one child of the current location and enters it; an existing entry is never reused.
    internal bool CreateFolder(string name) => Model.CreateFolder(name);

    public void Draw(string id, float height) => View.Draw(id, height);
}
