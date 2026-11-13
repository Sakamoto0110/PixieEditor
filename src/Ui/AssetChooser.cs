using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;

namespace PixieEditor.Ui;

// Picks a file of one asset kind (a PNG for a sprite, a TrueType font for a text), in a modal window:
// a FolderBrowser that lists the files of that kind, starting in the project's folder for the kind
// (images/, fonts/), or the project's, or the folder of the last choice of that kind; a click on a
// file chooses it. One for the whole editor, drawn every frame at the top level, so the inspector and
// the viewport's menu can both open it.
internal sealed class AssetChooser(string? projectFolder)
{
    private const string FileTitle = "Choose a file";
    private const string FolderTitle = "Choose a folder to import";

    private FolderBrowser? Browser;
    private Action<string>? Chosen;
    private bool OpenRequested;
    private string? Kind = "png";
    private string? LastImportFolder;
    private readonly Dictionary<string, string> LastFolders = [];

    // Shows the chooser for files of `kind` ("png", "ttf"); `chosen` gets the file's full path when
    // one is clicked.
    public void Open(string kind, Action<string> chosen)
    {
        Kind = kind;
        Chosen = chosen;
        var start = LastFolders.GetValueOrDefault(kind) ?? ProjectAssets.StartFolder(projectFolder, kind) ??
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Browser = new FolderBrowser(start, "." + kind);
        OpenRequested = true;
    }

    public void OpenFolder(Action<string> chosen)
    {
        Kind = null;
        Chosen = chosen;
        var start = LastImportFolder ?? projectFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Browser = new FolderBrowser(start);
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
            ImGui.TextDisabled(projectFolder == null
                ? what
                : $"{what} One outside the project is copied into its {ProjectAssets.FolderFor(kind)} folder.");
        }
        else
        {
            ImGui.TextWrapped("Copies PNG and TTF files from this folder and its subfolders into the project.");
        }
        Browser!.Draw("files", Math.Max(120f, ImGui.GetContentRegionAvail().Y - 60f));
        string? chosen = null;
        if (Kind is { } fileKind && Browser.ChosenFile is { } file)
        {
            if (Path.GetDirectoryName(file) is { } folder)
                LastFolders[fileKind] = folder;
            chosen = file;
        }
        if (Kind == null)
        {
            ImGui.BeginDisabled(Browser.Current.Length == 0);
            if (ImGui.Button("Import this folder"))
                chosen = LastImportFolder = Browser.Current;
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
}
