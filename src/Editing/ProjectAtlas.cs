using System.Text.Json;

namespace PixieEditor.Editing;

// Reads the Engine's v1 sprite atlas contract for browsing. References keep the authored ID.
internal sealed class ProjectAtlas(string image, int width, int height, IReadOnlyList<ProjectAtlas.Frame> frames)
{
    internal sealed record Frame(string Id, string Name, int X, int Y, int Width, int Height, double PivotX, double PivotY);
    internal readonly record struct Reference(string Atlas, string FrameId)
    {
        public bool IsSet => Atlas.Length != 0 || FrameId.Length != 0;
    }

    public string Image { get; } = image;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public IReadOnlyList<Frame> Frames { get; } = frames;
    public Frame? Find(string id) => Frames.FirstOrDefault(frame => frame.Id == id);

    public static ProjectAtlas? Read(string project, string path, out string? problem)
    {
        try
        {
            using var input = File.Open(ProjectAssets.AtlasAssetFile(project, path, ".atlas.json"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // Accept a UTF-8 BOM, as the native descriptor parser does.
            Span<byte> bom = stackalloc byte[3];
            if (input.ReadAtLeast(bom, bom.Length, false) != bom.Length ||
                !bom.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
                input.Position = 0;
            using var document = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = 9 });
            var root = document.RootElement;
            CheckMembers(root);
            if (Text(root, "format") != "pixie-sprite-atlas" || Int(root, "version") != 1)
                throw new IOException("Expected a version 1 pixie-sprite-atlas descriptor.");
            var image = Text(root, "image");
            var imageFile = ProjectAssets.AtlasAssetFile(project, image, ".png");
            var size = Array(root, "imageSize", 2);
            var width = Int(size[0]);
            var height = Int(size[1]);
            if (width == 0 || height == 0) throw new IOException("Image dimensions must be positive.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var frames = new List<Frame>();
            foreach (var entry in Array(root, "frames").EnumerateArray())
            {
                var id = Text(entry, "id");
                if (id.Length != 32 || !id.Any(ch => ch != '0') || id.Any(ch => !"0123456789abcdef".Contains(ch)) || !ids.Add(id))
                    throw new IOException($"Invalid or duplicate frame ID: {id}");
                var name = Text(entry, "name");
                var rect = Array(entry, "rect", 4);
                var x = Int(rect[0]);
                var y = Int(rect[1]);
                var w = Int(rect[2]);
                var h = Int(rect[3]);
                if (name.Length == 0 || w == 0 || h == 0 || w > width - x || h > height - y)
                    throw new IOException($"Frame {id} needs a name and a nonempty rectangle inside the image.");
                var pivot = Array(entry, "pivot", 2);
                frames.Add(new(id, name, x, y, w, h, Normalized(pivot[0]), Normalized(pivot[1])));
            }
            problem = File.Exists(imageFile) ? null : $"Atlas PNG is missing: {image}";
            return new(image, width, height, frames);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or
                                      ArgumentException or InvalidOperationException)
        {
            problem = e.Message;
            return null;
        }
    }

    private static void CheckMembers(JsonElement value, int depth = 0)
    {
        if (depth > 8) throw new IOException("Atlas nesting exceeds eight levels.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
            {
                if (!keys.Add(field.Name)) throw new IOException($"Duplicate atlas member: {field.Name}");
                CheckMembers(field.Value, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) CheckMembers(item, depth + 1);
        }
    }

    private static JsonElement Member(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var member)
            ? member : throw new IOException($"Missing atlas member: {name}");

    private static string Text(JsonElement value, string name) =>
        Member(value, name).GetString() ?? throw new IOException($"Expected text in {name}.");

    private static int Int(JsonElement value, string name) => Int(Member(value, name));

    private static int Int(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) && result >= 0
            ? result : throw new IOException("Expected a nonnegative 32-bit integer in the atlas.");

    private static JsonElement Array(JsonElement value, string name, int? count = null)
    {
        var array = Member(value, name);
        if (array.ValueKind != JsonValueKind.Array || (count != null && array.GetArrayLength() != count))
            throw new IOException($"Invalid atlas array: {name}.");
        return array;
    }

    private static double Normalized(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result) && result is >= 0 and <= 1
            ? result : throw new IOException("Expected an atlas pivot coordinate between 0 and 1.");
}
