using System.Text;
using Hexa.NET.ImGui;
using PixieEditor.Editing;

namespace PixieEditor.Ui;

internal static unsafe class AtlasFrameDrag
{
    private const string Type = "PIXIE_ATLAS_FRAME";

    internal static void Source(string path, ProjectAtlas.Frame frame)
    {
        if (!ImGui.BeginDragDropSource(ImGuiDragDropFlags.None)) return;
        var payload = Encoding.UTF8.GetBytes(frame.Id + path);
        fixed (byte* bytes = payload)
            ImGui.SetDragDropPayload(Type, bytes, (nuint)payload.Length, ImGuiCond.Once);
        ImGui.TextUnformatted(frame.Name);
        ImGui.EndDragDropSource();
    }

    internal static ProjectAtlas.Reference? Target()
    {
        if (!ImGui.BeginDragDropTarget()) return null;
        try
        {
            var payload = ImGui.AcceptDragDropPayload(Type);
            if (payload.Handle == null || !payload.Delivery || payload.DataSize <= 32)
                return null;
            var text = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(payload.Data, payload.DataSize));
            return new(text[32..], text[..32]);
        }
        finally { ImGui.EndDragDropTarget(); }
    }
}
