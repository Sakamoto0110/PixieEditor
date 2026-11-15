using System.Diagnostics;
using System.Text.Json.Nodes;
using Hexa.NET.GLFW;
using PixieEditor.Editing;
using PixieEditor.Native;
using PixieEditor.Ui;
using static PixieEditor.Editing.SpritesheetPacking;

internal static class SpritesheetBuilderTests
{
    private const string AtlasPath = "assets/actors.atlas.json";
    private const string SpritePath = "images/actor.png";
    private const string NeighborPath = "images/neighbor.png";
    private const string LegacyPath = "images/legacy.png";
    private const string SheetPath = "images/sheet.png";

    internal static unsafe int Run(string root, string hash, string work)
    {
        work = Path.GetFullPath(work);
        var project = Path.Combine(work, "builder-project");
        var sdk = EngineSdk.Load(root, "authoring", "Release", "builder fixture", expectedManifestSha256: hash);
        GameLibrary.SelectSdk(sdk);
        if (GLFW.Init() == 0) return 3;
        GLFW.WindowHint(GLFW.GLFW_VISIBLE, GLFW.GLFW_FALSE);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 4);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 5);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);
        GLFW.WindowHint(0x0002000E, -1840); // GLFW_POSITION_X
        GLFW.WindowHint(0x0002000F, 80); // GLFW_POSITION_Y
        var window = GLFW.CreateWindow(1600, 900, "Spritesheet Builder fixture", default, default);
        if (window.Handle == null) { GLFW.Terminate(); return 4; }
        try
        {
            GLFW.MakeContextCurrent(window);
            GameLibrary.Use(sdk.RuntimePath);
            EngineSession.CreateProject(project);
            var inputs = Directory.CreateDirectory(Path.Combine(work, "generated-inputs")).FullName;
            var actor = new byte[] { 230, 22, 37, 255 };
            var neighbor = new byte[] { 24, 220, 82, 255 };
            var legacy = new byte[] { 30, 90, 240, 255 };
            CreatePng(inputs, "actor.png", 48, 32, actor, [255, 165, 23, 255]);
            CreatePng(inputs, "neighbor.png", 96, 64, neighbor, [25, 200, 200, 255]);
            CreatePng(inputs, "legacy.png", 48, 32, legacy, [165, 55, 230, 255]);
            CreatePng(inputs, "sheet.png", 96, 64, [75, 175, 235, 255], [230, 168, 31, 255]);
            foreach (var file in Directory.GetFiles(inputs))
                ProjectAssets.Import(project, file);
            Check(ProjectAssets.Browse(project).Paths.Contains(SpritePath), "imported PNGs appear in the catalog");

            var draft = new SpritesheetDraft(packsImages: true) { Path = AtlasPath };
            draft.AddSource(SpritePath);
            var frame = draft.Entries[0].Frame with { Name = "Actor", PivotX = 0.25, PivotY = 0.75 };
            draft.Entries[0] = new(frame, SpritePath);
            var first = draft.Save(project);
            var original = first.Find(frame.Id)!;
            var firstPixels = ReadPng(Path.Combine(project, first.Image));
            Check(Pixel(firstPixels, original.X, original.Y).SequenceEqual(actor), "packing preserves the input pixels");
            Check(Pixel(firstPixels, original.X - draft.Extrusion, original.Y - draft.Extrusion).SequenceEqual(actor) &&
                Pixel(firstPixels, original.X - draft.Extrusion - 1, original.Y).SequenceEqual(new byte[4]),
                "packing extrudes corner and edge texels and leaves transparent padding");

            var slices = new SpritesheetDraft(packsImages: false) { Path = "assets/cells.atlas.json", Sheet = SheetPath };
            slices.AddSlices(Grid(96, 64, 24, 20, 1, 2, 3, 4));
            var firstSlice = slices.Entries[0].Frame;
            var count = slices.Entries.Count;
            slices.AddSlices([Rect.Of(firstSlice), new Rect(75, 48, 14, 12)]);
            Check(slices.Entries.Count == count + 1, "grid cells and a manual region coexist without duplicating an existing rectangle");
            slices.Entries[0] = new(firstSlice with { Name = "First cell", PivotX = 0.125 });
            var sliced = slices.Save(project);
            Check(sliced.Image == SheetPath && sliced.Frames[0] is { Name: "First cell", X: 1, Y: 2, PivotX: 0.125 },
                "slicing references the original PNG and stores the edited region metadata");

            ulong smoothId, pixelId, legacyId, cameraId;
            using (var session = EngineSession.OpenProject(400, 240, project))
            {
                var selection = new Selection();
                var history = new History();
                var authoring = new Authoring(session, selection, history);
                var recorder = new EditRecorder(session, history, () => authoring.Recording);
                authoring.SceneEditor = new SceneDocumentEditor(session, selection, history, recorder, () => false, () => { });
                authoring.Scenes = new SceneAuthoring(session, selection, history, authoring.SceneEditor);

                var smooth = authoring.PlaceAtlasFrame(new(AtlasPath, frame.Id), (-144, 0), 100)!.Value;
                smoothId = session.SceneId(smooth)!.Value;
                Check(history.UndoCount == 1, "placing an atlas frame records one scene operation");
                authoring.Undo();
                Check(session.Find(smoothId) == null, "undo removes the placed atlas sprite");
                authoring.Redo();
                smooth = session.Find(smoothId)!.Value;

                var pixel = authoring.PlaceImages([Path.Combine(project, LegacyPath)], (0, 0)).Single();
                Check(authoring.AssignAtlasFrame([pixel], AtlasPath, frame.Id), "Asset Manager assignment accepts the built atlas");
                var sprite = session.TypeIndex("Sprite");
                session.Set(pixel, sprite, session.FieldIndex(sprite, "filter"), new PixieValue { X = 1 });
                pixelId = session.SceneId(pixel)!.Value;
                var direct = authoring.PlaceImages([Path.Combine(project, LegacyPath)], (144, 0)).Single();
                legacyId = session.SceneId(direct)!.Value;
                Check(session.GetText(direct, sprite, session.FieldIndex(sprite, "atlas")) == "", "direct PNG stays a direct source");

                var camera = session.CreateEntity("Reduced zoom");
                cameraId = session.SceneId(camera)!.Value;
                var cameraType = session.TypeIndex("Camera2D");
                session.AddComponent(camera, session.TypeIndex("Transform2D"));
                session.AddComponent(camera, cameraType);
                session.Set(camera, cameraType, session.FieldIndex(cameraType, "zoom"), new PixieValue { X = 0.25 });
                session.Save();
                session.FreeCamera = (0, 0, 0.25);
                _ = session.Render();
                CheckQuad(session, smooth);
            }

            // Set the authored default camera for the generic shipping player.
            var scene = Directory.GetFiles(Path.Combine(project, "scenes"), "*.scene").Single();
            var savedScene = JsonNode.Parse(File.ReadAllText(scene))!.AsObject();
            savedScene["version"] = 2;
            savedScene["sceneScopes"] = new JsonArray();
            savedScene["presentation"] = new JsonObject { ["schemaVersion"] = 1, ["camera"] = cameraId.ToString("x16") };
            var entities = savedScene["entities"]!.AsArray();
            entities.Remove(entities.Single(entity => entity!["name"]!.GetValue<string>() == "shape"));
            var directSprite = entities.Single(entity => entity!["id"]!.GetValue<string>() == legacyId.ToString("x16"))!
                ["components"]!["Sprite"]!.AsObject();
            directSprite["v"] = 1;
            directSprite.Remove("atlas");
            directSprite.Remove("frameId");
            File.WriteAllText(scene, savedScene.ToJsonString());

            draft = SpritesheetDraft.Open(project, AtlasPath);
            var stale = SpritesheetDraft.Open(project, AtlasPath);
            draft.Entries[0] = draft.Entries[0] with { Frame = draft.Entries[0].Frame with { Name = "Renamed actor" } };
            draft.AddSource(NeighborPath);
            var repacked = draft.Save(project);
            var moved = repacked.Find(frame.Id)!;
            Check((original.X, original.Y) != (moved.X, moved.Y) &&
                moved is { Name: "Renamed actor", PivotX: 0.25, PivotY: 0.75 },
                "reopening the recipe and repacking moves the renamed frame with its ID and pivot intact");
            Check(File.Exists(Path.Combine(project, first.Image)) && repacked.Image != first.Image,
                "published generations have different immutable PNG paths");
            var descriptor = Path.Combine(project, AtlasPath);
            var bytes = File.ReadAllBytes(descriptor);
            ExpectIo(() => stale.Save(project), "a stale draft cannot overwrite a newer atlas");
            draft.Padding += 1;
            using (File.Open(descriptor, FileMode.Open, FileAccess.Read, FileShare.Read))
                ExpectIo(() => draft.Save(project), "a blocked descriptor replacement fails after staging");
            Check(bytes.SequenceEqual(File.ReadAllBytes(descriptor)) &&
                ReadPng(Path.Combine(project, repacked.Image)).Pixels.Length > 0,
                "failed rebuild leaves the descriptor and its usable PNG intact");
            Check(!Directory.EnumerateFiles(Path.GetDirectoryName(descriptor)!, "*.tmp").Any(),
                "failed rebuild removes temporary files");

            using (var session = EngineSession.OpenProject(400, 240, project))
            {
                var sprite = session.TypeIndex("Sprite");
                var smooth = session.Find(smoothId)!.Value;
                var pixel = session.Find(pixelId)!.Value;
                var direct = session.Find(legacyId)!.Value;
                Check(new[] { smooth, pixel }.All(entity =>
                    session.GetText(entity, sprite, session.FieldIndex(sprite, "frameId")) == frame.Id),
                    "the saved scene reopens with its original atlas IDs");
                session.SelectViewportCamera(2, cameraId);
                Capture(session, Path.Combine(work, "editor-reopened.png"));
                CheckQuad(session, smooth);
                CheckQuad(session, pixel);
                CheckSpritePixels(work, "editor-reopened.png", true);

                var repair = SpritesheetDraft.Open(project, AtlasPath);
                var removed = repair.Entries.ToArray();
                repair.Entries.Clear();
                Check(repair.Save(project).Frames.Count == 0, "a packed atlas can be saved after removing its last frame");
                WaitForAtlasPoll(session, smooth, 16);
                Check(session.GetText(smooth, sprite, session.FieldIndex(sprite, "frameId")) == frame.Id &&
                    session.GetText(direct, sprite, session.FieldIndex(sprite, "image")) == LegacyPath,
                    "a missing frame keeps the saved reference and the legacy PNG path");
                repair.Entries.AddRange(removed);
                repair.Save(project);
                WaitForAtlasPoll(session, smooth, 12);
                CheckQuad(session, smooth);
                Capture(session, Path.Combine(work, "editor-repaired.png"));
                CheckSpritePixels(work, "editor-repaired.png", true);
            }

            using var job = new ProjectExportJob();
            var output = Path.Combine(work, "shipping");
            Check(job.Start(sdk, project, output), "normal Editor export job starts the paired exporter");
            var timeout = Stopwatch.StartNew();
            while (!job.Finished && timeout.Elapsed < TimeSpan.FromSeconds(45)) Thread.Sleep(50);
            Check(job.Finished && !job.Failed, "normal Editor export completes with shipping verification");
            var exported = ProjectAtlas.Read(output, AtlasPath, out var problem);
            Check(exported?.Find(frame.Id) == moved && problem == null &&
                File.Exists(Path.Combine(output, LegacyPath)) && !File.Exists(Path.Combine(output, SpritePath)),
                "shipping package contains the resolved atlas generation and legacy PNG");
            var exportedScene = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "scenes/main.scene")))!;
            Check(exportedScene["entities"]!.AsArray().Any(entity =>
                    entity!["components"]?["Sprite"]?["v"]?.GetValue<int>() == 1),
                "shipping package still contains the version 1 direct-PNG sprite");
            return 0;
        }
        finally
        {
            GLFW.DestroyWindow(window);
            GLFW.Terminate();
        }
    }

    internal static void CheckSpritePixels(string work, string capture, bool editor)
    {
        var rendered = ReadPng(Path.Combine(work, capture));
        var cx = rendered.Width / 2;
        var cy = rendered.Height / 2;
        var zoom = editor ? 0.25 : 1.0; // The generic player starts with its free camera.
        foreach (var worldX in new[] { -144, 0 })
        {
            var center = cx + (int)(worldX * zoom);
            var left = center - (int)(12 * zoom);
            var right = left + (int)(48 * zoom);
            var red = Pixel(rendered, center - (int)(8 * zoom), cy);
            var yellow = Pixel(rendered, center + (int)(16 * zoom), cy);
            Check(red[0] > 150 && red[1] < 100 && red[2] < 100 &&
                yellow[0] > 170 && yellow[1] > 95 && yellow[2] < 100 &&
                Pixel(rendered, left, cy)[0] > 150 && Pixel(rendered, left - 1, cy)[0] < 100 &&
                Pixel(rendered, right - 1, cy)[0] > 150 && Pixel(rendered, right, cy)[0] < 100 &&
                Pixel(rendered, center, cy - (int)(24 * zoom) - 1)[0] < 100 &&
                Pixel(rendered, center, cy + (int)(8 * zoom))[0] < 100,
                $"{capture} {(worldX < 0 ? "Smooth" : "Pixel")} at {zoom}× shows the frame with its saved pivot");
        }
        var blue = Pixel(rendered, cx + (int)(136 * zoom), cy);
        Check(blue[2] > 150 && blue[0] < 100, $"{capture} shows the direct PNG");
        if (!editor)
            Check(rendered.Width == 1600 && rendered.Height == 900, "standalone capture uses the requested framebuffer size");
    }

    private static void CheckQuad(EngineSession session, uint entity)
    {
        var quad = session.ViewQuad(entity)!;
        var transform = session.TypeIndex("Transform2D");
        var point = session.Get(entity, transform, session.FieldIndex(transform, "position"));
        var pivot = session.WorldToView(point.X, point.Y);
        Check(Math.Abs(pivot.X - quad[0].X - 3) < 0.001 && Math.Abs(pivot.Y - quad[0].Y - 6) < 0.001 &&
            Math.Abs(quad[1].X - quad[0].X - 12) < 0.001 && Math.Abs(quad[3].Y - quad[0].Y - 8) < 0.001,
            "native view geometry uses the saved 0.25/0.75 pivot and 48×32 rectangle at 0.25×");
    }

    private static void WaitForAtlasPoll(EngineSession session, uint entity, double width)
    {
        var time = Stopwatch.StartNew();
        do
        {
            _ = session.Render();
            var quad = session.ViewQuad(entity)!;
            if (Math.Abs(quad[1].X - quad[0].X - width) < 0.001)
            {
                Console.WriteLine($"PASS native atlas cache resolved sprite width {width}px");
                return;
            }
            Thread.Sleep(50);
        } while (time.Elapsed < TimeSpan.FromSeconds(3));
        throw new InvalidOperationException($"Sprite did not resolve to {width}px.");
    }

    private static unsafe void Capture(EngineSession session, string path)
    {
        const int width = 400, height = 240;
        var texture = session.Render();
        var get = (delegate* unmanaged[Stdcall]<uint, int, uint, uint, int, void*, void>)GLFW.GetProcAddress("glGetTextureImage");
        var pixels = new byte[width * height * 4];
        fixed (byte* data = pixels)
            get(texture, 0, Gl.Rgba, Gl.UnsignedByte, pixels.Length, data);
        for (var y = 0; y < height / 2; ++y)
        {
            var top = pixels.AsSpan(y * width * 4, width * 4);
            var bottom = pixels.AsSpan((height - 1 - y) * width * 4, width * 4);
            var copy = top.ToArray();
            bottom.CopyTo(top);
            copy.CopyTo(bottom);
        }
        using var output = File.Create(path);
        new Image(width, height, pixels).Write(output);
    }

    private static void CreatePng(string folder, string name, int width, int height, byte[] left, byte[] right)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; ++y)
            for (var x = 0; x < width; ++x)
                (x < width / 2 ? left : right).CopyTo(pixels, (y * width + x) * 4);
        using var file = File.Create(Path.Combine(folder, name));
        new Image(width, height, pixels).Write(file);
    }

    private static ReadOnlySpan<byte> Pixel(Image image, int x, int y) =>
        image.Pixels.AsSpan((y * image.Width + x) * 4, 4);

    private static void ExpectIo(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Check(true, message); return; }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine($"PASS {message}");
    }
}
