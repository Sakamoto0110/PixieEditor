using System.Globalization;
using System.Text.Json;

namespace PixieEditor.Editing;

internal sealed record AtlasAnimation(string Name, int Fps, IReadOnlyList<string> FrameIds)
{
    internal const int DefaultFps = 12;
    internal const int MaxFps = 120;

    internal int FrameIndex(double elapsed) =>
        (int)(Math.Floor(elapsed * Fps) % FrameIds.Count);

    internal static string NumberedPrefix(string name)
    {
        SplitNumber(name, out var prefix, out _);
        return prefix;
    }

    internal static string[] NumberedFrames(IEnumerable<ProjectAtlas.Frame> frames, string prefix) =>
        frames.Select(frame =>
        {
            var numbered = SplitNumber(frame.Name, out var stem, out var number);
            return (frame.Id, Matches: numbered && stem.Equals(prefix.Trim(), StringComparison.OrdinalIgnoreCase), Number: number);
        }).Where(frame => frame.Matches).OrderBy(frame => frame.Number).Select(frame => frame.Id).ToArray();

    private static bool SplitNumber(string name, out string prefix, out int number)
    {
        var trimmed = name.AsSpan().TrimEnd();
        var end = trimmed.Length;
        while (end > 0 && char.IsAsciiDigit(trimmed[end - 1])) --end;
        prefix = trimmed[..end].TrimEnd().ToString();
        return int.TryParse(trimmed[end..], NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    internal static IEnumerable<AtlasAnimation> Read(JsonElement root)
    {
        if (!root.TryGetProperty("animations", out var animations)) yield break;
        if (animations.ValueKind != JsonValueKind.Array)
            throw new IOException("Atlas animations must be an array.");

        foreach (var entry in animations.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString() ?? "";
            var fps = entry.GetProperty("fps").GetInt32();
            if (!entry.GetProperty("loop").GetBoolean())
                throw new IOException($"Animation '{name}' must use loop playback.");
            var ids = entry.GetProperty("frameIds").EnumerateArray()
                .Select(id => id.GetString() ?? "").ToArray();
            yield return new(name, fps, ids);
        }
    }

    internal static string? Problem(IReadOnlyList<AtlasAnimation> animations, IEnumerable<ProjectAtlas.Frame> frames)
    {
        var ids = frames.Select(frame => frame.Id).ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var animation in animations)
        {
            if (string.IsNullOrWhiteSpace(animation.Name) || animation.Name.Contains('\0') || !names.Add(animation.Name.Trim()))
                return "Enter a distinct name for each animation.";
            if (animation.Fps is < 1 or > MaxFps)
                return $"\"{animation.Name}\" needs an FPS between 1 and {MaxFps}.";
            if (animation.FrameIds.Count == 0)
                return $"Add at least one frame to \"{animation.Name}\".";
            if (animation.FrameIds.Any(id => !ids.Contains(id)))
                return $"Replace or remove missing frames from \"{animation.Name}\".";
        }
        return null;
    }
}
