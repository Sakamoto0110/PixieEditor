using System.Numerics;
using Hexa.NET.ImGui;

namespace PixieEditor.Ui.Browsing;

// Draws a FileBrowserModel with ImGui: toolbar (Back, Forward, Up, Refresh, New folder),
// the path as clickable segments that switch to an editable field, places on the left,
// a filter and a sortable table on the right. It holds only drawing state; every rule is
// in the model, so another view (or a test) can drive the same model.
internal sealed class FileBrowserView(FileBrowserModel model)
{
    private static readonly Vector4 MarkColor = new(0.55f, 0.8f, 1.0f, 1.0f);
    private static readonly Vector4 FileColor = new(0.75f, 0.95f, 0.7f, 1.0f);
    private static readonly Vector4 LinkColor = new(0.85f, 0.75f, 1.0f, 1.0f);
    private static readonly Vector4 DimColor = new(0.6f, 0.6f, 0.6f, 1.0f);
    private static readonly Vector4 ErrorColor = new(1.0f, 0.45f, 0.4f, 1.0f);
    private const float PlacesWidth = 130f;

    private bool EditingPath;
    private bool FocusPath;
    private string Typed = "";
    private bool CreatingFolder;
    private bool FocusFolderName;
    private string NewFolderName = "";
    private bool ScrollToSelection;

    public FileBrowserModel Model => model;

    public void Draw(string id, float height)
    {
        ImGui.PushID(id);
        DrawToolbar();
        DrawPath();
        if (CreatingFolder) DrawNewFolder();

        var listHeight = Math.Max(120f, height);
        if (ImGui.BeginChild("##places", new Vector2(PlacesWidth, listHeight), ImGuiChildFlags.Borders))
        {
            foreach (var place in model.Places)
            {
                var here = string.Equals(place.Path.TrimEnd('\\', '/'), model.Current.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable($"{place.Label}##{place.Path}", here)) model.Navigate(place.Path);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(place.Path);
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(-1);
        // The model owns the filter; the field shows it even when set by code.
        var filter = model.Filter;
        if (ImGui.InputTextWithHint("##filter", "Filter by name", ref filter, 256))
            model.SetFilter(filter);
        DrawTable(listHeight - ImGui.GetFrameHeightWithSpacing());
        ImGui.EndGroup();

        if (model.Error != null) ImGui.TextColored(ErrorColor, model.Error);
        else ImGui.TextColored(DimColor, Summary());
        ImGui.PopID();
    }

    private string Summary()
    {
        var folders = model.Entries.Count(e => e.Kind == EntryKind.Folder);
        var files = model.Entries.Count - folders;
        var counts = model.Options.Mode == BrowseMode.File ? $"{folders} folders, {files} files" : $"{folders} folders";
        return model.Selected is { } s ? $"{counts} · selected: {s.Name}" : counts;
    }

    private void DrawToolbar()
    {
        ImGui.BeginDisabled(!model.CanGoBack);
        if (ImGui.Button("<##back")) Navigate(model.Back);
        ImGui.EndDisabled();
        Tip("Back (Alt+Left)");
        ImGui.SameLine();
        ImGui.BeginDisabled(!model.CanGoForward);
        if (ImGui.Button(">##forward")) Navigate(model.Forward);
        ImGui.EndDisabled();
        Tip("Forward (Alt+Right)");
        ImGui.SameLine();
        ImGui.BeginDisabled(!model.CanGoUp);
        if (ImGui.Button("Up")) Navigate(model.Up);
        ImGui.EndDisabled();
        Tip("Parent folder (Backspace)");
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) model.Refresh();
        if (model.Options.AllowCreateFolder)
        {
            ImGui.SameLine();
            if (ImGui.Button("New folder"))
            {
                CreatingFolder = true;
                FocusFolderName = true;
                NewFolderName = "";
                model.ClearError();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button(EditingPath ? "Segments" : "Edit path")) StartEditingPath(!EditingPath);
        Tip("Type or paste a path (Ctrl+L)");
    }

    private void DrawPath()
    {
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.L))
            StartEditingPath(true);
        if (EditingPath)
        {
            if (FocusPath) { ImGui.SetKeyboardFocusHere(); FocusPath = false; }
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##path", ref Typed, 1024, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (model.Navigate(Typed)) EditingPath = false;
            }
            if (ImGui.IsItemDeactivated() && ImGui.IsKeyPressed(ImGuiKey.Escape)) EditingPath = false;
            return;
        }

