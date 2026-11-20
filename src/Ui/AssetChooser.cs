using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;

namespace PixieEditor.Ui;

// Picks a PNG, TrueType font, or project atlas in a modal window:
// a FolderBrowser that lists the files of that kind, starting in the project's folder for the kind
// (images/, fonts/), or the project's, or the folder of the last choice of that kind; a click on a
// file chooses it. One for the whole editor, drawn every frame at the top level, so the inspector and
// the viewport's menu can both open it.
internal sealed class AssetChooser(string? projectFolder)
{
    private const string FileTitle = "Choose a file";
    private const string FolderTitle = "Choose a folder to import";

    private FolderBrowserView? Browser;
    private Action<string>? Chosen;
    private bool OpenRequested;
    private string? Kind = "png";
    private string? LastImportFolder;
    private readonly Dictionary<string, string> LastFolders = [];

    // Shows the chooser for files of `kind`; `chosen` gets the file's full path when
    // one is clicked.
    public void Open(string kind, Action<string> chosen)
    {
        Kind = kind;
        Chosen = chosen;
        var start = LastFolders.GetValueOrDefault(kind) ?? ProjectAssets.StartFolder(projectFolder, kind) ??
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Browser = new(new FolderBrowser(start, "." + kind, Places(start)));
        OpenRequested = true;
    }

    public void OpenFolder(Action<string> chosen)
    {
        Kind = null;
        Chosen = chosen;
        var start = LastImportFolder ?? projectFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Browser = new(new FolderBrowser(start, places: Places(start)));
        OpenRequested = true;
    }

    public void Draw()
    {
        var title = Kind == null ? FolderTitle : FileTitle;
        if (OpenRequested)
        {
            ImGui.OpenPopup(title);
            OpenRequested = false;
        }

        ImGui.SetNextWindowSize(new Vector2(560, 420), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal(title, ImGuiWindowFlags.None))
            return;

        if (Kind is { } kind)
        {
            var what = kind == "png" ? "A PNG file." : kind == "ttf" ? "A TrueType font (.ttf)." : $"A .{kind} file.";
            ImGui.TextDisabled(kind == "atlas.json" ? "Choose a sprite atlas inside the project." : projectFolder == null
                ? what
                : $"{what} One outside the project is copied into its {ProjectAssets.FolderFor(kind)} folder.");
        }
        else
        {
            ImGui.TextWrapped("Copies PNG and TTF files from this folder and its subfolders into the project.");
        }
        Browser!.Draw("files", Math.Max(120f, ImGui.GetContentRegionAvail().Y - 60f));
        string? chosen = null;
        if (Kind is { } fileKind && Browser.Browser.ChosenFile is { } file)
        {
            if (Path.GetDirectoryName(file) is { } folder)
                LastFolders[fileKind] = folder;
            chosen = file;
        }
        if (Kind != null)
        {
            var selected = Browser.Browser.Model.Selected is { Kind: Browsing.EntryKind.File } entry ? entry.FullPath : null;
            ImGui.BeginDisabled(selected == null);
            if (ImGui.Button("Choose", new Vector2(120, 0)) && selected != null)
            {
                chosen = selected;
                if (Path.GetDirectoryName(selected) is { } folder) LastFolders[Kind] = folder;
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        if (Kind == null)
        {
            ImGui.BeginDisabled(Browser.Browser.Target.Length == 0);
            if (ImGui.Button("Import this folder"))
                chosen = LastImportFolder = Browser.Browser.Target;
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        var cancel = ImGui.Button("Cancel", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape);
        if (cancel || chosen != null)
        {
            var callback = Chosen;
            Chosen = null;
            ImGui.CloseCurrentPopup();
            if (!cancel && chosen != null)
                callback?.Invoke(chosen);
        }

        ImGui.EndPopup();
    }

    private IEnumerable<BrowserPlace> Places(string start)
    {
        if (projectFolder != null) yield return new("Project", projectFolder);
        yield return new("Last location", start);
    }
}
