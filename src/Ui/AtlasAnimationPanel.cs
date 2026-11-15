using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed class AtlasAnimationPanel
{
    private int selected;
    private int selectedStep;
    private string prefix = "";
    private string frameSearch = "";
    private string? problem;
    private double? startedAt;
    private int playbackStartStep;
    private int zoom;

    internal void Open(SpritesheetDraft draft, string? frameId)
    {
        selected = selectedStep = 0;
        frameSearch = "";
        problem = null;
        startedAt = null;
        zoom = 0;
        var frame = draft.Entries.FirstOrDefault(entry => entry.Frame.Id == frameId)?.Frame;
        prefix = frame == null ? "" : AtlasAnimation.NumberedPrefix(frame.Name);
    }

    internal void Pause() => startedAt = null;

    internal void DrawControls(SpritesheetDraft draft)
    {
        var animations = draft.Animations;
        ImGui.BeginDisabled(draft.Entries.Count == 0);
        if (ImGui.Button("New animation"))
        {
            var name = Enumerable.Range(1, animations.Count + 1).Select(n => $"Animation {n}")
                .First(candidate => animations.All(animation => !candidate.Equals(animation.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
            animations.Add(new(name, AtlasAnimation.DefaultFps, []));
            Select(animations.Count - 1);
        }
        ImGui.EndDisabled();
        if (animations.Count == 0)
        {
            ImGui.TextWrapped("Create an animation, then add individual frames or load a numbered sequence.");
            return;
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##animations", animations[selected].Name))
        {
            for (var index = 0; index < animations.Count; ++index)
            {
                ImGui.PushID(index);
                if (ImGui.Selectable(Label(animations[index].Name), index == selected))
                    Select(index);
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }

        var animation = animations[selected];
        var nameText = animation.Name;
        var fps = animation.Fps;
        ImGui.SetNextItemWidth(-65);
        if (ImGui.InputText("Name", ref nameText, 256)) animation = animation with { Name = nameText };
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("FPS", ref fps))
        {
            animation = animation with { Fps = fps };
            Pause();
        }

        ImGui.SeparatorText("Frame order");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##numbered-prefix", "Numbered frame prefix", ref prefix, 256);
        if (ImGui.Button("Load numbered frames"))
        {
            var ids = AtlasAnimation.NumberedFrames(draft.Entries.Select(entry => entry.Frame), prefix);
            if (ids.Length == 0) problem = "No numbered frames match that prefix.";
            else
            {
                animation = animation with { FrameIds = ids };
                selectedStep = 0;
                problem = null;
                Pause();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Add frame..."))
        {
            frameSearch = "";
            ImGui.OpenPopup("Choose animation frame");
        }
        if (ImGui.BeginPopup("Choose animation frame"))
        {
            ImGui.InputTextWithHint("##animation-frame-search", "Find frame", ref frameSearch, 256);
            if (ImGui.BeginChild("##available-frames", new Vector2(280, 180)))
            {
                foreach (var entry in draft.Entries)
                {
                    var frame = entry.Frame;
                    if (!frame.Name.Contains(frameSearch, StringComparison.OrdinalIgnoreCase)) continue;
                    ImGui.PushID(frame.Id);
                    if (ImGui.Selectable(Label(frame.Name)))
                    {
                        animation = animation with { FrameIds = [.. animation.FrameIds, frame.Id] };
                        selectedStep = animation.FrameIds.Count - 1;
                        Pause();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
        if (problem != null) ImGui.TextWrapped(problem);

        if (ImGui.BeginChild("##animation-frames", new Vector2(0, 150), ImGuiChildFlags.Borders,
                ImGuiWindowFlags.HorizontalScrollbar))
        {
            var frames = draft.Entries.ToDictionary(entry => entry.Frame.Id);
            for (var index = 0; index < animation.FrameIds.Count; ++index)
            {
                var id = animation.FrameIds[index];
                ImGui.PushID(index);
                if (ImGui.Selectable(Label(frames.TryGetValue(id, out var entry) ? entry.Frame.Name : $"Missing frame {id}"),
                        index == selectedStep))
                {
                    selectedStep = index;
                    Pause();
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        var count = animation.FrameIds.Count;
        ImGui.BeginDisabled(selectedStep == 0);
        if (ImGui.Button("Move up")) animation = Move(animation, -1);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(selectedStep + 1 >= count);
        if (ImGui.Button("Move down")) animation = Move(animation, 1);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(count == 0);
        if (ImGui.Button("Remove step"))
        {
            var ids = animation.FrameIds.ToList();
            ids.RemoveAt(selectedStep);
            animation = animation with { FrameIds = ids };
            selectedStep = Math.Clamp(selectedStep, 0, Math.Max(0, ids.Count - 1));
            Pause();
        }
        ImGui.EndDisabled();

        ImGui.Spacing();
        if (ImGui.Button("Remove animation"))
        {
            animations.RemoveAt(selected);
            Select(Math.Max(0, selected - 1));
            return;
        }
        animations[selected] = animation;
    }

    internal void DrawPreview(EngineSession session, SpritesheetDraft draft)
    {
        if (draft.Animations.Count == 0) return;
        var animation = draft.Animations[selected];
        ImGui.BeginDisabled(animation.FrameIds.Count == 0 || animation.Fps is < 1 or > AtlasAnimation.MaxFps);
        if (ImGui.Button(startedAt.HasValue ? "Pause##animation" : "Play##animation"))
        {
            if (startedAt.HasValue) Pause();
            else
            {
                playbackStartStep = selectedStep;
                startedAt = ImGui.GetTime();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Restart"))
        {
            selectedStep = 0;
            playbackStartStep = 0;
            if (startedAt.HasValue) startedAt = ImGui.GetTime();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextDisabled("Loop");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.Combo("##animation-zoom", ref zoom, ZoomLabels, ZoomLabels.Length);

        if (animation.FrameIds.Count == 0) return;
        if (startedAt is { } start)
            selectedStep = (playbackStartStep + animation.FrameIndex(ImGui.GetTime() - start)) % animation.FrameIds.Count;
        var id = animation.FrameIds[selectedStep];
        var entry = draft.Entries.FirstOrDefault(candidate => candidate.Frame.Id == id);
        if (entry == null)
        {
            ImGui.TextWrapped($"Missing frame {id}");
            return;
        }

        ImGui.TextUnformatted($"{selectedStep + 1} / {animation.FrameIds.Count}   {entry.Frame.Name}");
        var path = entry.Source ?? draft.Sheet;
        if (path == null) return;
        var image = session.Image(path, out var error);
        if (image == null)
        {
            ImGui.TextWrapped(error ?? $"Could not preview {path}.");
            return;
        }
        var frame = entry.Source == null ? entry.Frame :
            entry.Frame with { X = 0, Y = 0, Width = image.Value.Width, Height = image.Value.Height };
        AssetPreview.DrawAnimationFrame(image.Value, frame, PreviewScales[zoom]);
    }

    private static readonly string[] ZoomLabels = ["4×", "2×", "1×", "8×"];
    private static readonly int[] PreviewScales = [4, 2, 1, 8];

    private AtlasAnimation Move(AtlasAnimation animation, int offset)
    {
        var ids = animation.FrameIds.ToArray();
        var next = selectedStep + offset;
        (ids[selectedStep], ids[next]) = (ids[next], ids[selectedStep]);
        selectedStep = next;
        Pause();
        return animation with { FrameIds = ids };
    }

    private void Select(int index)
    {
        selected = index;
        selectedStep = 0;
        problem = null;
        Pause();
    }

    private static string Label(string name) =>
        name.Replace("##", "#\u200b#", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ') + "##row";
}
