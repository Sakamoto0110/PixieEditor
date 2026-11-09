using System.Numerics;
using Hexa.NET.ImGui;
using PixieEditor.Native;

namespace PixieEditor.Ui;

internal sealed unsafe partial class ViewportPanel
{
    private bool ShowCircleGuide = true;
    private uint? CircleEntity;
    private long CircleRevision;
    private EngineSession.AuthoringCircle? PreviousCircle;
    private EngineSession.AuthoringCircle? CircleAtGestureStart;
    private const uint CircleColor = 0xE6F3C16B;
    private const uint OldCircleColor = 0x687FB4D8;

    private EngineSession.AuthoringCircle? ReadCircleGuide()
    {
        var entity = selection.Items.Count == 1 ? selection.Items[0] : (uint?)null;
        if (entity != CircleEntity || CircleRevision != session.BindingRevision)
        {
            CircleEntity = entity;
            CircleRevision = session.BindingRevision;
            PreviousCircle = null;
            CircleAtGestureStart = null;
        }
        var guide = entity is { } id ? session.CircleGuide(id) : null;
        if (guide == null)
        {
            PreviousCircle = null;
            CircleAtGestureStart = null;
            return null;
        }

        ImGui.Checkbox("Show rotation envelope", ref ShowCircleGuide);
        if (session.Edits.GestureActive?.Invoke() == true)
            CircleAtGestureStart ??= PreviousCircle;
        else
            CircleAtGestureStart = null;
        PreviousCircle = guide;
        return ShowCircleGuide ? guide : null;
    }

    private void DrawCircleGuide(EngineSession.AuthoringCircle? current, Vector2 viewportSize)
    {
        if (current is not { } circle || selection.Items.Count != 1 || selection.Items[0] != CircleEntity) return;
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(ImageOrigin, ImageOrigin + viewportSize, true);
        var label = $"Diameter {circle.Diameter:0.00} m";
        if (CircleAtGestureStart is { } previous && Math.Abs(circle.Diameter - previous.Diameter) >= 0.005)
        {
            DrawRing(draw, previous, OldCircleColor);
            label += $" ({circle.Diameter - previous.Diameter:+0.00;-0.00} m)";
        }
        DrawRing(draw, circle, CircleColor);
        var center = ProjectPoint(circle.X, circle.Y);
        var labelSize = ImGui.CalcTextSize(label);
        var at = center - new Vector2(labelSize.X * 0.5f, ScreenRadius(circle) + labelSize.Y + 4f);
        var maximum = Vector2.Max(ImageOrigin, ImageOrigin + viewportSize - labelSize);
        draw.AddText(Vector2.Clamp(at, ImageOrigin, maximum), CircleColor, label);
        draw.PopClipRect();
    }

    private void DrawRing(ImDrawListPtr draw, EngineSession.AuthoringCircle circle, uint color)
    {
        var radius = ScreenRadius(circle);
        if (!float.IsFinite(radius) || radius < 1f) return;
        var center = ProjectPoint(circle.X, circle.Y);
        const int dashes = 48;
        const float step = MathF.Tau / dashes;
        for (var i = 0; i < dashes; i++)
        {
            var a = i * step;
            var b = a + step * 0.62f;
            draw.AddLine(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius,
                center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius, color, 1.5f);
        }
    }

    private Vector2 ProjectPoint(double x, double y)
    {
        var pixel = session.WorldToView(x, y);
        return ImageOrigin + new Vector2((float)pixel.X, (float)pixel.Y) / Scale;
    }

    private float ScreenRadius(EngineSession.AuthoringCircle circle) =>
        Vector2.Distance(ProjectPoint(circle.X, circle.Y), ProjectPoint(circle.X + circle.Radius, circle.Y));
}
