using System.Numerics;
using System.Text.Json;
using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;
using static PixieEditor.Editing.SpritesheetPacking;

namespace PixieEditor.Ui;

internal sealed class SpritesheetBuilderPanel(EngineSession session) : IDisposable
{
    private const string Title = "Spritesheet Builder";
    private SpritesheetDraft? draft;
    private string? selectedFrame;
    private string search = "";
    private string? problem;
    private uint previewTexture;
    private (int Width, int Height, IReadOnlyList<ProjectAtlas.Frame> Frames)? preview;
    private Vector2? dragFrom;
    private Rect grid = new(0, 0, 32, 32);
    private int gapX;
    private int gapY;
    private bool requestOpen;
    private Task<Packed>? packing;
    private Task<ProjectAtlas>? saving;
    private readonly AtlasAnimationPanel animations = new();
    private bool showAnimations;

    internal void Open(SpritesheetDraft next, string? frame = null, bool editAnimations = false)
    {
        draft = next;
        selectedFrame = frame;
        search = "";
        problem = null;
        dragFrom = null;
        grid = new(0, 0, 32, 32);
        gapX = gapY = 0;
        showAnimations = editAnimations;
        animations.Open(next, frame);
        ClearPreview();
        requestOpen = true;
    }

    internal ProjectAtlas.Reference? Draw(string project, IReadOnlyList<string> paths)
    {
        if (draft is not { } current) return null;
        if (requestOpen)
        {
            ImGui.OpenPopup(Title);
            requestOpen = false;
        }
        ImGui.SetNextWindowSize(new Vector2(940, 695), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal(Title, ImGuiWindowFlags.NoSavedSettings))
            return null;

        ProjectAtlas.Reference? saved = null;
        if (saving is { IsCompleted: true })
        {
            Try(() =>
            {
                var atlas = saving.GetAwaiter().GetResult();
                saved = new(current.Path, selectedFrame ?? atlas.Frames.FirstOrDefault()?.Id ?? "");
            });
            saving = null;
        }
        if (packing is { IsCompleted: true })
        {
            Try(() =>
            {
                var packed = packing.GetAwaiter().GetResult();
                ClearPreview();
                previewTexture = Gl.UploadPreview(packed.Image.Width, packed.Image.Height, packed.Image.Pixels);
                preview = (packed.Image.Width, packed.Image.Height, packed.Frames);
            });
            packing = null;
        }

        var busy = packing != null || saving != null;
        if (saving == null && !saved.HasValue)
        {
            ImGui.BeginDisabled(busy);
            ImGui.BeginDisabled(current.Existing);
            var path = current.Path;
            if (ImGui.InputText("Atlas descriptor", ref path, 1024)) current.Path = path;
            ImGui.EndDisabled();
            if (ImGui.RadioButton("Frames", !showAnimations))
            {
                showAnimations = false;
                animations.Pause();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Animations", showAnimations))
                showAnimations = true;

            var workspace = ImGui.GetContentRegionAvail();
            var footerHeight = ImGui.GetFrameHeightWithSpacing() * (problem == null ? 2 : 4);
            var contentHeight = Math.Max(80, workspace.Y - footerHeight);
            if (ImGui.BeginChild("##builder-controls", new Vector2(Math.Clamp(workspace.X * 0.39f, 210, 360), contentHeight),
                    ImGuiChildFlags.Borders))
            {
                if (showAnimations) animations.DrawControls(current);
                else
                {
                    DrawSources(project, current, paths);
                    DrawFrames(current);
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("##builder-preview", new Vector2(0, contentHeight), ImGuiChildFlags.Borders))
            {
                if (showAnimations) animations.DrawPreview(session, current);
                else DrawPreview(current);
            }
            ImGui.EndChild();
            ImGui.EndDisabled();
        }

        var canSave = !busy && !saved.HasValue && session.Editing && !session.Edits.IsBlocked &&
            (current.PacksImages ? current.Existing || current.Entries.Count > 0 : current.Sheet != null);
        ImGui.BeginDisabled(!canSave);
        if (ImGui.Button("Save atlas"))
            saving = Task.Run(() => current.Save(project));
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(busy);
        var cancelled = ImGui.Button("Cancel");
        ImGui.EndDisabled();
        if (saving != null) ImGui.TextDisabled("Saving atlas...");
        else if (packing != null) ImGui.TextDisabled("Packing preview...");
        else if (!session.Editing) ImGui.TextDisabled("Stop Play before saving an atlas.");
        else if (problem != null) ImGui.TextWrapped(problem);
        if (cancelled || saved.HasValue)
        {
            ImGui.CloseCurrentPopup();
            draft = null;
            // The current ImGui frame may still reference this texture.
        }
        ImGui.EndPopup();
        return saved;
    }

    internal void BeginFrame()
    {
        if (preview == null && previewTexture != 0) ClearPreview();
    }

    private void DrawSources(string project, SpritesheetDraft current, IReadOnlyList<string> paths)
    {
        var packs = current.PacksImages;
        if (packs || current.Entries.Count == 0)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##builder-source-search", packs ? "Find PNGs to add" : "Find a PNG sheet", ref search, 256);
            if (ImGui.BeginChild("##builder-sources", new Vector2(0, 100), ImGuiChildFlags.Borders,
                    ImGuiWindowFlags.HorizontalScrollbar))
            {
                var added = packs ? current.Entries.Select(entry => entry.Source)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
                foreach (var path in paths)
                {
                    if (!ProjectAssets.IsImage(path) || !path.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (packs && StringComparer.OrdinalIgnoreCase.Equals(path, current.Sheet)))
                        continue;
                    ImGui.PushID(path);
                    var used = added?.Contains(path) ?? StringComparer.OrdinalIgnoreCase.Equals(path, current.Sheet);
                    ImGui.BeginDisabled(used);
                    if (ImGui.Selectable(VisibleLabel(path), used))
                        Try(() =>
                        {
                            _ = ProjectAssets.AtlasAssetFile(project, path, ".png");
                            if (packs)
                            {
                                current.AddSource(path);
                                selectedFrame = current.Entries[^1].Frame.Id;
                                InvalidatePacking();
                            }
                            else
                            {
                                current.Sheet = path;
                                dragFrom = null;
                            }
                        });
                    ImGui.EndDisabled();
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();
        }

        if (packs)
        {
            var padding = current.Padding;
            var extrusion = current.Extrusion;
            ImGui.SetNextItemWidth(90);
            var changed = ImGui.InputInt("Padding (px)", ref padding);
            ImGui.SetNextItemWidth(90);
            changed |= ImGui.InputInt("Extrusion (px)", ref extrusion);
            if (changed)
            {
                current.Padding = padding;
                current.Extrusion = extrusion;
                InvalidatePacking();
            }
            ImGui.BeginDisabled(current.Entries.Count == 0);
            if (ImGui.Button("Preview packing"))
                packing = Task.Run(() => current.PreviewPacking(project));
            ImGui.EndDisabled();
        }
        else if (current.Sheet is { } sheet)
        {
            ImGui.TextWrapped(sheet);
            grid = DrawRectInputs(grid, "Grid", ["Offset X", "Offset Y", "Cell width", "Cell height"]);
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("Gap X", ref gapX);
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("Gap Y", ref gapY);
            if (ImGui.Button("Add grid slices"))
                Try(() =>
                {
                    var image = session.Image(sheet, out var error) ?? throw new IOException(error ?? "Could not read the sheet.");
                    AddSlices(current, Grid(image.Width, image.Height, grid.Width, grid.Height, grid.X, grid.Y, gapX, gapY));
                });
        }
    }

    private void DrawFrames(SpritesheetDraft current)
    {
        ImGui.SeparatorText($"Frames ({current.Entries.Count})");
        if (ImGui.BeginChild("##builder-frames", new Vector2(0, 115), ImGuiChildFlags.Borders,
                ImGuiWindowFlags.HorizontalScrollbar))
        {
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                ImGui.Begin(clipper, current.Entries.Count);
                while (ImGui.Step(clipper))
                    for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; ++index)
                    {
                        var frame = current.Entries[index].Frame;
                        ImGui.PushID(frame.Id);
                        if (ImGui.Selectable(VisibleLabel(frame.Name) + "##frame", frame.Id == selectedFrame))
                            selectedFrame = frame.Id;
                        ImGui.PopID();
                    }
            }
            finally { ImGui.Destroy(clipper); }
        }
        ImGui.EndChild();

        var selected = current.Entries.FindIndex(entry => entry.Frame.Id == selectedFrame);
        if (selected < 0) return;
        var (original, source) = current.Entries[selected];
        ImGui.PushID(original.Id);
        var name = original.Name;
        var pivotX = original.PivotX;
        var pivotY = original.PivotY;
        ImGui.SetNextItemWidth(Math.Max(90, ImGui.GetContentRegionAvail().X - 55));
        ImGui.InputText("Name", ref name, 1024);
        ImGui.SetNextItemWidth(90);
        ImGui.InputDouble("Pivot X", ref pivotX, 0, 0, "%.3f");
        ImGui.SetNextItemWidth(90);
        ImGui.InputDouble("Pivot Y", ref pivotY, 0, 0, "%.3f");
        var rect = Rect.Of(original);
        if (!current.PacksImages)
            rect = DrawRectInputs(rect, "Frame", ["X", "Y", "Width", "Height"]);
        else if (source != null)
            ImGui.TextWrapped(source);
        var updated = original with
        {
            Name = name, PivotX = pivotX, PivotY = pivotY,
            X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height,
        };
        if (updated != original) current.Entries[selected] = new(updated, source);
        if (ImGui.Button("Remove frame"))
        {
            current.Entries.RemoveAt(selected);
            selectedFrame = current.Entries.Count > 0 ? current.Entries[Math.Min(selected, current.Entries.Count - 1)].Frame.Id : null;
            InvalidatePacking();
        }
        ImGui.PopID();
    }

    private static Rect DrawRectInputs(Rect rect, string group, string[] labels)
    {
        ImGui.PushID(group);
        var (x, y, width, height) = rect;
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt(labels[0], ref x);
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt(labels[1], ref y);
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt(labels[2], ref width);
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt(labels[3], ref height);
        ImGui.PopID();
        return new(x, y, width, height);
    }

    private unsafe void DrawPreview(SpritesheetDraft current)
    {
        if (current.PacksImages)
        {
            if (preview is { } packed)
            {
                var selected = current.Entries.FirstOrDefault(entry => entry.Frame.Id == selectedFrame)?.Frame;
                DrawCanvas(previewTexture, packed.Width, packed.Height, packed.Frames, selected, null);
            }
            else if (current.Entries.FirstOrDefault(entry => entry.Frame.Id == selectedFrame)?.Source is { } source)
                AssetPreview.DrawImage(session, source, Math.Min(270, Math.Max(1, ImGui.GetContentRegionAvail().X - 120)));
            return;
        }

        if (current.Sheet is not { } sheet) return;
        if (session.Image(sheet, out var error) is not { } image)
        {
            ImGui.TextWrapped(error ?? "Could not read the sheet.");
            return;
        }
        ImGui.TextDisabled("Drag to add a frame. Click a frame to select it.");
        var frames = current.Entries.Select(entry => entry.Frame);
        var selectedFrameData = current.Entries.FirstOrDefault(entry => entry.Frame.Id == selectedFrame)?.Frame;
        DrawCanvas(image.Texture, image.Width, image.Height, frames, selectedFrameData, current);
    }

    private unsafe void DrawCanvas(uint texture, int width, int height, IEnumerable<ProjectAtlas.Frame> frames,
        ProjectAtlas.Frame? selected, SpritesheetDraft? slice)
    {
        var available = ImGui.GetContentRegionAvail();
        var scale = Math.Max(0.001f, Math.Min(available.X / width, (available.Y - ImGui.GetTextLineHeightWithSpacing()) / height));
        var origin = ImGui.GetCursorScreenPos();
        var pixels = new Vector2(width, height);
        var size = pixels * scale;
        var draw = ImGui.GetWindowDrawList();
        // Give the canvas its own input item so dragging the sheet cannot move the popup.
        ImGui.InvisibleButton("##sheet-canvas", size);
        draw.AddRectFilled(origin, origin + size, ImGui.GetColorU32(ImGuiCol.FrameBg));
        draw.AddImage(new ImTextureRef(null, new ImTextureID(texture)), origin, origin + size);
        var mouse = Vector2.Clamp((ImGui.GetMousePos() - origin) / scale, Vector2.Zero, pixels);
        var hovered = ImGui.IsItemHovered();
        ProjectAtlas.Frame? hit = null;
        var ordinary = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.85f, 1f, 0.8f));
        var highlight = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.78f, 0.3f, 1f));
        foreach (var frame in frames)
        {
            if (!Rect.Of(frame).Fits(width, height)) continue;
            var start = new Vector2(frame.X, frame.Y);
            var end = start + new Vector2(frame.Width, frame.Height);
            if (mouse.X >= start.X && mouse.Y >= start.Y && mouse.X < end.X && mouse.Y < end.Y)
                hit = frame;
            if (frame.Id != selected?.Id)
                draw.AddRect(origin + start * scale, origin + end * scale, ordinary);
        }
        if (selected != null)
        {
            var selectedRect = slice == null ? frames.FirstOrDefault(frame => frame.Id == selected.Id) : selected;
            if (selectedRect != null && Rect.Of(selectedRect).Fits(width, height))
            {
                var start = new Vector2(selectedRect.X, selectedRect.Y);
                var extent = new Vector2(selectedRect.Width, selectedRect.Height);
                draw.AddRect(origin + start * scale, origin + (start + extent) * scale, highlight);
                if (selected.PivotX is >= 0 and <= 1 && selected.PivotY is >= 0 and <= 1)
                {
                    var pivot = origin + (start + extent * new Vector2((float)selected.PivotX, (float)selected.PivotY)) * scale;
                    draw.AddCircleFilled(pivot, 4, highlight);
                }
            }
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (slice != null) dragFrom = mouse;
            else if (hit != null) selectedFrame = hit.Id;
        }
        if (slice != null && dragFrom is { } from)
        {
            var start = Vector2.Min(from, mouse);
            var end = Vector2.Max(from, mouse);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left, 3))
                draw.AddRect(origin + start * scale, origin + end * scale, highlight);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                if (Vector2.Distance(from, mouse) * scale >= 3)
                {
                    var x = (int)MathF.Floor(start.X);
                    var y = (int)MathF.Floor(start.Y);
                    var rect = new Rect(x, y, (int)MathF.Ceiling(end.X) - x, (int)MathF.Ceiling(end.Y) - y);
                    if (rect.Fits(width, height)) Try(() => AddSlices(slice, [rect]));
                }
                else if (hit != null) selectedFrame = hit.Id;
                dragFrom = null;
            }
        }
        ImGui.TextDisabled($"{width} x {height} px");
    }

    private void AddSlices(SpritesheetDraft current, Rect[] slices)
    {
        var before = current.Entries.Count;
        current.AddSlices(slices);
        if (current.Entries.Count > before) selectedFrame = current.Entries[before].Frame.Id;
    }

    private void Try(Action action)
    {
        try { action(); problem = null; }
        catch (Exception error) when (IsFileProblem(error)) { problem = error.Message; }
    }

    internal static bool IsFileProblem(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException;

    private static string VisibleLabel(string text) =>
        text.Replace("##", "#\u200b#", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');

    // Input changes invalidate the geometry. The old texture may be on this frame's draw list.
    private void InvalidatePacking() => preview = null;

    private void ClearPreview()
    {
        preview = null;
        if (previewTexture == 0) return;
        Gl.DeletePreview(previewTexture);
        previewTexture = 0;
    }

    public void Dispose()
    {
        // Publication belongs to this project even if the Editor closes mid-write.
        if (saving != null)
        {
            try { saving.GetAwaiter().GetResult(); }
            catch (Exception error) when (IsFileProblem(error))
            {
                Console.Error.WriteLine($"Could not save atlas {draft?.Path}: {error.Message}");
            }
        }
        ClearPreview();
    }
}
