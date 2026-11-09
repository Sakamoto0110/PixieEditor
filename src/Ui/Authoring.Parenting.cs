using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed partial class Authoring
{
    internal bool Reparent(IReadOnlyList<uint> children, uint? parent, bool keepLocal) => Guard(() =>
    {
        if (!Recording || !session.HasParenting)
            throw new EngineException("Hierarchy edits require a version 3 project in Edit mode.");
        return RecordParentingChange(parent == null ? "detach branch" : "reparent branch",
            () =>
            {
                if (!session.SetParents(children, parent, keepLocal)) return false;
                selection.Set(children);
                return true;
            });
    });

    private bool RecordParentingChange(string name, Func<bool> change)
    {
        var before = session.ProjectSnapshot();
        var beforeSelection = selection.Items.Select(session.SceneId).OfType<ulong>().ToArray();
        history.ReserveRecord();
        if (!change()) return true;
        var after = session.ProjectSnapshot();
        if (before != after)
        {
            var afterSelection = selection.Items.Select(session.SceneId).OfType<ulong>().ToArray();
            history.Record(new ParentingStep(name, before, after, beforeSelection, afterSelection));
        }
        return true;
    }

    internal static uint HierarchyRoot(uint entity, IReadOnlyDictionary<uint, uint> parents)
    {
        while (parents.TryGetValue(entity, out var parent)) entity = parent;
        return entity;
    }

    private static uint[] BranchRoots(uint[] chosen, IReadOnlyDictionary<uint, uint> parents)
    {
        var selected = chosen.ToHashSet();
        return chosen.Where(entity =>
        {
            while (parents.TryGetValue(entity, out var parent))
            {
                if (selected.Contains(parent)) return false;
                entity = parent;
            }
            return true;
        }).ToArray();
    }

    internal bool DuplicateBranch(IReadOnlyList<uint> entities)
    {
        if (!Recording || !session.HasParenting || entities.Count == 0) return false;
        var all = session.Entities();
        var children = session.ParentLinks(all).ToLookup(edge => edge.Value, edge => edge.Key);
        var included = entities.ToHashSet();
        var pending = new Queue<uint>(entities);
        while (pending.TryDequeue(out var entity))
            foreach (var child in children[entity])
                if (included.Add(child)) pending.Enqueue(child);
        if (included.Any(entity => session.SceneId(entity) == null))
        {
            Status.Show("A branch copy requires authored entities.", error: true);
            return false;
        }
        return Duplicate(all.Where(included.Contains).ToArray());
    }
}
