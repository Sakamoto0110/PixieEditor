using Hexa.NET.ImGui;
using PixieEditor.Inspection;
using PixieEditor.Native;
using PixieInspector.ImGui;

namespace PixieEditor.Ui;

internal sealed partial class InspectorPanel
{
    // Use descriptor order and group paths for the shared component fields. The same filtered field
    // set feeds PixieInspector nodes and the panel's text, list, and mask controls.
    private void BuildFieldGroups(int type, ComponentHandle[] handles)
    {
        var name = session.TypeName(type);
        var groups = Enumerable.Range(0, session.FieldCount(type))
            .Select(field => (Index: field, Info: session.FieldInfo(type, field)))
            .Where(field => !session.Editing || !field.Info.Flags.HasFlag(PixieFieldFlags.Runtime))
            .OrderBy(field => field.Info.Order)
            .ToLookup(field => PresentationGroup(type, field.Index, field.Info.Flags), field => field.Index);
        var first = true;
        foreach (var path in groups.Select(group => group.Key).DefaultIfEmpty(""))
        {
            var fields = groups[path].ToArray();
            var inspector = InspectorFactory.Build(session, type, handles, () => RebuildRequested = true,
                include: fields.Contains, showRebuild: first);
            first = false;
            var texts = fields.Select(field => (Field: field, Kind: session.FieldInfo(type, field).Kind))
                .Where(f => f.Kind is PixieFieldKind.Text or PixieFieldKind.PointList or PixieFieldKind.RecordList)
                .Select(f => new TextField(f.Field, session.FieldName(type, f.Field), session.FieldText(type, f.Field, PixieFieldText.Label),
                    session.FieldText(type, f.Field, PixieFieldText.Tooltip), session.FieldText(type, f.Field, PixieFieldText.Asset), f.Kind))
                .ToArray();
            var view = inspector.CreateImGuiView();
#if PIXIE_INSPECTOR_FIELD_INTERACTIONS
            ConfigureInteraction(view);
#endif
            Sections.Add(new Section(type, name, inspector, view, texts, fields,
                path.Length == 0 ? [] : path.Split('/')));
        }
    }

    private string PresentationGroup(int type, int field, PixieFieldFlags flags)
    {
        var path = NormalizeGroup(session.FieldGroup(type, field));
        var category = flags.HasFlag(PixieFieldFlags.Runtime) ? "Runtime" : flags.HasFlag(PixieFieldFlags.Derived) ? "Derived" : "";
        return category.Length == 0 ? path : path.Length == 0 ? category : $"{category}/{path}";
    }

    private static string NormalizeGroup(string path)
    {
        var segments = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return segments.Length <= 32 ? string.Join('/', segments) : "";
    }

    private void DrawFieldGroups(IEnumerable<Section> sections, ComponentHandle[] handles, int depth = 0)
    {
        foreach (var branch in sections.GroupBy(section => section.Group is { } path && depth < path.Length ? path[depth] : null))
        {
            if (branch.Key == null)
            {
                foreach (var section in branch) DrawFieldGroup(section, handles);
                continue;
            }
            if (depth == 0) ImGui.Indent(BranchIndent);
            if (ImGui.TreeNodeEx(branch.Key, BranchHeader))
            {
                DrawFieldGroups(branch, handles, depth + 1);
                ImGui.TreePop();
            }
            else
            {
                foreach (var section in branch) FinishHiddenGroup(section);
            }
            if (depth == 0) ImGui.Unindent(BranchIndent);
        }
    }

    // A collapsed group stops receiving input. End its active views and drop pending typing buffers
    // so hidden controls cannot keep the candidate edit gate busy.
    private void FinishHiddenGroup(Section section)
    {
#if PIXIE_INSPECTOR_FIELD_INTERACTIONS
        if (section.View.IsEditing) section.View.EndInteraction();
        foreach (var (address, record) in RecordSections)
            if (record.View.IsEditing && address.Type == section.Type && section.Texts.Any(field => field.Field == address.List))
                record.View.EndInteraction();
#endif
        bool InGroup(int type, int field) => type == section.Type && section.Texts.Any(text => text.Field == field);
        foreach (var field in section.Texts) Typing.Remove((section.Type, field.Field));
        foreach (var key in PointTyping.Keys.Where(k => InGroup(k.Type, k.Field)).ToArray()) PointTyping.Remove(key);
        foreach (var key in RecordText.Keys.Where(k => InGroup(k.Type, k.List)).ToArray()) RecordText.Remove(key);
        foreach (var key in RecordPoints.Keys.Where(k => InGroup(k.Type, k.List)).ToArray()) RecordPoints.Remove(key);
        foreach (var key in LayerTyping.Keys.Where(k => InGroup(k.Type, k.List)).ToArray()) LayerTyping.Remove(key);
    }

    private void DrawFieldGroup(Section section, ComponentHandle[] handles)
    {
        var type = section.Type;
        foreach (var text in section.Texts)
        {
            if (section.Name == "Sprite" && text.Name is "atlas" or "frameId")
                continue;
            if (text.Kind == PixieFieldKind.RecordList)
            {
                if (session.FieldInfo(type, text.Field).Editor == PixieEditorKind.LayerNames)
                    DrawLayerNames(type, text.Field);
                else DrawRecords(type, text);
            }
            else if (text.Kind == PixieFieldKind.PointList)
                DrawPoints(type, text);
            else
                DrawText(type, text);
        }

        DrawLayerMasks(type, handles, section.Fields);
        if (section.Inspector.ShownChildren.Any()) section.View.Draw();
    }
}