        // Segments wrap onto the next line when the path is longer than the row.
        var room = ImGui.GetContentRegionAvail().X;
        var used = 0f;
        var segments = model.Segments;
        for (var i = 0; i < segments.Count; i++)
        {
            var (label, path) = segments[i];
            var width = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2 + 14f;
            if (i > 0)
            {
                if (used + width < room) ImGui.SameLine(0, 2);
                else used = 0;
            }
            if (ImGui.SmallButton($"{label}##seg{i}")) model.Navigate(path);
            used += width;
            if (i < segments.Count - 1)
            {
                ImGui.SameLine(0, 2);
                ImGui.TextColored(DimColor, ">");
            }
        }
        // A click on the empty space after the segments edits the path, as in common file dialogs.
        ImGui.SameLine();
        if (ImGui.InvisibleButton("##editpath", new Vector2(Math.Max(1f, ImGui.GetContentRegionAvail().X), ImGui.GetFrameHeight())))
            StartEditingPath(true);
        Tip("Click to type a path");
    }

    private void StartEditingPath(bool on)
    {
        EditingPath = on;
        FocusPath = on;
        Typed = model.Current;
    }

    private void DrawNewFolder()
    {
        if (FocusFolderName) { ImGui.SetKeyboardFocusHere(); FocusFolderName = false; }
        ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X - 170));
        var confirm = ImGui.InputTextWithHint("##newFolder", "New folder name", ref NewFolderName, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Create folder") || confirm) && model.CreateFolder(NewFolderName))
            CreatingFolder = false;
        ImGui.SameLine();
        if (ImGui.Button("Cancel##newFolder") || ImGui.IsKeyPressed(ImGuiKey.Escape) && !EditingPath)
        {
            CreatingFolder = false;
            model.ClearError();
        }
    }

    private void DrawTable(float height)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp;
        // Folders have no size, so the Size column only appears when files are listed.
        var showSize = model.Options.Mode == BrowseMode.File;
        if (!ImGui.BeginTable("##entries", showSize ? 3 : 2, flags, new Vector2(-1, Math.Max(80f, height)))) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 4f);
        ImGui.TableSetupColumn("Modified", ImGuiTableColumnFlags.WidthStretch, 1.6f);
        if (showSize) ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        Header(0, "Name", SortColumn.Name);
        Header(1, "Modified", SortColumn.Modified);
        if (showSize) Header(2, "Size", SortColumn.Size);

        var focused = ImGui.IsWindowFocused();
        if (focused) HandleKeys();

        if (model.Entries.Count == 0)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextDisabled(model.Filter.Length > 0 ? "Nothing matches the filter." : "This folder is empty.");
        }

        BrowserEntry? open = null;
        foreach (var entry in model.Entries)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            var selected = model.Selected?.FullPath == entry.FullPath;
            var mark = model.MarkOf(entry);
            var color = entry.Link ? LinkColor : mark != null ? MarkColor : entry.Kind == EntryKind.File ? FileColor : (Vector4?)null;
            if (color is { } c) ImGui.PushStyleColor(ImGuiCol.Text, c);
            var label = (entry.Kind == EntryKind.Folder ? "[D] " : "    ") + entry.Name + (mark != null ? $"   ({mark})" : "") + (entry.Link ? "   (link)" : "");
            if (ImGui.Selectable($"{label}##{entry.FullPath}", selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick))
            {
                model.Select(entry);
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) open = entry;
            }
            if (color != null) ImGui.PopStyleColor();
            if (selected && ScrollToSelection) { ImGui.SetScrollHereY(); ScrollToSelection = false; }
            if (ImGui.IsItemHovered() && ImGui.CalcTextSize(label).X > ImGui.GetColumnWidth()) ImGui.SetTooltip(entry.Name);
            ImGui.TableSetColumnIndex(1);
            ImGui.TextColored(DimColor, entry.Modified?.ToString("yyyy-MM-dd HH:mm") ?? "");
            if (showSize)
            {
                ImGui.TableSetColumnIndex(2);
                ImGui.TextColored(DimColor, entry.Size is { } size ? FormatSize(size) : "");
            }
        }
        ImGui.EndTable();
        if (open != null) model.Open(open);
    }

    private void Header(int column, string label, SortColumn sort)
    {
        ImGui.TableSetColumnIndex(column);
        var arrow = model.Sort == sort ? (model.Descending ? " v" : " ^") : "";
        if (ImGui.Selectable($"{label}{arrow}##h{column}"))
            model.SetSort(sort, model.Sort == sort && !model.Descending);
    }

    private void HandleKeys()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) { model.MoveSelection(1); ScrollToSelection = true; }
        if (!ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(ImGuiKey.UpArrow)) { model.MoveSelection(-1); ScrollToSelection = true; }
        if ((ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) && model.Selected is { } s) model.Open(s);
        if (ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(ImGuiKey.UpArrow)) model.Up();
        if (ImGui.IsKeyPressed(ImGuiKey.F5)) model.Refresh();
        if (ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(ImGuiKey.LeftArrow)) model.Back();
        if (ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(ImGuiKey.RightArrow)) model.Forward();
    }

    private void Navigate(Func<bool> move)
    {
        move();
        EditingPath = false;
    }

    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    internal static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}
