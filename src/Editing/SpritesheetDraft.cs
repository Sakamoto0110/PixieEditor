using System.Security.Cryptography;
using System.Text.Json;
using static PixieEditor.Editing.SpritesheetPacking;

namespace PixieEditor.Editing;

internal sealed class SpritesheetDraft
{
    internal sealed record Entry(ProjectAtlas.Frame Frame, string? Source = null);
    internal string Path { get; set; } = "assets/sprites.atlas.json";
    internal bool PacksImages { get; }
    internal string? Sheet { get; set; }
    internal int Padding { get; set; } = 2;
    internal int Extrusion { get; set; } = 4;
    internal List<Entry> Entries { get; } = [];
    internal List<AtlasAnimation> Animations { get; } = [];
    private byte[]? originalHash;

    internal SpritesheetDraft(bool packsImages) => PacksImages = packsImages;

    internal static SpritesheetDraft Open(string project, string path)
    {
        var file = ProjectAssets.AtlasAssetFile(project, path, ".atlas.json");
        using var input = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var hash = SHA256.HashData(input);
        input.Position = 0;
        using var document = ProjectAtlas.ReadDocument(input);
        var root = document.RootElement;
        var atlas = ProjectAtlas.Parse(project, root);

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var draft = new SpritesheetDraft(root.TryGetProperty("pixieEditorPacking", out var recipe))
        {
            Path = path,
            Sheet = atlas.Image,
            originalHash = hash,
        };
        if (draft.PacksImages)
        {
            if (recipe.GetProperty("version").GetInt32() != 1)
                throw new IOException("Unsupported PixieEditor packing recipe.");
            draft.Padding = recipe.GetProperty("padding").GetInt32();
            draft.Extrusion = recipe.GetProperty("extrusion").GetInt32();
            foreach (var source in recipe.GetProperty("sources").EnumerateObject())
                sources.Add(source.Name, source.Value.GetString() ?? throw new IOException("Missing packing source."));
            if (sources.Count != atlas.Frames.Count || atlas.Frames.Any(frame => !sources.ContainsKey(frame.Id)))
                throw new IOException("The packing recipe needs a source for every frame.");
            if (sources.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Count)
                throw new IOException("The packing recipe uses the same PNG more than once.");
        }
        foreach (var frame in atlas.Frames)
            draft.Entries.Add(new(frame, sources.GetValueOrDefault(frame.Id)));
        draft.Animations.AddRange(atlas.Animations);
        return draft;
    }

    internal bool Existing => originalHash != null;

