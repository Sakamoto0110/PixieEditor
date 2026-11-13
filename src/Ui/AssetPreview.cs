using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Editing;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal static class AssetPreview
{
    public static void DrawImage(EngineSession session, string path, float maxSize)
    {
        if (session.Image(path, out var problem) is not { } found)
        {
            DrawProblem(problem ?? path);
            return;
        }

        DrawTexture(found.Texture, found.Width, found.Height, maxSize, Vector2.Zero, Vector2.One);
        ImGui.SameLine();
        ImGui.TextDisabled($"{found.Width} x {found.Height} px");
    }

    public static bool DrawFrame(EngineSession session, ProjectAtlas atlas, ProjectAtlas.Frame frame, float maxSize)
    {
        if (session.Image(atlas.Image, out var problem) is not { } image)
        {
            DrawProblem(problem ?? atlas.Image);
            return false;
        }
        if (image.Width != atlas.Width || image.Height != atlas.Height)
        {
            DrawProblem($"{atlas.Image} is {image.Width} x {image.Height} px. Expected {atlas.Width} x {atlas.Height} px.");
            return false;
        }
        var dimensions = new Vector2(atlas.Width, atlas.Height);
        // Inset half a texel so the preview's linear sampler stays inside the frame.
        DrawTexture(image.Texture, frame.Width, frame.Height, maxSize,
            new Vector2(frame.X + 0.5f, frame.Y + 0.5f) / dimensions,
            new Vector2(frame.X + frame.Width - 0.5f, frame.Y + frame.Height - 0.5f) / dimensions);
        return true;
    }

    private static void DrawProblem(string problem)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(new Vector4(1.0f, 0.45f, 0.4f, 1.0f), problem);
        ImGui.PopTextWrapPos();
    }

    private static unsafe void DrawTexture(uint texture, int width, int height, float maxSize, Vector2 uvStart, Vector2 uvEnd)
    {
        var scale = maxSize / Math.Max(width, height);
        var size = new Vector2(width * scale, height * scale);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(origin, origin + size, ImGui.GetColorU32(ImGuiCol.FrameBg));
        // The texture's first row is the image's top, as ImGui draws it.
        ImGui.Image(new ImTextureRef(null, new ImTextureID(texture)), size, uvStart, uvEnd);
    }
}
