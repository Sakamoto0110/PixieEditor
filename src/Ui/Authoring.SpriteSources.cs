using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed partial class Authoring
{
    internal (int Atlas, int Frame)? AtlasFields(int type) =>
        session.FindField(type, "atlas") is { } atlas && session.FindField(type, "frameId") is { } frame
            ? (atlas, frame) : null;

    internal bool CanAssignSpriteSource(IReadOnlyList<uint> entities, int type, params int[] fields) =>
        !session.Edits.IsBlocked && entities.Count != 0 &&
        entities.All(entity => session.IsAlive(entity) && session.Has(entity, type)) &&
        fields.All(field => !session.FieldInfo(type, field).Flags.HasFlag(PixieFieldFlags.ReadOnly) &&
                            entities.All(entity => !session.FieldLocked(entity, type, field)));

    internal bool AssignAtlasFrame(IReadOnlyList<uint> entities, string path, string id)
    {
        return Guard(() =>
        {
            var type = session.TypeIndex("Sprite");
            if (type < 0 || AtlasFields(type) is not { } fields)
                throw new EngineException("Select an Engine SDK with sprite atlas support.");
            if (session.ProjectFolder is not { } project || !session.Editing ||
                !CanAssignSpriteSource(entities, type, fields.Atlas, fields.Frame) ||
                entities.Any(entity => session.SceneId(entity) == null))
                throw new EngineException("Select editable scene sprites in Edit mode.");

            var atlas = ProjectAtlas.Read(project, path, out var problem);
            if (atlas == null || problem != null)
                throw new EngineException(problem ?? $"Cannot read atlas {path}.");
            if (atlas.Find(id) == null)
                throw new EngineException($"Frame {id} is missing from {path}. Refresh the Asset Manager.");
            if (session.Image(atlas.Image, out problem) is not { } image)
                throw new EngineException(problem ?? $"Cannot read atlas PNG {atlas.Image}.");
            if (image.Width != atlas.Width || image.Height != atlas.Height)
                throw new EngineException($"Atlas PNG {atlas.Image} is {image.Width} x {image.Height} px. Expected {atlas.Width} x {atlas.Height} px.");

            return WriteSpriteSource(entities, type, "assign atlas frame", (fields.Atlas, path), (fields.Frame, id));
        });
    }

    internal void UseDirectSpriteImage(IReadOnlyList<uint> entities, int type)
    {
        Guard(() => AtlasFields(type) is { } fields &&
            WriteSpriteSource(entities, type, "use direct PNG", (fields.Atlas, ""), (fields.Frame, "")));
    }

    internal void SetDirectSpriteImage(IReadOnlyList<uint> entities, int type, int image, string path)
    {
        // The PNG chooser and text field both select the direct source. Preserve region and pivot.
        Guard(() => AtlasFields(type) is { } fields
            ? WriteSpriteSource(entities, type, "assign direct PNG", (image, path), (fields.Atlas, ""), (fields.Frame, ""))
            : WriteSpriteSource(entities, type, "assign direct PNG", (image, path)));
    }

    private bool WriteSpriteSource(IReadOnlyList<uint> entities, int type, string name, params (int Field, string Value)[] values)
    {
        if (entities.Any(entity => !session.IsAlive(entity) || !session.Has(entity, type)))
            throw new EngineException("The sprite selection has changed.");
        var changes = entities.SelectMany(entity => values.Select(value =>
            (Entity: entity, value.Field, value.Value, Before: session.GetText(entity, type, value.Field))))
            .Where(change => change.Before != change.Value).ToArray();
        if (changes.Length == 0) return true;
        if (!CanAssignSpriteSource(entities, type, changes.Select(change => change.Field).Distinct().ToArray()))
            throw new EngineException("The sprite source is controlled by the project or the selection has changed.");

        // Capture both strings together in the existing authoring history. All locks were checked
        // above. If a native setter fails, restore the writes that succeeded before it.
        return ChangeTogether(entities, name, () =>
        {
            var written = 0;
            try
            {
                foreach (var change in changes)
                {
                    session.SetText(change.Entity, type, change.Field, change.Value);
                    ++written;
                }
            }
            catch (EngineException)
            {
                while (written > 0)
                {
                    var change = changes[--written];
                    session.SetText(change.Entity, type, change.Field, change.Before);
                }
                throw;
            }
        });
    }
}
