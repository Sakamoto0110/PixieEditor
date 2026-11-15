using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed partial class InspectorPanel
{
    private sealed record TriggerCode(string File, uint Line, string Text, bool Available);
    private EngineSession.TriggerActionSource[] TriggerSources = [];
    private long TriggerSourceRevision = -1;
    private TriggerCode[] TriggerCodePreviews = [];
    private string TriggerCodeAction = "";
    private string NewTriggerActionName = "";
    private string? TriggerSourceProblem;
    private string? TriggerCreationProblem;
    internal bool TriggerActionBuildRequested { get; set; }

    private void DrawTriggerActionCode(int type, int list, int item, int field, bool readOnly)
    {
        RefreshTriggerSources();
        var entity = Entities[0];
        var bound = session.GetRecordText(entity, type, list, item, field);
        var label = TriggerSources.FirstOrDefault(source => source.Id == bound)?.Action;
        ImGui.TextUnformatted(label ?? (bound.Length == 0 ? "No action" : bound.StartsWith("action:", StringComparison.Ordinal)
            ? "Unresolved C++ action" : bound));
        ImGui.SameLine();
        if (ImGui.SmallButton("Action code"))
        {
            RefreshTriggerSources(force: true);
            SelectTriggerAction(bound);
            NewTriggerActionName = "";
            TriggerCreationProblem = null;
            ImGui.OpenPopup("trigger-action-code");
        }

        if (!ImGui.IsPopupOpen("trigger-action-code")) return;
        var viewport = ImGui.GetMainViewport();
        var width = Math.Clamp(viewport.WorkSize.X - 24, 1, 670);
        ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2, ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, Math.Max(1, viewport.WorkSize.Y - 24)));
        if (!ImGui.BeginPopup("trigger-action-code")) return;
        if (TriggerSourceProblem is { } problem)
            TriggerCodeMessage(problem);
        else
        {
            var selected = TriggerSources.FirstOrDefault(source => source.Id == TriggerCodeAction);
            if (ImGui.BeginCombo("Action", selected?.Action ?? "Choose a registered action"))
            {
                foreach (var group in TriggerSources.GroupBy(source => source.Id, StringComparer.Ordinal))
                {
                    ImGui.PushID(group.Key);
                    if (ImGui.Selectable(group.First().Action, group.Key == TriggerCodeAction))
                        SelectTriggerAction(group.Key);
                    ImGui.PopID();
                }
                ImGui.EndCombo();
            }

            var canEdit = !readOnly && session.Editing && !session.FieldLocked(entity, type, list);
            ImGui.BeginDisabled(!canEdit);
            if (TriggerCodePreviews.Length > 0 && ImGui.Button("Use action"))
            {
                BindTriggerAction(entity, type, list, item, field, TriggerCodeAction);
                ImGui.CloseCurrentPopup();
            }
            if (bound.Length > 0)
            {
                if (TriggerCodePreviews.Length > 0) ImGui.SameLine();
                if (ImGui.Button("Clear action"))
                {
                    BindTriggerAction(entity, type, list, item, field, "");
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndDisabled();

            if (session.ProjectFolder is { } project)
            {
                ImGui.BeginDisabled(!canEdit || CodeBuildPending());
                ImGui.InputTextWithHint("##new-action-name", "Function name (optional)", ref NewTriggerActionName, 65);
                ImGui.SameLine();
                ImGui.BeginDisabled(NewTriggerActionName.Length > 0 && !ProjectCode.ValidName(NewTriggerActionName));
                if (ImGui.Button("Add C++ action"))
                {
                    try
                    {
                        var action = ProjectCode.CreateTriggerAction(project, NewTriggerActionName, GameLibrary.Selection);
                        BindTriggerAction(entity, type, list, item, field, action.Id);
                        if (session.GetRecordText(entity, type, list, item, field) != action.Id)
                            throw new EngineException($"Created {action.Source}. Build and reopen, then choose it from the action list.");
                        TriggerActionBuildRequested = true;
                        ImGui.CloseCurrentPopup();
                    }
                    catch (Exception error) when (error is EngineException or IOException or UnauthorizedAccessException
                        or ArgumentException or System.Text.Json.JsonException)
                    {
                        TriggerCreationProblem = error.Message;
                    }
                }
                ImGui.EndDisabled();
                ImGui.EndDisabled();
            }
            if (TriggerCreationProblem is { } generationError) TriggerCodeMessage(generationError);
            if (TriggerCodePreviews.Length == 0 && TriggerCodeAction.Length != 0)
                TriggerCodeMessage("The assigned action is missing from the compiled project. Build and reopen or choose an available action.");

            foreach (var (preview, index) in TriggerCodePreviews.Select((value, index) => (value, index)))
            {
                ImGui.PushID(index);
                if (preview.Available)
                {
                    ImGui.TextUnformatted($"{preview.File}:{preview.Line}");
                    if (ImGui.Button("Open in VS Code") && session.ProjectFolder is { } folder)
                    {
                        try { if (CodeEditor.Open(folder, preview.File, preview.Line) is { } notice) Status.Show(notice, error: true); }
                        catch (EngineException error) { Status.Show(error.Message, error: true); }
                    }
                    if (ImGui.BeginChild("##code", new Vector2(0, 230), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
                        ImGui.TextUnformatted(preview.Text);
                    ImGui.EndChild();
                }
                else TriggerCodeMessage(preview.Text);
                ImGui.PopID();
            }
        }
        ImGui.EndPopup();
    }

    private void BindTriggerAction(uint entity, int type, int list, int item, int field, string id)
    {
        recorder.Close();
        authoring.SetRecordText(entity, type, list, item, field, id);
    }

    private void RefreshTriggerSources(bool force = false)
    {
        if (!force && TriggerSourceRevision == session.BindingRevision) return;
        TriggerSourceRevision = session.BindingRevision;
        TriggerSourceProblem = null;
        try { TriggerSources = session.ReadTriggerActionSources(); }
        catch (Exception error) when (error is EngineException or System.Text.Json.JsonException)
        {
            TriggerSources = [];
            TriggerSourceProblem = error.Message;
        }
    }

    private void SelectTriggerAction(string id)
    {
        TriggerCodeAction = id;
        TriggerCodePreviews = TriggerSources.Where(source => source.Id == id).Select(ReadTriggerCode).ToArray();
    }

    private TriggerCode ReadTriggerCode(EngineSession.TriggerActionSource source)
    {
        try
        {
            if (session.ProjectFolder is not { } folder || source.Line == 0 || source.Line > int.MaxValue - 22)
                throw new EngineException("The action has no viewable project source location.");
            var file = Path.IsPathFullyQualified(source.File) ? Path.GetRelativePath(folder, source.File) : source.File;
            var path = ProjectCode.ResolveSource(folder, file);
            var first = Math.Max(1, (int)source.Line - 2);
            using var stream = File.OpenRead(path);
            if (stream.Length > 2 * 1024 * 1024) throw new EngineException("The action source exceeds the 2 MiB preview limit.");
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            for (var number = 1; number < first + 22 && reader.ReadLine() is { } line; ++number)
                if (number >= first) lines.Add($"{(number == source.Line ? '>' : ' ')} {number,4}  {line}");
            if (lines.Count <= source.Line - first) throw new EngineException("The registered line is outside the current source. Rebuild the project to refresh it.");
            return new(file, source.Line, string.Join('\n', lines), true);
        }
        catch (Exception error) when (error is EngineException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new("", 0, error.Message, false);
        }
    }

    private static void TriggerCodeMessage(string text)
    {
        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }
}
