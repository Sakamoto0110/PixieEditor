using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed partial class InspectorPanel
{
    private enum SpriteSourceMode { Single, Sheet, Animation }

    private static readonly string[] SpriteModeNames = ["Single sprite", "Spritesheet", "Animation"];
    private SpriteSourceMode? SpriteModeDraft;
    private string? SpriteAtlasDraft;
    private string? SpriteAtlasChoiceProblem;
    private (string Path, ProjectAtlas? Atlas, string? Problem, int Revision, long RefreshAfter)? SpriteAtlasPreview;
    private ProjectAtlas.Reference[] SpriteReferences = [];
    private bool DirectSpriteGeometryVisible = true;

    private void ResetSpriteSourceChoice()
    {
        SpriteModeDraft = null;
        SpriteAtlasDraft = null;
        SpriteAtlasChoiceProblem = null;
        SpriteAtlasPreview = null;
        SpriteReferences = [];
        DirectSpriteGeometryVisible = true;
    }

    private bool DrawSpriteSource(int type, Authoring.SpriteSourceFields fields)
    {
        var references = Entities.Select(entity => new ProjectAtlas.Reference(
            session.GetText(entity, type, fields.Atlas), session.GetText(entity, type, fields.Frame),
            fields.Animation is { } animation ? session.GetText(entity, type, animation) : "")).Distinct().ToArray();
        if (!SpriteReferences.SequenceEqual(references))
        {
            ResetSpriteSourceChoice();
            SpriteReferences = references;
        }

        static SpriteSourceMode Mode(ProjectAtlas.Reference source) => source.Animation.Length != 0
            ? SpriteSourceMode.Animation : source.IsSet ? SpriteSourceMode.Sheet : SpriteSourceMode.Single;

        var modes = references.Select(Mode).Distinct().ToArray();
        SpriteSourceMode? committedMode = modes.Length == 1 ? modes[0] : null;
        var mode = SpriteModeDraft ?? committedMode;
        var canAssign = session.Editing && session.ProjectFolder != null &&
            authoring.CanAssignSpriteSource(Entities, type, fields.All) &&
            Entities.All(entity => session.SceneId(entity) != null);

        ImGui.BeginDisabled(!canAssign);
        ImGui.SetNextItemWidth(-50);
        if (ImGui.BeginCombo("Type", mode is { } shown ? SpriteModeNames[(int)shown] : "Mixed"))
        {
            for (var i = 0; i < SpriteModeNames.Length; i++)
            {
                var choice = (SpriteSourceMode)i;
                ImGui.BeginDisabled(choice == SpriteSourceMode.Animation && fields.Animation == null);
                if (ImGui.Selectable(SpriteModeNames[i], choice == mode))
                {
                    SpriteModeDraft = null;
                    SpriteAtlasDraft = null;
                    SpriteAtlasChoiceProblem = null;
                    if (choice == SpriteSourceMode.Single && committedMode != choice)
                        authoring.UseDirectSpriteImage(Entities, type);
                    else if (choice != committedMode)
                        SpriteModeDraft = choice;
                    mode = choice;
                }
                ImGui.EndDisabled();
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();

        var isDirect = mode == SpriteSourceMode.Single;
        DirectSpriteGeometryVisible = isDirect;
        if (mode is SpriteSourceMode.Sheet or SpriteSourceMode.Animation)
        {
            DrawSpriteAtlasSource(type, canAssign, mode.Value, committedMode, references);
            ImGui.Spacing();
        }
        return isDirect;
    }

    private void DrawSpriteAtlasSource(int type, bool canAssign, SpriteSourceMode mode,
        SpriteSourceMode? committedMode, ProjectAtlas.Reference[] references)
    {
        if (session.ProjectFolder is not { } project) return;
        var paths = references.Select(reference => reference.Atlas).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var path = SpriteAtlasDraft ?? (paths.Length == 1 ? paths[0] : "");
        var reference = references.Length == 1 && committedMode == mode && SpriteAtlasDraft == null
            ? references[0] : new ProjectAtlas.Reference(path, "");
        ImGui.TextWrapped(path.Length != 0 ? $"Atlas: {path}" : "Choose an atlas to set the sprite source.");
        ImGui.BeginDisabled(!canAssign);
        if (ImGui.SmallButton("Choose atlas..."))
        {
            var targets = Entities;
            var revision = session.BindingRevision;
            files.Open("atlas.json", file =>
            {
                if (revision != session.BindingRevision || !ReferenceEquals(targets, Entities)) return;
                try
                {
                    var relative = Path.GetRelativePath(project, file).Replace('\\', '/');
                    _ = ProjectAssets.AtlasAssetFile(project, relative, ".atlas.json");
                    SpriteAtlasDraft = relative;
                    SpriteAtlasChoiceProblem = null;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    SpriteAtlasChoiceProblem = error.Message;
                }
            });
        }
        if (SpriteModeDraft != null || SpriteAtlasDraft != null || SpriteAtlasChoiceProblem != null)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel source change"))
            {
                SpriteModeDraft = null;
                SpriteAtlasDraft = null;
                SpriteAtlasChoiceProblem = null;
            }
        }
        ImGui.EndDisabled();
        if (SpriteAtlasChoiceProblem != null)
            ImGui.TextColored(ProblemColor, SpriteAtlasChoiceProblem);

        if (path.Length == 0) return;
        var (atlas, problem) = ReadSpriteAtlas(project, path);
        if (problem != null) ImGui.TextColored(ProblemColor, problem);
        if (atlas != null)
        {
            if (mode == SpriteSourceMode.Animation)
                DrawSpriteAnimationPicker(type, path, atlas, reference, canAssign && problem == null);
            else
            {
                ImGui.BeginDisabled(!canAssign || problem != null);
                DrawSpriteFramePicker(path, atlas, reference);
                ImGui.EndDisabled();
            }
        }
        if (reference.IsSet && reference.FrameId.Length != 0 && OpenAtlasFrame != null &&
            ImGui.SmallButton("Show in Asset Manager"))
            OpenAtlasFrame(reference);
    }

    private void DrawSpriteFramePicker(string path, ProjectAtlas atlas, ProjectAtlas.Reference reference)
    {
        var selected = atlas.Find(reference.FrameId);
        ImGui.SetNextItemWidth(-62);
        var label = selected?.Name ?? (reference.FrameId.Length > 0 ? "Missing frame" : "Choose a frame");
        if (!ImGui.BeginCombo("Frame", SpriteChoiceLabel(label), ImGuiComboFlags.HeightLarge)) return;
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            ImGui.Begin(clipper, atlas.Frames.Count);
            while (ImGui.Step(clipper))
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var frame = atlas.Frames[i];
                    ImGui.PushID(frame.Id);
                    if (ImGui.Selectable(SpriteChoiceLabel(frame.Name), frame.Id == reference.FrameId) &&
                        authoring.AssignAtlasFrame(Entities, path, frame.Id))
                        ResetSpriteSourceChoice();
                    ImGui.PopID();
                }
            }
        }
        finally { ImGui.Destroy(clipper); }
        ImGui.EndCombo();
    }

    private void DrawSpriteAnimationPicker(int type, string path, ProjectAtlas atlas, ProjectAtlas.Reference reference, bool canAssign)
    {
        var selected = atlas.Animations.FirstOrDefault(clip => clip.Name == reference.Animation);
        var label = selected?.Name ?? (reference.Animation.Length > 0 ? "Missing animation" : "Choose an animation");
        ImGui.BeginDisabled(!canAssign);
        ImGui.SetNextItemWidth(-83);
        if (ImGui.BeginCombo("Clip", SpriteChoiceLabel(label)))
        {
            foreach (var clip in atlas.Animations)
            {
                ImGui.PushID(clip.Name);
                if (ImGui.Selectable(SpriteChoiceLabel(clip.Name), clip == selected) &&
                    authoring.AssignAtlasAnimation(Entities, path, clip.Name))
                    ResetSpriteSourceChoice();
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (atlas.Animations.Count == 0) ImGui.TextDisabled("This atlas has no saved animations.");
        if (selected != null) DrawSpriteFps(type, selected.Fps);
    }

    private void DrawSpriteFps(int type, int clipFps)
    {
        if (session.FindField(type, "animationFps") is not { } field) return;
        var rates = Entities.Select(entity => (int)session.Get(entity, type, field).X).Distinct().ToArray();
        var usesClipFps = rates is [0];
        var value = rates.Length == 1 && rates[0] > 0 ? rates[0] : clipFps;
        ImGui.BeginDisabled(!session.Editing || !authoring.CanAssignSpriteSource(Entities, type, field));
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt(rates.Length == 1 ? "FPS##sprite-fps" : "FPS (mixed)##sprite-fps", ref value))
            Write(Math.Clamp(value, 0, AtlasAnimation.MaxFps));
        ImGui.SameLine();
        if (usesClipFps)
            ImGui.TextDisabled("From clip");
        else if (ImGui.SmallButton($"Use clip FPS ({clipFps})"))
            Write(0);
        ImGui.EndDisabled();

        void Write(int fps)
        {
            foreach (var entity in Entities)
                if (session.Get(entity, type, field).X != fps)
                    recorder.Write(entity, type, field, new PixieValue { X = fps });
        }
    }

    private (ProjectAtlas? Atlas, string? Problem) ReadSpriteAtlas(string project, string path)
    {
        var now = System.Environment.TickCount64;
        var revision = ProjectAssets.Revision;
        if (SpriteAtlasPreview is { } cached && cached.Path == path && cached.Revision == revision && now < cached.RefreshAfter)
            return (cached.Atlas, cached.Problem);
        var atlas = ProjectAtlas.Read(project, path, out var problem);
        SpriteAtlasPreview = (path, atlas, problem, revision, now + 500);
        return (atlas, problem);
    }

    private static string SpriteChoiceLabel(string text) =>
        text.Replace("##", "#\u200b#", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