    internal void AddSource(string path)
    {
        if (!ProjectAssets.IsAtlasAssetPath(path, ".png") ||
            StringComparer.OrdinalIgnoreCase.Equals(Sheet, path))
            throw new IOException("Choose a project PNG other than this atlas's generated image.");
        if (Entries.Any(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.Source, path)))
            return;
        RequireRoom(1);
        Entries.Add(new(NewFrame(System.IO.Path.GetFileNameWithoutExtension(path), default), path));
    }

    internal void AddSlices(IReadOnlyList<Rect> rectangles)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(Sheet ?? throw new IOException("Choose a PNG sheet."));
        var existing = Entries.Select(entry => Rect.Of(entry.Frame)).ToHashSet();
        var additions = rectangles.Where(existing.Add).ToArray();
        RequireRoom(additions.Length);
        foreach (var rect in additions)
            Entries.Add(new(NewFrame($"{stem} {Entries.Count + 1}", rect)));
    }

    private void RequireRoom(int count)
    {
        if (Entries.Count + count > MaxFrames)
            throw new IOException($"The builder supports up to {MaxFrames} frames per atlas.");
    }

    private static ProjectAtlas.Frame NewFrame(string name, Rect rect) =>
        new(Guid.NewGuid().ToString("N"), name, rect.X, rect.Y, rect.Width, rect.Height, 0.5, 0.5);

    internal Packed PreviewPacking(string project)
    {
        if (!Existing && Entries.Count == 0) throw new IOException("Add at least one PNG to pack.");
        return Pack(project, Entries, Padding, Extrusion);
    }

    internal ProjectAtlas Save(string project)
    {
        if (AtlasAnimation.Problem(Animations, Entries.Select(entry => entry.Frame)) is { } animationProblem)
            throw new IOException(animationProblem);
        var descriptor = ProjectAssets.AtlasAssetFile(project, Path, ".atlas.json");
        if (!ProjectAssets.IsCatalogPath(Path))
            throw new IOException("Save the atlas outside the project's metadata and build folders.");
        var parent = System.IO.Path.GetDirectoryName(descriptor)!;
        Directory.CreateDirectory(parent);
        var lockFile = ProjectAssets.AtlasAssetFile(project, Path + ".pixie-lock", ".pixie-lock");
        using var writerLock = new FileStream(lockFile, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
        VerifyOriginal(descriptor);
        var pendingDescriptor = System.IO.Path.Combine(parent, $".pixie-atlas-{Guid.NewGuid():N}.tmp");
        var pendingImage = System.IO.Path.Combine(parent, $".pixie-atlas-{Guid.NewGuid():N}.tmp");
        try
        {
            ProjectAtlas atlas;
            string? imageFile = null;
            if (PacksImages)
            {
                var packed = PreviewPacking(project);
                using var stream = new FileStream(pendingImage, FileMode.CreateNew, FileAccess.ReadWrite);
                packed.Image.Write(stream);
                stream.Flush(flushToDisk: true);
                stream.Position = 0;
                var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                var separator = Path.LastIndexOf('/');
                var image = $"{Path[..(separator + 1)]}atlas-{hash}.png";
                imageFile = ProjectAssets.AtlasAssetFile(project, image, ".png");
                atlas = new(image, packed.Image.Width, packed.Image.Height, packed.Frames);
            }
            else
            {
                var sheet = Sheet ?? throw new IOException("Choose a PNG sheet.");
                var image = ReadPng(ProjectAssets.AtlasAssetFile(project, sheet, ".png"));
                atlas = new(sheet, image.Width, image.Height, Entries.Select(entry => entry.Frame).ToArray());
            }

            byte[] hashAfterSave;
            using (var stream = new FileStream(pendingDescriptor, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                WriteDescriptor(stream, atlas);
                stream.Flush(flushToDisk: true);
                stream.Position = 0;
                using (var document = ProjectAtlas.ReadDocument(stream))
                    _ = ProjectAtlas.Parse(project, document.RootElement);
                stream.Position = 0;
                hashAfterSave = SHA256.HashData(stream);
            }

            // A descriptor reader can always open its image, even if it read the old
            // descriptor just before publication. Generated images are immutable.
            if (imageFile != null)
                PublishImage(pendingImage, imageFile);
            VerifyOriginal(descriptor);
            File.Move(pendingDescriptor, descriptor, overwrite: Existing);
            originalHash = hashAfterSave;
            Sheet = atlas.Image;
            for (var index = 0; index < Entries.Count; ++index)
                Entries[index] = Entries[index] with { Frame = atlas.Frames[index] };
            ProjectAssets.NotifyChanged();
            return atlas;
        }
        finally
        {
            DeletePending(pendingDescriptor);
            DeletePending(pendingImage);
        }
    }

    private void VerifyOriginal(string descriptor)
    {
        if (!Existing)
        {
            if (File.Exists(descriptor) || Directory.Exists(descriptor))
                throw new IOException("That atlas path already exists. Open it from the Asset Manager to edit it.");
            return;
        }

        using var input = File.Open(descriptor, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (!SHA256.HashData(input).AsSpan().SequenceEqual(originalHash))
            throw new IOException("The atlas changed on disk. Reopen it before saving.");
    }

    private static void PublishImage(string pending, string destination)
    {
        try { File.Move(pending, destination); }
        catch (IOException) when (File.Exists(destination))
        {
            using var expected = File.OpenRead(pending);
            using var existing = File.OpenRead(destination);
            if (!SHA256.HashData(expected).AsSpan().SequenceEqual(SHA256.HashData(existing)))
                throw new IOException($"The generated PNG path is occupied by different content: {destination}");
        }
    }

    private static void DeletePending(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not remove atlas staging file {path}: {error.Message}");
        }
    }

    private void WriteDescriptor(Stream output, ProjectAtlas atlas)
    {
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("format", "pixie-sprite-atlas");
        writer.WriteNumber("version", 1);
        writer.WriteString("image", atlas.Image);
        writer.WritePropertyName("imageSize");
        JsonSerializer.Serialize(writer, new[] { atlas.Width, atlas.Height });
        writer.WritePropertyName("frames");
        JsonSerializer.Serialize(writer, atlas.Frames.Select(frame => new
        {
            id = frame.Id,
            name = frame.Name,
            rect = new[] { frame.X, frame.Y, frame.Width, frame.Height },
            pivot = new[] { frame.PivotX, frame.PivotY },
        }));
        if (Animations.Count != 0)
        {
            writer.WritePropertyName("animations");
            JsonSerializer.Serialize(writer, Animations.Select(animation => new
            {
                name = animation.Name.Trim(),
                fps = animation.Fps,
                loop = true,
                frameIds = animation.FrameIds,
            }));
        }
        if (PacksImages)
        {
            writer.WritePropertyName("pixieEditorPacking");
            JsonSerializer.Serialize(writer, new
            {
                version = 1,
                padding = Padding,
                extrusion = Extrusion,
                sources = Entries.ToDictionary(entry => entry.Frame.Id, entry => entry.Source),
            });
        }
        writer.WriteEndObject();
    }
}
