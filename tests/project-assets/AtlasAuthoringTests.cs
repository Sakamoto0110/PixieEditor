using Hexa.NET.GLFW;
using System.Text.Json.Nodes;
using PixieEditor.Editing;
using PixieEditor.Native;
using PixieEditor.Ui;

internal static class AtlasAuthoringTests
{
    private const string AtlasPath = AtlasFixture.AtlasPath;
    private const string ImagePath = AtlasFixture.ImagePath;
    private const string FrameId = AtlasFixture.FrameId;
    // Generated 2x2 RGBA PNG.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFElEQVR4nGP8z8Dwn4GBgYGJAQoAHxcCAk+Uzr4AAAAASUVORK5CYII=";

    internal static unsafe int Run(string root, string catalogHash, string work, bool exerciseAnimation = false)
    {
        var sdk = EngineSdk.Load(root, "authoring", "Release", "atlas fixture", expectedManifestSha256: catalogHash);
        GameLibrary.SelectSdk(sdk);
        if (GLFW.Init() == 0) return 3;
        GLFW.WindowHint(GLFW.GLFW_VISIBLE, GLFW.GLFW_FALSE);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 4);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 5);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);
        GLFW.WindowHint(0x0002000E, -1840); // GLFW_POSITION_X
        GLFW.WindowHint(0x0002000F, 80); // GLFW_POSITION_Y
        var window = GLFW.CreateWindow(1600, 900, "Atlas authoring test context", default, default);
        if (window.Handle == null)
        {
            GLFW.Terminate();
            return 4;
        }
        try
        {
            GLFW.MakeContextCurrent(window);
            GameLibrary.Use(sdk.RuntimePath);
            var folder = Path.Combine(Path.GetFullPath(work), "atlas-project");
            EngineSession.CreateProject(folder);
            Directory.CreateDirectory(Path.Combine(folder, "assets"));
            var atlasFile = Path.Combine(folder, AtlasPath);
            var png = Convert.FromBase64String(Png);
            File.WriteAllBytes(Path.Combine(folder, ImagePath), png);
            const string legacy = "assets/legacy.png";
            File.WriteAllBytes(Path.Combine(folder, legacy), png);
            File.WriteAllText(atlasFile, AtlasFixture.Descriptor());
            ulong[] ids;
            using (var session = EngineSession.OpenProject(64, 64, folder))
            {
                var selection = new Selection();
                var history = new History();
                var authoring = new Authoring(session, selection, history);
                var recorder = new EditRecorder(session, history, () => authoring.Recording);
                authoring.SceneEditor = new SceneDocumentEditor(session, selection, history, recorder, () => false, () => { });
                authoring.Scenes = new SceneAuthoring(session, selection, history, authoring.SceneEditor);
                var sprite = session.TypeIndex("Sprite");
                var transform = session.TypeIndex("Transform2D");
                var image = session.FieldIndex(sprite, "image");
                var region = session.FieldIndex(sprite, "region");
                var pivot = session.FieldIndex(sprite, "pivot");
                var atlas = session.FieldIndex(sprite, "atlas");
                var frame = session.FieldIndex(sprite, "frameId");
                var entities = Enumerable.Range(1, 2).Select(index => session.CreateEntity($"Sprite {index}")).ToArray();
                foreach (var entity in entities)
                {
                    session.AddComponent(entity, transform);
                    session.AddComponent(entity, sprite);
                    session.SetText(entity, sprite, image, legacy);
                    session.Set(entity, sprite, region, new PixieValue { Z = 1, W = 2 });
                    session.Set(entity, sprite, pivot, new PixieValue { X = 0.25, Y = 0.75 });
                }
                selection.Set(entities);
                ids = entities.Select(entity => session.SceneId(entity)!.Value).ToArray();
                bool UsesAtlas() => selection.Items.Count == 2 && selection.Items.All(entity =>
                    session.GetText(entity, sprite, atlas) == AtlasPath && session.GetText(entity, sprite, frame) == FrameId);
                bool UsesDirect(string path) => selection.Items.Count == 2 && selection.Items.All(entity =>
                    session.GetText(entity, sprite, image) == path &&
                    session.GetText(entity, sprite, atlas) == "" && session.GetText(entity, sprite, frame) == "");
                Check(session.Image(ImagePath, out _) is { Width: 2, Height: 2 }, "native SDK decodes generated atlas PNG");
                Check(authoring.AssignAtlasFrame(selection.Items, AtlasPath, FrameId) && UsesAtlas() && history.UndoCount == 1,
                    "assign frame to two sprites as one undo step");
                Check(authoring.AssignAtlasFrame(selection.Items, AtlasPath, FrameId) && history.UndoCount == 1,
                    "repeat assignment creates no history step");
                authoring.Undo();
                Check(UsesDirect(legacy), "undo restores direct PNG on both sprites");
                authoring.Redo();
                Check(UsesAtlas(), "redo restores both atlas fields");
                Check(selection.Items.All(entity => session.GetText(entity, sprite, image) == legacy &&
                    session.Get(entity, sprite, region).W == 2 && session.Get(entity, sprite, pivot).Y == 0.75),
                    "atlas assignment preserves direct PNG, region and pivot");
                authoring.UseDirectSpriteImage(selection.Items, sprite);
                Check(UsesDirect(legacy), "use direct PNG restores the preserved source");
                authoring.Undo();
                Check(UsesAtlas(), "undo restores atlas after using direct PNG");
                authoring.SetDirectSpriteImage(selection.Items, sprite, image, ImagePath);
                Check(UsesDirect(ImagePath), "choosing a PNG clears the atlas reference");
                authoring.Undo();
                Check(UsesAtlas(), "undo restores atlas after choosing PNG");

                var steps = history.UndoCount;
                File.WriteAllText(atlasFile, AtlasFixture.Descriptor(includeFrame: false));
                Check(!authoring.AssignAtlasFrame(selection.Items, AtlasPath, FrameId) && UsesAtlas() && history.UndoCount == steps,
                    "a removed frame refuses assignment without rewriting the reference");
                File.WriteAllText(atlasFile, AtlasFixture.Descriptor(width: 3));
                Check(!authoring.AssignAtlasFrame(selection.Items, AtlasPath, FrameId) && UsesAtlas() && history.UndoCount == steps,
                    "mismatched decoded PNG dimensions refuse assignment");
                File.WriteAllText(atlasFile, AtlasFixture.Descriptor());
                session.Save();
                authoring.Scenes.Convert();
                Check(authoring.AssignAtlasFrame(selection.Items, AtlasPath, AtlasFixture.OtherId) &&
                    selection.Items.All(entity => session.GetText(entity, sprite, frame) == AtlasFixture.OtherId),
                    "assign a different frame in a v2 scene");
                authoring.Undo();
                Check(UsesAtlas(), "v2 undo restores both references through candidate publication");
                session.Save();
            }
            File.WriteAllText(atlasFile, AtlasFixture.Descriptor("Renamed", 1));
            using var reopened = EngineSession.OpenProject(64, 64, folder);
            var spriteType = reopened.TypeIndex("Sprite");
            Check(ids.All(id => reopened.Find(id) is { } entity &&
                reopened.GetText(entity, spriteType, reopened.FieldIndex(spriteType, "atlas")) == AtlasPath &&
                reopened.GetText(entity, spriteType, reopened.FieldIndex(spriteType, "frameId")) == FrameId),
                "reopen retains the authored IDs after rename and repack");
            Check(ProjectAtlas.Read(folder, AtlasPath, out var problem)?.Find(FrameId) is { Name: "Renamed", X: 1 } && problem == null,
                "the browser resolves the renamed, moved frame by ID");
            if (exerciseAnimation) CheckAnimation(reopened, folder, ids[0], atlasFile);
            return 0;
        }
        finally
        {
            GLFW.DestroyWindow(window);
            GLFW.Terminate();
        }
    }

    private static void CheckAnimation(EngineSession session, string folder, ulong id, string atlasFile)
    {
        const string clip = "Walk";
        var descriptor = JsonNode.Parse(AtlasFixture.Descriptor())!;
        descriptor["frames"]![1]!["rect"]![2] = 2;
        descriptor["animations"] = new JsonArray(new JsonObject
        {
            ["name"] = clip,
            ["fps"] = 30,
            ["loop"] = true,
            ["frameIds"] = new JsonArray(FrameId, AtlasFixture.OtherId),
        });
        File.WriteAllText(atlasFile, descriptor.ToJsonString());

        var selection = new Selection();
        var history = new History();
        var authoring = new Authoring(session, selection, history);
        var recorder = new EditRecorder(session, history, () => authoring.Recording);
        authoring.SceneEditor = new SceneDocumentEditor(session, selection, history, recorder, () => false, () => { });
        var sprite = session.TypeIndex("Sprite");
        var field = session.FieldIndex(sprite, "animation");
        uint Entity() => session.Find(id)!.Value;
        string Animation() => session.GetText(Entity(), sprite, field);

        selection.Set([Entity()]);
        Check(authoring.AssignAtlasAnimation(selection.Items, AtlasPath, clip) && Animation() == clip && history.UndoCount == 1,
            "assigning an atlas animation to a sprite records one undo step");
        authoring.Undo();
        Check(Animation() == "", "undo returns the sprite to a still frame");
        authoring.Redo();
        Check(Animation() == clip, "redo restores the animation");
        Check(authoring.AssignAtlasFrame([Entity()], AtlasPath, FrameId) && Animation() == "",
            "assigning a still frame clears the animation");
        authoring.Undo();
        session.Save();

        using var reopened = EngineSession.OpenProject(64, 64, folder);
        uint ReopenedEntity() => reopened.Find(id)!.Value;
        double DrawnWidth()
        {
            reopened.Render();
            var quad = reopened.ViewQuad(ReopenedEntity())!;
            return quad[1].X - quad[0].X;
        }
        Check(reopened.GetText(ReopenedEntity(), sprite, field) == clip,
            "a saved scene reopens with its animation assigned");
        Check(DrawnWidth() == 1, "the first animation frame renders in Edit mode");
        reopened.Play();
        reopened.Paused = true;
        Check(DrawnWidth() == 1, "Play starts at the first frame");
        reopened.Step();
        Check(DrawnWidth() == 1, "the first fixed step stays within the first frame at 30 FPS");
        reopened.Step();
        Check(DrawnWidth() == 2, "the animation changes the resolved sprite geometry at 30 FPS");
        reopened.Advance(0.5);
        Check(DrawnWidth() == 2, "paused playback holds the current animation frame");
        reopened.Step();
        reopened.Step();
        Check(DrawnWidth() == 1, "animation wraps back to the first frame");
        reopened.Stop();
        Check(DrawnWidth() == 1 && reopened.GetText(ReopenedEntity(), sprite, field) == clip,
            "Stop restores the authored scene and first animation frame");

        var fps = reopened.FieldIndex(sprite, "animationFps");
        reopened.Set(ReopenedEntity(), sprite, fps, new PixieValue { X = 60 });
        reopened.Save();
        using var overridden = EngineSession.OpenProject(64, 64, folder);
        uint OverriddenEntity() => overridden.Find(id)!.Value;
        Check(overridden.Get(OverriddenEntity(), sprite, fps).X == 60, "the sprite's FPS override survives save and reopen");
        overridden.Play();
        overridden.Paused = true;
        overridden.Step();
        overridden.Render();
        var faster = overridden.ViewQuad(OverriddenEntity())!;
        Check(faster[1].X - faster[0].X == 2, "the second frame renders after one fixed step with an FPS override of 60");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine($"PASS {message}");
    }
}
