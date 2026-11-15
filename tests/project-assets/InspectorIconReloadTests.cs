using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Hexa.NET.GLFW;
using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;
using PixieEditor.Ui;
using PixieInspector.ImGui;

internal static class InspectorIconReloadTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    // Accept an existing SDK-built project module; no compilation or project writes are needed.
    internal static unsafe int Run(string root, string catalogHash, string project, string module)
    {
        var sdk = EngineSdk.Load(root, "authoring", "Release", "Inspector icon reload test", expectedManifestSha256: catalogHash);
        GameLibrary.SelectSdk(sdk);
        GameLibrary.Use(sdk.RuntimePath);
        if (GLFW.Init() == 0) return 3;
        GLFW.WindowHint(GLFW.GLFW_VISIBLE, GLFW.GLFW_FALSE);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 4);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 5);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);
        GLFW.WindowHint(0x0002000E, -1880); // GLFW_POSITION_X
        GLFW.WindowHint(0x0002000F, 80); // GLFW_POSITION_Y
        var window = GLFW.CreateWindow(640, 480, "Inspector icon reload test", default, default);
        if (window.Handle == null) { GLFW.Terminate(); return 4; }
        try
        {
            GLFW.MakeContextCurrent(window);
            var context = ImGui.CreateContext();
            var io = ImGui.GetIO();
            io.IniFilename = null;
            try
            {
                using var session = EngineSession.OpenProject(640, 480, project);
                var entity = session.CreateEntity("Inspector icon fixture");
                session.AddComponent(entity, session.TypeIndex("Shape"));
                var sceneId = session.SceneId(entity)!.Value;
                var selection = new Selection();
                var history = new History();
                var authoring = new Authoring(session, selection, history);
                var recorder = new EditRecorder(session, history, () => authoring.Recording);
                using var panel = new InspectorPanel(session, authoring, recorder, new AssetChooser(project));
                ImGuiEditIcons Bind()
                {
                    // Exercise the Inspector's normal rebind without drawing a visible window.
                    typeof(InspectorPanel).GetMethod("Rebuild", PrivateInstance)!.Invoke(panel,
                        [new[] { session.Find(sceneId)!.Value }, new[] { session.TypeIndex("Shape") }]);
                    var sections = (IEnumerable)typeof(InspectorPanel).GetField("Sections", PrivateInstance)!.GetValue(panel)!;
                    var views = sections.Cast<object>().Select(section =>
                        (ImGuiInspectorView)section.GetType().GetProperty("View")!.GetValue(section)!).ToArray();
                    var icons = views.First().EditIcons ?? throw new InvalidOperationException("Inspector icons were not loaded.");
                    Check(views.All(view => ReferenceEquals(view.EditIcons, icons)), "component views share current icons");
                    return icons;
                }
                var initial = Bind();
                var lockPixels = Pixels(initial.Lock);
                var chainPixels = Pixels(initial.Chain);
                Check(lockPixels != chainPixels, "initial lock and chain contain distinct image pixels");
                void Verify(ImGuiEditIcons icons, string stage)
                {
                    // Read the view's actual GL handles before asking the new session to load any images.
                    // glIsTexture alone is insufficient: a retired texture name can be reused for another image.
                    Check(Pixels(icons.Lock) == lockPixels && Pixels(icons.Chain) == chainPixels,
                        stage + ": lock and chain retain their correct GPU pixels");
                    var folder = Path.Combine(AppContext.BaseDirectory, "assets", "inspector");
                    var locked = session.Image(Path.Combine(folder, "lock.png"), out _)!.Value;
                    var chain = session.Image(Path.Combine(folder, "chain.png"), out _)!.Value;
                    Check(icons.Lock.GetTexID().Equals(new ImTextureID(locked.Texture)) &&
                        icons.Chain.GetTexID().Equals(new ImTextureID(chain.Texture)), stage + ": handles belong to current session");
                }
                Verify(Bind(), "same-session rebind");
                var revision = session.BindingRevision;
                session.PrepareBindingsReplacement();
                var rejected = false;
                try { using var _ = session.OpenReloadCandidate(640, 480, Path.Combine(project, "missing-icon-test-module.dll")); }
                catch (EngineException) { rejected = true; }
                Check(rejected && session.BindingRevision == revision, "rejected candidate preserves current session");
                Verify(Bind(), "rejected reload");
                for (var round = 1; round <= 2; round++)
                {
                    var identity = session.ProjectRevision().SessionIdentity;
                    session.PrepareBindingsReplacement();
                    using (var candidate = session.OpenReloadCandidate(640, 480, module))
                    {
                        candidate.Render();
                        session.PublishReload(candidate, module);
                    } // Destroy the retired session and its textures before rebinding.
                    Check(session.ProjectRevision().SessionIdentity != identity, "reload publishes a different native session");
                    Verify(Bind(), $"reload {round}");
                }
                return 0;
            }
            finally { ImGui.DestroyContext(context); }
        }
        finally { GLFW.DestroyWindow(window); GLFW.Terminate(); }
    }

    private static unsafe string Pixels(ImTextureRef reference)
    {
        var texture = (uint)reference.GetTexID().Handle;
        var isTexture = (delegate* unmanaged[Stdcall]<uint, byte>)GLFW.GetProcAddress("glIsTexture");
        Check(isTexture(texture) != 0, "Inspector texture is alive");
        var parameter = (delegate* unmanaged[Stdcall]<uint, int, uint, int*, void>)GLFW.GetProcAddress("glGetTextureLevelParameteriv");
        int width, height;
        parameter(texture, 0, 0x1000, &width); // GL_TEXTURE_WIDTH
        parameter(texture, 0, 0x1001, &height); // GL_TEXTURE_HEIGHT
        Check(width > 0 && height > 0 && width <= 4096 && height <= 4096, "Inspector texture has valid dimensions");
        var pixels = new byte[checked(width * height * 4)];
        var read = (delegate* unmanaged[Stdcall]<uint, int, uint, uint, int, void*, void>)GLFW.GetProcAddress("glGetTextureImage");
        fixed (byte* destination = pixels)
            read(texture, 0, 0x1908, 0x1401, pixels.Length, destination); // GL_RGBA, GL_UNSIGNED_BYTE
        var error = (delegate* unmanaged[Stdcall]<uint>)GLFW.GetProcAddress("glGetError");
        Check(error() == 0, "GPU texture read succeeds");
        return $"{width}x{height}:{Convert.ToHexString(SHA256.HashData(pixels))}";
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
