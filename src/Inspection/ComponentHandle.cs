using PixieEditor.Native;

namespace PixieEditor.Inspection;

/// <summary>Locates a record by its owner component type, list field, and zero-based index.</summary>
internal readonly record struct RecordAddress(int OwnerType, int List, int Index);

/// <summary>Binds an Inspector node to a component or record in the native World.</summary>
/// <remarks>
/// Each access uses the native field storage. With a recorder, writes also feed gesture history.
/// The binding expires when the session's binding revision changes. Record addresses are positional
/// and the panel also rebuilds them when the list shape changes.
/// </remarks>
internal sealed class ComponentHandle(EngineSession session, uint entity, int type, Editing.EditRecorder? recorder = null, RecordAddress? record = null)
{
    private readonly long Revision = session.BindingRevision;
    private void RequireCurrent()
    {
        if (Revision != Session.BindingRevision)
            throw new EngineException("The Inspector binding belongs to a replaced World. Select the target again.");
    }
    public EngineSession Session { get; } = session;
    public uint Entity { get; } = entity;
    public int Type { get; } = type;
    internal RecordAddress? Record { get; } = record;

    public PixieValue Get(int field)
    {
        RequireCurrent();
        return Record is { } r ? Session.GetRecord(Entity, r.OwnerType, r.List, r.Index, field) : Session.Get(Entity, Type, field);
    }

    public void Set(int field, PixieValue value)
    {
        RequireCurrent();
        if (Record is { } r)
        {
            if (recorder != null) recorder.WriteRecord(Entity, r, field, value);
            else Session.SetRecord(Entity, r.OwnerType, r.List, r.Index, field, value);
            return;
        }
        if (recorder != null)
            recorder.Write(Entity, Type, field, value);
        else
            Session.Set(Entity, Type, field, value);
    }
}
