using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;

namespace PixieEditor.Ui;

// The current ImGui view for a navigation session. Layout and input bindings live here.
internal sealed class FolderBrowserView(IFolderBrowser browser)
{
    private static readonly Vector4 ProjectColor = new(0.55f, 0.8f, 1.0f, 1.0f);
    private static readonly Vector4 ErrorColor = new(1.0f, 0.45f, 0.4f, 1.0f);
    private static readonly Vector4 FileColor = new(0.75f, 0.95f, 0.7f, 1.0f);

    private string Typed = browser.Current;
    private bool CreatingFolder;
    private string NewFolderName = "";
    private bool FocusFolderName;

    public IFolderBrowser Browser => browser;

    public void Draw(string id, float height)
    {
        ImGui.PushID(id);
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
        var io = ImGui.GetIO();
        if (focused && !ImGui.IsAnyItemActive())
        {
            if (io.KeyAlt && ImGui.IsKeyPressed(ImGuiKey.LeftArrow)) Navigate(browser.Back);
            if (io.KeyAlt && ImGui.IsKeyPressed(ImGuiKey.RightArrow)) Navigate(browser.Forward);
            if (io.KeyAlt && ImGui.IsKeyPressed(ImGuiKey.UpArrow)) Navigate(browser.Up);
            if (ImGui.IsKeyPressed(ImGuiKey.F5)) Navigate(browser.Refresh);
        }
        if (ImGui.Button("New folder"))
        {
            CreatingFolder = true;
            NewFolderName = "";
            FocusFolderName = true;
            browser.ClearError();
        }
        if (CreatingFolder)
        {
            if (FocusFolderName)
            {
                ImGui.SetKeyboardFocusHere();
                FocusFolderName = false;
            }
            ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X - 160));
            var confirm = ImGui.InputText("##newFolderName", ref NewFolderName, 256, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            if ((ImGui.Button("Create folder") || confirm) && Navigate(() => browser.CreateFolder(NewFolderName)))
                CreatingFolder = false;
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                CreatingFolder = false;
                browser.ClearError();
            }
        }
        if (ImGui.Button("Up")) Navigate(browser.Up);
        ImGui.SameLine();
        if (focused && io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.L)) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##path", ref Typed, 4096, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            Navigate(() => browser.Go(Typed));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(browser.Extension == null ? "Type a folder and press Enter." : "Type a folder or a file and press Enter.");

        if (OperatingSystem.IsWindows())
        {
            foreach (var place in browser.Places.Where(place => Path.GetPathRoot(place.Path) == place.Path))
            {
                if (ImGui.SmallButton(place.Label))
                    Navigate(() => browser.Go(place.Path));
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        if (ImGui.BeginChild("##folders", new Vector2(0, height), ImGuiChildFlags.Borders))
        {
            if (browser.Entries.Count == 0)
                ImGui.TextDisabled(browser.Extension == null ? "No folders here." : $"No folders or {browser.Extension} files here.");
            foreach (var entry in browser.Entries)
            {
                var colored = entry.Kind != BrowserEntryKind.Folder;
                if (colored)
                    ImGui.PushStyleColor(ImGuiCol.Text, entry.Kind == BrowserEntryKind.Project ? ProjectColor : FileColor);
                var name = entry.Kind == BrowserEntryKind.Project ? $"{entry.Name}    (project)" : entry.Name;
                var clicked = ImGui.Selectable($"{name}##{entry.Path}");
                if (colored) ImGui.PopStyleColor();
                if (clicked)
                {
                    Navigate(() => browser.Go(entry.Path));
                    break;
                }
            }
        }

        ImGui.EndChild();
        if (browser.Error is { } error)
            ImGui.TextColored(ErrorColor, error);
        ImGui.PopID();
    }

    private bool Navigate(Func<bool> action)
    {
        if (!action()) return false;
        Typed = browser.Current;
        return true;
    }
}
