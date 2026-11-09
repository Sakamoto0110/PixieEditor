using Hexa.NET.ImGui;

namespace PixieEditor.Ui;

internal sealed partial class InspectorPanel
{
    private HashSet<uint> OpenSelectionPath = [];
    // A null value remembers the open details without retaining an unused binding.
    private readonly Dictionary<uint, InspectorPanel?> OpenBranches = [];
    private Action? ParentingCommand;

    private void DrawParenting(Selection selection, bool selectionChanged)
    {
        var all = session.Entities();
        var links = session.ParentLinks(all);
        var children = links.ToLookup(edge => edge.Value, edge => edge.Key);
        var roots = Entities.Select(entity => Authoring.HierarchyRoot(entity, links)).Distinct();
        var selectionPath = new HashSet<uint>(Entities);
        foreach (var entity in Entities)
        {
            var ancestor = entity;
            while (links.TryGetValue(ancestor, out ancestor)) selectionPath.Add(ancestor);
        }
        var openPath = selectionChanged || !OpenSelectionPath.SetEquals(selectionPath);
        OpenSelectionPath = selectionPath;

        uint? inspected = Entities.Length == 1 ? Entities[0] : null;
        var detailsDrawn = false;
        var visibleBindings = new HashSet<uint>();
        var pending = new Stack<(uint Entity, int Depth)>();
        foreach (var root in roots.Reverse()) pending.Push((root, 0));
        while (pending.TryPop(out var row))
        {
            var (entity, depth) = row;
            var hasChildren = children.Contains(entity);
            var selected = selection.Contains(entity);
            var indent = BranchIndent * Math.Min(depth, 12);
            ImGui.PushID(unchecked((int)entity));
            if (indent > 0) ImGui.Indent(indent);
            if (openPath && selectionPath.Contains(entity))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
            var flags = BranchHeader | ImGuiTreeNodeFlags.NoTreePushOnOpen |
                ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
            if (depth == 0) flags |= ImGuiTreeNodeFlags.DefaultOpen;
            var expanded = ImGui.TreeNodeEx($"{authoring.Describe(entity)}###entity", flags);
            var toggled = ImGui.IsItemToggledOpen();
            if (selected)
                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.HeaderHovered));
            if (ImGui.IsItemClicked() && !toggled)
            {
                if (ImGui.GetIO().KeyCtrl) selection.Toggle(entity);
                else selection.Set(entity);
            }

            if (ImGui.BeginPopupContextItem("entity-actions"))
            {
                DrawParentingMenu(selection.Contains(entity) ? [.. selection.Items] : [entity], all);
                ImGui.EndPopup();
            }
            if (!expanded && toggled && OpenBranches.Remove(entity, out var closed))
                ReleaseBranchBinding(closed);
            if (expanded && inspected != null && (inspected == entity || toggled || OpenBranches.ContainsKey(entity)))
            {
                ImGui.Indent(BranchIndent);
                if (inspected == entity)
                {
                    OpenBranches.TryAdd(entity, null);
                    DrawEntityDetails();
                    detailsDrawn = true;
                }
                else
                {
                    DrawOpenBranch(entity);
                    visibleBindings.Add(entity);
                }
                ImGui.Unindent(BranchIndent);
            }
            if (expanded && hasChildren)
                foreach (var child in children[entity].Reverse()) pending.Push((child, depth + 1));

            if (indent > 0) ImGui.Unindent(indent);
            ImGui.PopID();
        }
        if (inspected != null && !detailsDrawn) FinishEntityDetails();
        ReleaseHiddenBranchBindings(visibleBindings);
    }

    private void DrawOpenBranch(uint entity)
    {
        if (!OpenBranches.TryGetValue(entity, out var binding) || binding == null)
            OpenBranches[entity] = binding = new InspectorPanel(session, authoring, recorder, files);
        var types = Enumerable.Range(0, session.TypeCount).Where(type => session.Has(entity, type)).ToArray();
        if (binding.BoundRevision != session.BindingRevision || !types.SequenceEqual(binding.Types))
            binding.Rebuild([entity], types);
        binding.DrawEntityDetails();
        RebuildRequested |= binding.RebuildRequested;
        binding.RebuildRequested = false;
    }

    private void ReleaseHiddenBranchBindings(IReadOnlySet<uint>? visible = null)
    {
        foreach (var (entity, binding) in OpenBranches.ToArray())
        {
            if (visible?.Contains(entity) == true) continue;
            if (binding != null)
            {
                ReleaseBranchBinding(binding);
                OpenBranches[entity] = null;
            }
            if (!session.IsAlive(entity)) OpenBranches.Remove(entity);
        }
    }

    private void ReleaseBranchBinding(InspectorPanel? binding)
    {
        if (binding == null) return;
        if (binding.BoundRevision == session.BindingRevision) binding.FinishEntityDetails();
        binding.Dispose();
    }

    private void FinishEntityDetails()
    {
        if (!HasLocalAuthoringGesture) return;
        NameActive = false;
        foreach (var section in Sections) FinishHiddenGroup(section);
    }

    private void DrawParentingMenu(uint[] chosen, uint[] all)
    {
        var transform = session.TypeIndex("Transform2D");
        var canEdit = authoring.Recording && transform >= 0 &&
            chosen.All(entity => session.SceneId(entity) != null && session.Has(entity, transform));
        if (ImGui.BeginMenu("Set parent", canEdit))
        {
            ImGui.TextDisabled("Keeps the current world position, rotation and scale.");
            ImGui.Separator();
            if (ImGui.MenuItem("None (make root)")) QueueParent(null);
            foreach (var parent in all)
            {
                if (chosen.Contains(parent) || session.SceneId(parent) == null || !session.Has(parent, transform)) continue;
                if (ImGui.MenuItem($"{authoring.Describe(parent)}##parent-{parent}")) QueueParent(parent);
            }
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem("Duplicate branch", "", false, canEdit))
            ParentingCommand = () =>
            {
                recorder.Close();
                authoring.DuplicateBranch(chosen);
            };

        void QueueParent(uint? parent)
        {
            ParentingCommand = () =>
            {
                recorder.Close();
                authoring.Reparent(chosen, parent, keepLocal: false);
            };
        }
    }
}
