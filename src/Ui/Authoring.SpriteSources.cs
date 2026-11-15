using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed partial class Authoring
{
    internal readonly record struct SpriteSourceFields(int Atlas, int Frame, int? Animation)
    {
        internal int[] All => Animation is { } animation ? [Atlas, Frame, animation] : [Atlas, Frame];
    }

    internal SpriteSourceFields? AtlasFields(int type) =>
        session.FindField(type, "atlas") is { } atlas && session.FindField(type, "frameId") is { } frame
            ? new(atlas, frame, session.FindField(type, "animation")) : null;

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
                !CanAssignSpriteSource(entities, type, fields.All) ||
                entities.Any(entity => session.SceneId(entity) == null))
                throw new EngineException("Select editable scene sprites in Edit mode.");

            _ = ValidateAtlasFrame(project, path, id);

            return WriteAtlasSource(entities, type, fields, "assign atlas frame", new(path, id));
        });
    }

    internal bool AssignAtlasAnimation(IReadOnlyList<uint> entities, string path, string name) => Guard(() =>
    {
        var type = session.TypeIndex("Sprite");
        if (type < 0 || AtlasFields(type) is not { Animation: not null } fields)
            throw new EngineException("Select an Engine SDK with sprite animation support.");
        if (session.ProjectFolder is not { } project || !session.Editing ||
            !CanAssignSpriteSource(entities, type, fields.All) || entities.Any(entity => session.SceneId(entity) == null))
            throw new EngineException("Select editable scene sprites in Edit mode.");

        var atlas = ValidateAtlas(project, path);
        var animation = atlas.Animations.FirstOrDefault(clip => clip.Name == name)
            ?? throw new EngineException($"Animation \"{name}\" is missing from {path}.");
        if (AtlasAnimation.Problem(atlas.Animations, atlas.Frames) is { } problem)
            throw new EngineException(problem);

        return WriteAtlasSource(entities, type, fields, "assign sprite animation", new(path, animation.FrameIds[0], name));
    });

    internal uint? PlaceAtlasFrame(ProjectAtlas.Reference reference, (double X, double Y) at, double fit) =>
        Guard<uint?>(() =>
        {
            var sprite = session.TypeIndex("Sprite");
            var transform = session.TypeIndex("Transform2D");
            if (sprite < 0 || transform < 0 || AtlasFields(sprite) is not { } fields)
                throw new EngineException("Select an Engine SDK with sprite atlas support.");
            if (session.ProjectFolder is not { } project || !session.Editing)
                throw new EngineException("Place atlas sprites in Edit mode.");

            var frame = ValidateAtlasFrame(project, reference.Atlas, reference.FrameId);
            var names = session.Entities().Select(session.EntityName).ToHashSet();
            var name = UniqueSpriteName(names, frame.Name.Replace('\0', ' '));
            var entity = session.CreateEntity(name);
            try
            {
                InitializeSprite(entity, transform, sprite, at);
                session.SetText(entity, sprite, fields.Atlas, reference.Atlas);
                session.SetText(entity, sprite, fields.Frame, reference.FrameId);
                FitSprite(entity, transform, Math.Max(frame.Width, frame.Height), fit);
                Record($"creation of sprite {Describe(entity)}", [entity], before: null, after: true);
            }
            catch (EngineException)
            {
                session.Destroy(entity);
                throw;
            }
            selection.Set(entity);
            return entity;
        });

    private ProjectAtlas.Frame ValidateAtlasFrame(string project, string path, string id)
    {
        var atlas = ValidateAtlas(project, path);
        return atlas.Find(id) ?? throw new EngineException($"Frame {id} is missing from {path}. Refresh the Asset Manager.");
    }

    private ProjectAtlas ValidateAtlas(string project, string path)
    {
        var atlas = ProjectAtlas.Read(project, path, out var problem);
        if (atlas == null || problem != null)
            throw new EngineException(problem ?? $"Cannot read atlas {path}.");
        if (session.Image(atlas.Image, out problem) is not { } image)
            throw new EngineException(problem ?? $"Cannot read atlas PNG {atlas.Image}.");
        if (image.Width != atlas.Width || image.Height != atlas.Height)
            throw new EngineException($"Atlas PNG {atlas.Image} is {image.Width} x {image.Height} px. Expected {atlas.Width} x {atlas.Height} px.");
        return atlas;
    }

    internal void UseDirectSpriteImage(IReadOnlyList<uint> entities, int type)
    {
        Guard(() => AtlasFields(type) is { } fields &&
            WriteAtlasSource(entities, type, fields, "use direct PNG", new("", "")));
    }

    internal void SetDirectSpriteImage(IReadOnlyList<uint> entities, int type, int image, string path)
    {
        // The PNG chooser and text field both select the direct source. Preserve region and pivot.
        Guard(() => AtlasFields(type) is { } fields
            ? WriteAtlasSource(entities, type, fields, "assign direct PNG", new("", ""), (image, path))
            : WriteSpriteSource(entities, type, "assign direct PNG", (image, path)));
    }

    private bool WriteAtlasSource(IReadOnlyList<uint> entities, int type, SpriteSourceFields fields, string action,
        ProjectAtlas.Reference reference, params (int Field, string Value)[] additional)
    {
        (int Field, string Value)[] values = fields.Animation is { } animation
            ? [(fields.Atlas, reference.Atlas), (fields.Frame, reference.FrameId), (animation, reference.Animation), .. additional]
            : [(fields.Atlas, reference.Atlas), (fields.Frame, reference.FrameId), .. additional];
        return WriteSpriteSource(entities, type, action, values);
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

        // Capture the source fields together in the existing authoring history. All locks were checked
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
