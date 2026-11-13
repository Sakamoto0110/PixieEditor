using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed class AssetManagerPanel(EngineSession session, AssetChooser chooser, Selection selection, Authoring authoring)
{
    private static readonly string[] Filters = ["All assets", "Images", "Fonts", "Sprite atlases"];
    private sealed record AtlasEntry(ProjectAtlas? Atlas, string? Problem);
    private sealed record Scan(ProjectAssets.Catalog Catalog, Dictionary<string, AtlasEntry> Atlases, int Revision);
    private bool visible;
    private bool focusRequested;
    private string search = "";
    private int filter;
    private string? selected;
    private bool selectedAtlas;
    private string? selectedFrame;
    private ProjectAtlas? filteredAtlas;
    private ProjectAtlas.Frame[] filteredFrames = [];
    private string frameSearch = "";
    private string[] paths = [];
    private string[] filtered = [];
    private string[] browseProblems = [];
    private int catalogRevision = -1;
    private Dictionary<string, AtlasEntry> atlases = new(StringComparer.OrdinalIgnoreCase);
    private Task<Scan>? scan;
    private Task<ProjectAssets.ImportResult>? import;
    private ProjectAssets.ImportResult? importResult;

    public void Open() => visible = true;

    public void OpenFrame(ProjectAtlas.Reference reference)
    {
        Open();
        focusRequested = true;
        Select(reference.Atlas, atlas: true);
        selectedFrame = reference.FrameId;
        frameSearch = "";
        filteredAtlas = null;
        search = "";
        filter = 3;
        FilterPaths();
        catalogRevision = -1;
    }

    public void Draw()
    {
        var project = session.ProjectFolder;
        if (project == null)
            return;

        FinishTasks();
        if (!visible)
            return;
        if (scan == null && import == null && catalogRevision != ProjectAssets.Revision)
            StartScan(project);

        ImGui.SetNextWindowSize(new Vector2(620, 580), ImGuiCond.FirstUseEver);
        if (focusRequested)
        {
            ImGui.SetNextWindowFocus();
            focusRequested = false;
        }
        if (ImGui.Begin("Asset Manager", ref visible))
        {
            DrawActions(project);
            DrawFilter();
            DrawList();
            DrawPreview();
        }
        ImGui.End();
    }

    private void DrawActions(string project)
    {
        ImGui.BeginDisabled(import != null);
        if (ImGui.Button("Import..."))
            ImGui.OpenPopup("Import project assets");
        if (ImGui.BeginPopup("Import project assets"))
        {
            if (ImGui.MenuItem("PNG image..."))
                chooser.Open("png", file => StartImport(() => ImportFile(project, file)));
            if (ImGui.MenuItem("TrueType font..."))
                chooser.Open("ttf", file => StartImport(() => ImportFile(project, file)));
            if (ImGui.MenuItem("Folder and subfolders..."))
                chooser.OpenFolder(folder => StartImport(() => ProjectAssets.ImportFolder(project, folder)));
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(scan != null);
        if (ImGui.Button("Refresh"))
            StartScan(project);
        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (import != null)
            ImGui.TextDisabled("Importing assets...");
        if (importResult is { } result)
        {
            ImGui.TextDisabled($"{result.Copied} imported, {result.Existing} already in project, {result.Skipped} unsupported");
            DrawProblems("Import errors", result.Problems);
        }
        DrawProblems("Browse errors", browseProblems);
    }

    private void DrawFilter()
    {
        ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X - 130));
        var changed = ImGui.InputTextWithHint("##search", "Search asset paths", ref search, 512);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("##asset-type", ref filter, Filters, Filters.Length) || changed)
            FilterPaths();
        ImGui.TextDisabled(scan == null ? $"{filtered.Length} of {paths.Length} assets" : "Scanning project assets...");
    }

    private void DrawList()
    {
        var height = Math.Max(70f, ImGui.GetContentRegionAvail().Y * (selectedAtlas ? 0.32f : 0.6f));
        if (ImGui.BeginChild("##project-assets", new Vector2(0, height), ImGuiChildFlags.Borders,
                ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (filtered.Length == 0 && scan == null)
                ImGui.TextDisabled(paths.Length == 0 ? "No PNG images, TrueType fonts or sprite atlases in this project." : "No matching assets.");

            var clipper = ImGui.ImGuiListClipper();
            try
            {
                ImGui.Begin(clipper, filtered.Length);
                while (ImGui.Step(clipper))
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; ++i)
                    {
                        var path = filtered[i];
                        ImGui.PushID(i);
                        // A file name can contain ImGui's label delimiter. Keep it visible.
                        if (ImGui.Selectable(VisibleLabel(path), StringComparer.OrdinalIgnoreCase.Equals(path, selected)))
                            Select(path, ProjectAssets.IsAtlas(path));
                        ImGui.PopID();
                    }
                }
            }
            finally { ImGui.Destroy(clipper); }
        }
        ImGui.EndChild();
    }

    private void DrawPreview()
    {
        if (selected == null)
            return;
        ImGui.TextWrapped(selected);
        if (ImGui.SmallButton("Copy project path"))
            ImGui.SetClipboardText(selected);
        if (selectedAtlas)
            DrawAtlas(selected);
        else if (ProjectAssets.IsImage(selected))
            AssetPreview.DrawImage(session, selected, Math.Clamp(ImGui.GetContentRegionAvail().Y, 32f, 160f));
        else
            ImGui.TextDisabled("TrueType font (.ttf)");
    }

    private void DrawAtlas(string path)
    {
        if (!ProjectAssets.IsAtlasAssetPath(path, ".atlas.json"))
        {
            ImGui.TextWrapped("Expected a project-relative .atlas.json path.");
            return;
        }
        if (!atlases.TryGetValue(path, out var entry))
        {
            ImGui.TextWrapped(scan != null ? "Reading atlas..." : $"Atlas descriptor is missing: {path}");
            return;
        }
        if (entry.Problem != null)
            ImGui.TextWrapped(entry.Problem);
        if (entry.Atlas is not { } atlas)
            return;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##atlas-frame-search", "Search frame names or IDs", ref frameSearch, 256) ||
            !ReferenceEquals(filteredAtlas, atlas))
        {
            filteredAtlas = atlas;
            filteredFrames = atlas.Frames.Where(frame =>
                frame.Name.Contains(frameSearch, StringComparison.OrdinalIgnoreCase) ||
                frame.Id.Contains(frameSearch, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        if (ImGui.BeginChild("##atlas-frames", new Vector2(0, Math.Clamp(ImGui.GetContentRegionAvail().Y * 0.42f, 58f, 150f)),
                ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                ImGui.Begin(clipper, filteredFrames.Length);
                while (ImGui.Step(clipper))
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; ++i)
                    {
                        var frame = filteredFrames[i];
                        ImGui.PushID(frame.Id);
                        var label = frame.Name.Replace('\r', ' ').Replace('\n', ' ');
                        if (ImGui.Selectable(VisibleLabel(label), frame.Id == selectedFrame))
                            selectedFrame = frame.Id;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(frame.Id);
                        ImGui.PopID();
                    }
                }
            }
            finally { ImGui.Destroy(clipper); }
            if (filteredFrames.Length == 0)
                ImGui.TextDisabled(atlas.Frames.Count == 0 ? "This atlas has no frames." : "No matching frames.");
        }
        ImGui.EndChild();
        if (string.IsNullOrEmpty(selectedFrame))
            return;
        if (atlas.Find(selectedFrame) is not { } chosen)
        {
            ImGui.TextWrapped($"Frame {selectedFrame} is missing from this atlas.");
            return;
        }
        ImGui.TextWrapped($"{chosen.Name}   {chosen.Width} x {chosen.Height} px");
        var canPreview = entry.Problem == null &&
            AssetPreview.DrawFrame(session, atlas, chosen, Math.Clamp(ImGui.GetContentRegionAvail().Y - 34f, 32f, 120f));
        var sprite = session.TypeIndex("Sprite");
        var fields = sprite < 0 ? null : authoring.AtlasFields(sprite);
        var targets = selection.Items;
        var canAssign = canPreview && session.Editing && fields is { } sourceFields &&
            authoring.CanAssignSpriteSource(targets, sprite, sourceFields.Atlas, sourceFields.Frame) &&
            targets.All(entity => session.SceneId(entity) != null);
        ImGui.BeginDisabled(!canAssign);
        if (ImGui.Button("Assign frame to selected sprites"))
            authoring.AssignAtlasFrame(targets, path, chosen.Id);
        ImGui.EndDisabled();
        if (fields == null)
            ImGui.TextDisabled("Assignment needs an Engine SDK with atlas support.");
        else if (!canAssign && canPreview)
            ImGui.TextDisabled("Select editable scene sprites in Edit mode.");
    }

    private void Select(string path, bool atlas)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(selected, path) && selectedAtlas == atlas)
            return;
        selected = path;
        selectedAtlas = atlas;
        selectedFrame = null;
        filteredAtlas = null;
        filteredFrames = [];
        frameSearch = "";
    }

    private static string VisibleLabel(string text) => text.Replace("##", "#\u200b#", StringComparison.Ordinal);

    private void FilterPaths()
    {
        var query = search.Trim();
        filtered = paths.Where(path => path.Contains(query, StringComparison.OrdinalIgnoreCase) && (filter switch
        {
            1 => ProjectAssets.IsImage(path),
            2 => ProjectAssets.IsKind(path, "ttf"),
            3 => ProjectAssets.IsAtlas(path),
            _ => true,
        })).ToArray();
    }

    private void StartScan(string project)
    {
        // If another import copies a file during the scan, schedule one more scan after it completes.
        var revision = ProjectAssets.Revision;
        scan = Task.Run(() =>
        {
            var catalog = ProjectAssets.Browse(project);
            var descriptors = new Dictionary<string, AtlasEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in catalog.Paths.Where(ProjectAssets.IsAtlas))
                descriptors[path] = new(ProjectAtlas.Read(project, path, out var problem), problem);
            return new Scan(catalog, descriptors, revision);
        });
    }

    private void StartImport(Func<ProjectAssets.ImportResult> copy)
    {
        importResult = null;
        import = Task.Run(copy);
    }

    private void FinishTasks()
    {
        if (scan is { IsCompleted: true })
        {
            try
            {
                var (catalog, descriptors, revision) = scan.GetAwaiter().GetResult();
                paths = catalog.Paths;
                atlases = descriptors;
                browseProblems = catalog.Problems;
                catalogRevision = revision;
                if (selected != null && !selectedAtlas && !paths.Contains(selected, StringComparer.OrdinalIgnoreCase))
                    selected = null;
                FilterPaths();
            }
            catch (Exception e) when (IsFileProblem(e))
            {
                browseProblems = [e.Message];
                catalogRevision = ProjectAssets.Revision;
            }
            scan = null;
        }

        if (import is { IsCompleted: true })
        {
            try { importResult = import.GetAwaiter().GetResult(); }
            catch (Exception e) when (IsFileProblem(e)) { importResult = new(0, 0, 0, [e.Message]); }
            import = null;
            catalogRevision = -1;
        }
    }

    private static ProjectAssets.ImportResult ImportFile(string project, string file)
    {
        var (_, copiedTo) = ProjectAssets.Import(project, file);
        return copiedTo == null ? new(0, 1, 0, []) : new(1, 0, 0, []);
    }

    private static void DrawProblems(string label, string[] problems)
    {
        if (problems.Length != 0 && ImGui.CollapsingHeader($"{label} ({problems.Length})"))
            foreach (var problem in problems)
                ImGui.TextWrapped(problem);
    }

    private static bool IsFileProblem(Exception e) =>
        e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
