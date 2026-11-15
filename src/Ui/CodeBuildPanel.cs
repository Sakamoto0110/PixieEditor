using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed class CodeBuildPanel : IDisposable
{
    private ProjectBuild? Build;
    private int LinesShown;
    private bool Visible;
    private string? Error;
    internal bool Pending { get; private set; }
    internal bool HasOutput => Build != null;

    internal void Start(ProjectBuild.BuildPlan plan)
    {
        Build?.Dispose();
        Build = new(plan, stageModuleForEditor: true);
        LinesShown = 0;
        Error = null;
        Pending = Visible = true;
        Build.Start();
    }

    internal void Show() => Visible = true;

    internal void ApplyIfReady(bool canApply, Action<string> apply)
    {
        if (!Pending || Build is not { Finished: true } build) return;
        if (build.BuiltLibrary is { } library)
        {
            if (!canApply) return;
            Pending = false;
            try
            {
                apply(library);
                Visible = false;
                Status.Show("Project code reloaded");
            }
            catch (Exception error) when (error is EngineException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                Error = error.Message;
            }
        }
        else
        {
            Pending = false;
            Error = build.Problem ?? "The build did not produce a game module.";
        }
        if (Error != null)
        {
            Visible = true;
            Status.Show(Error, error: true);
        }
    }

    internal void Draw(bool editing)
    {
        if (!Visible || Build == null) return;
        ImGui.SetNextWindowSize(new Vector2(720, 340), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Project build", ref Visible))
        {
            if (Error != null) ImGui.TextWrapped(Error);
            else if (!Build.Finished) ImGui.TextUnformatted("Building project code...");
            else if (Pending && !editing) ImGui.TextUnformatted("Stop Play to apply the rebuilt code.");
            else if (Pending) ImGui.TextUnformatted("Waiting to apply the rebuilt code...");
            BuildScreen.DrawOutput(Build.Lines, ref LinesShown);
        }
        ImGui.End();
    }

    public void Dispose() => Build?.Dispose();
}
