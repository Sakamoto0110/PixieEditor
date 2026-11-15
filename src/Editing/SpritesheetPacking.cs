using StbImageSharp;

namespace PixieEditor.Editing;

internal static class SpritesheetPacking
{
    internal const int MaxDimension = 8192;
    internal const int MaxFrames = 16384;
    internal readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        internal bool Fits(int width, int height) =>
            X >= 0 && Y >= 0 && Width > 0 && Height > 0 && Width <= width - X && Height <= height - Y;

        internal static Rect Of(ProjectAtlas.Frame frame) => new(frame.X, frame.Y, frame.Width, frame.Height);
    }

    internal sealed record Image(int Width, int Height, byte[] Pixels)
    {
        internal void Write(Stream destination) =>
            new StbImageWriteSharp.ImageWriter().WritePng(Pixels, Width, Height,
                StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, destination);
    }

    internal sealed record Packed(Image Image, IReadOnlyList<ProjectAtlas.Frame> Frames);
    private sealed record Source(ProjectAtlas.Frame Frame, string Path, Image Image);

    internal static Image ReadPng(string path, long remainingPixels = (long)MaxDimension * MaxDimension)
    {
        using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        Span<byte> signature = stackalloc byte[8];
        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (input.ReadAtLeast(signature, signature.Length, false) != signature.Length || !signature.SequenceEqual(png))
            throw new IOException($"Expected a PNG image: {path}");
        input.Position = 0;
        var info = ImageInfo.FromStream(input);
        if (info is not { Width: > 0 and <= MaxDimension, Height: > 0 and <= MaxDimension } ||
            (long)info.Value.Width * info.Value.Height > remainingPixels)
            throw new IOException($"Invalid PNG dimensions or image budget exceeded: {path}");
        input.Position = 0;
        try
        {
            var decoded = ImageResult.FromStream(input, ColorComponents.RedGreenBlueAlpha);
            return new(decoded.Width, decoded.Height, decoded.Data);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new IOException($"Could not decode PNG {path}: {error.Message}", error);
        }
    }

    internal static Rect[] Grid(int width, int height, int cellWidth, int cellHeight,
        int offsetX, int offsetY, int gapX, int gapY)
    {
        if (cellWidth <= 0 || cellHeight <= 0 || offsetX < 0 || offsetY < 0 || gapX < 0 || gapY < 0)
            throw new IOException("Grid cells must be positive. Offsets and gaps must be nonnegative.");
        if (cellWidth > width - offsetX || cellHeight > height - offsetY)
            throw new IOException("The grid has no complete cell inside the sheet.");

        var stepX = (long)cellWidth + gapX;
        var stepY = (long)cellHeight + gapY;
        var columns = 1 + (width - offsetX - cellWidth) / stepX;
        var rows = 1 + (height - offsetY - cellHeight) / stepY;
        if (rows * columns > MaxFrames)
            throw new IOException($"The grid exceeds {MaxFrames} cells.");
        var cells = new List<Rect>();
        for (var row = 0; row < rows; ++row)
            for (var column = 0; column < columns; ++column)
                cells.Add(new((int)(offsetX + column * stepX), (int)(offsetY + row * stepY), cellWidth, cellHeight));
        return cells.ToArray();
    }

    internal static Packed Pack(string project, IReadOnlyList<SpritesheetDraft.Entry> entries, int padding, int extrusion)
    {
        if (padding is < 0 or > 32 || extrusion is < 1 or > 32)
            throw new IOException("Padding must be 0–32 pixels and extrusion must be 1–32 pixels.");
        if (entries.Count == 0)
            return new(new(1, 1, new byte[4]), []);

        var border = padding + extrusion;
        var sources = new List<Source>(entries.Count);
        var remaining = (long)MaxDimension * MaxDimension;
        foreach (var entry in entries)
        {
            var path = entry.Source ?? throw new IOException($"Missing source for frame {entry.Frame.Id}.");
            var image = ReadPng(ProjectAssets.AtlasAssetFile(project, path, ".png"), remaining);
            remaining -= (long)image.Width * image.Height;
            sources.Add(new(entry.Frame, path, image));
        }
        var ordered = sources.OrderByDescending(source => source.Image.Height)
            .ThenByDescending(source => source.Image.Width)
            .ThenBy(source => source.Path, StringComparer.Ordinal).ToArray();
        var widest = ordered.Max(source => source.Image.Width) + 2 * border;
        var area = ordered.Sum(source => (long)(source.Image.Width + 2 * border) * (source.Image.Height + 2 * border));
        var width = 1;
        while (width < MaxDimension && (width < widest || (long)width * width < area))
            width *= 2;

        var placements = new ProjectAtlas.Frame[ordered.Length];
        for (; width <= MaxDimension && width >= widest; width *= 2)
        {
            var height = Arrange(ordered, placements, width, border);
            if (height > MaxDimension)
                continue;
            var pixels = new byte[width * height * 4];
            for (var index = 0; index < ordered.Length; ++index)
                CopyWithExtrusion(ordered[index].Image, pixels, width, placements[index], extrusion);
            var byId = placements.ToDictionary(frame => frame.Id);
            return new(new(width, height, pixels), entries.Select(entry => byId[entry.Frame.Id]).ToArray());
        }
        throw new IOException($"The sprites do not fit in an atlas up to {MaxDimension} × {MaxDimension} pixels.");
    }

    private static int Arrange(Source[] sources, ProjectAtlas.Frame[] placements, int width, int border)
    {
        var x = 0;
        var y = 0;
        var rowHeight = 0;
        for (var index = 0; index < sources.Length; ++index)
        {
            var (frame, _, image) = sources[index];
            var cellWidth = image.Width + 2 * border;
            var cellHeight = image.Height + 2 * border;
            if (x + cellWidth > width)
            {
                y += rowHeight;
                x = rowHeight = 0;
            }
            rowHeight = Math.Max(rowHeight, cellHeight);
            if (y + rowHeight > MaxDimension)
                return y + rowHeight;
            placements[index] = frame with { X = x + border, Y = y + border, Width = image.Width, Height = image.Height };
            x += cellWidth;
        }
        return y + rowHeight;
    }

    private static void CopyWithExtrusion(Image source, byte[] destination, int stride,
        ProjectAtlas.Frame frame, int extrusion)
    {
        for (var y = -extrusion; y < source.Height + extrusion; ++y)
        {
            var sourceY = Math.Clamp(y, 0, source.Height - 1);
            var row = source.Pixels.AsSpan(sourceY * source.Width * 4, source.Width * 4);
            var left = ((frame.Y + y) * stride + frame.X) * 4;
            row.CopyTo(destination.AsSpan(left));
            for (var edge = 1; edge <= extrusion; ++edge)
            {
                row[..4].CopyTo(destination.AsSpan(left - edge * 4, 4));
                row[^4..].CopyTo(destination.AsSpan(left + (source.Width - 1 + edge) * 4, 4));
            }
        }
    }
}
