using Hexa.NET.GLFW;
using PixieEditor.Native;

internal static class ModuleReloadTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }

    internal static unsafe int Run(EngineSdk sdk, string root)
    {
        GameLibrary.SelectSdk(sdk);
        GameLibrary.Use(sdk.RuntimePath);
        var project = Path.Combine(Path.GetFullPath(root), "ReloadProbe");
        EngineSession.CreateProject(project);
        SdkRequirements.Adopt(project, sdk);
        var action = ProjectCode.CreateTriggerAction(project, "ReloadAction", sdk);
        var source = Path.Combine(project, action.Source);
        var stub = File.ReadAllText(source);
        string Implementation(int red) => "#include \"pixie/runtime/Shape.hpp\"\n" + stub.Replace(
            "// Add the code to run when this trigger fires.",
            $"entity.Get<pixie::Shape>().color = pxColorRgba({red}, 34, 56, 255);", StringComparison.Ordinal);
        File.WriteAllText(source, Implementation(12));
        var plan = ProjectBuild.Prepare(project, sdk);
        using var build = new ProjectBuild(plan, stageModuleForEditor: true);
        void Build()
        {
            build.Start();
            if (!SpinWait.SpinUntil(() => build.Finished, TimeSpan.FromSeconds(90)))
                throw new TimeoutException("Project module build timed out.");
        }
        Build();
        var oldModule = build.BuiltLibrary ?? throw new InvalidOperationException(build.Problem);

        if (GLFW.Init() == 0) return 3;
        GLFW.WindowHint(GLFW.GLFW_VISIBLE, GLFW.GLFW_FALSE);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 4);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 5);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);
        var window = GLFW.CreateWindow(640, 480, "Project module reload qualification", default, default);
        if (window.Handle == null) { GLFW.Terminate(); return 4; }
        try
        {
            GLFW.MakeContextCurrent(window);
            using var session = EngineSession.OpenProject(640, 480, project);
            using (var initial = session.OpenReloadCandidate(640, 480, oldModule))
                session.PublishReload(initial, oldModule);
            var entity = session.Entities().Single();
            var sceneId = session.SceneId(entity)!.Value;
            var trigger = session.TypeIndex("InputTriggers");
            var list = session.FieldIndex(trigger, "bindings");
            session.AddComponent(entity, trigger);
            session.EditRecord(entity, trigger, list, 0, remove: false);
            var record = session.RecordInfo(entity, trigger, list).Type;
            session.SetRecord(entity, trigger, list, 0, session.FieldIndex(record, "code"), new PixieValue { X = 290 });
            session.SetRecordText(entity, trigger, list, 0, session.FieldIndex(record, "action"), action.Id);
            var authored = session.ProjectSnapshot();
            var oldIdentity = session.ProjectRevision().SessionIdentity;
            double PressF1()
            {
                session.Play();
                session.Paused = true;
                session.Key(290, true);
                session.Step();
                var shape = session.TypeIndex("Shape");
                var red = session.Get(session.Find(sceneId)!.Value, shape, session.FieldIndex(shape, "color")).X;
                session.Key(290, false);
                session.Stop();
                return red;
            }
            Check(PressF1() == 12, "initial module executes F1");

            File.AppendAllText(source, "\n#error Injected compile failure\n");
            Build();
            Check(build.BuiltLibrary == null && build.Problem != null, "failed rebuild produces no publishable module");
            Check(session.ProjectRevision().SessionIdentity == oldIdentity && PressF1() == 12,
                "failed rebuild retains the old session and code");

            File.WriteAllText(source, Implementation(210));
            Build();
            var newModule = build.BuiltLibrary ?? throw new InvalidOperationException(build.Problem);
            Check(newModule != oldModule, "rebuild stages a distinct DLL while the old DLL is loaded");
            var rejected = false;
            try { using var _ = session.OpenReloadCandidate(640, 480, Path.Combine(root, "missing.dll")); }
            catch (EngineException) { rejected = true; }
            Check(rejected && session.ProjectRevision().SessionIdentity == oldIdentity && PressF1() == 12,
                "failed module admission retains the old session and code");

            using (var next = session.OpenReloadCandidate(640, 480, newModule))
            {
                Check(next.Unsaved && next.ProjectSnapshot() == authored, "candidate preserves unsaved trigger binding");
                session.PublishReload(next, newModule);
            }
            Check(session.ProjectRevision().SessionIdentity != oldIdentity && PressF1() == 210,
                "the same host context dispatches F1 to the newly loaded code");
            return 0;
        }
        finally
        {
            GLFW.DestroyWindow(window);
            GLFW.Terminate();
        }
    }
}
