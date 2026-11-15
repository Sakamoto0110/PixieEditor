using PixieEditor.Editing;

internal static class AtlasAnimationTests
{
    internal static int Run(string directory)
    {
        var project = Directory.CreateDirectory(Path.Combine(directory, Guid.NewGuid().ToString("N"))).FullName;
        const string sheet = "sheet.png";
        const string path = "walk.atlas.json";
        using (var png = File.Create(Path.Combine(project, sheet)))
            new SpritesheetPacking.Image(3, 1, new byte[12]).Write(png);

        var draft = new SpritesheetDraft(packsImages: false) { Sheet = sheet, Path = path };
        draft.AddSlices([new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1)]);
        var names = new[] { "Walk 10", "Walk 2", "Walk 1" };
        for (var i = 0; i < names.Length; ++i)
        {
            var entry = draft.Entries[i];
            draft.Entries[i] = entry with { Frame = entry.Frame with { Name = names[i] } };
        }

        var frames = draft.Entries.Select(entry => entry.Frame).ToArray();
        var ids = AtlasAnimation.NumberedFrames(frames, "Walk");
        Check(ids.SequenceEqual(frames.Reverse().Select(frame => frame.Id)), "numbered frames use numeric order");
        Check(AtlasAnimation.NumberedPrefix("Walk 10") == "Walk" &&
            AtlasAnimation.NumberedFrames(frames, "Unused").Length == 0, "arbitrary prefixes use names only to build the initial sequence");

        var clip = new AtlasAnimation("Walking", 8, ids);
        Check(clip.FrameIndex(0) == 0 && clip.FrameIndex(0.125) == 1 &&
            clip.FrameIndex(0.25) == 2 && clip.FrameIndex(0.375) == 0,
            "preview advances at authored FPS and wraps at the end");
        draft.Animations.Add(clip);
        draft.Save(project);
        var reopened = SpritesheetDraft.Open(project, path);
        var saved = reopened.Animations.Single();
        Check(saved.Name == clip.Name && saved.Fps == clip.Fps && saved.FrameIds.SequenceEqual(ids),
            "save/reopen retains animation name, FPS, and ordered frame IDs");

        var renamed = reopened.Entries[0];
        reopened.Entries[0] = renamed with { Frame = renamed.Frame with { Name = "Renamed", X = 1 } };
        reopened.Save(project);
        reopened = SpritesheetDraft.Open(project, path);
        Check(reopened.Animations.Single().FrameIds.SequenceEqual(ids), "a frame rename and rectangle change preserve the animation");

        var before = File.ReadAllBytes(Path.Combine(project, path));
        reopened.Entries.RemoveAt(0);
        try
        {
            reopened.Save(project);
            throw new InvalidOperationException("Saving an animation with a removed frame should fail.");
        }
        catch (IOException) { }
        Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(project, path))),
            "invalid animation leaves the previous atlas intact");
        Check(ProjectAtlas.Read(project, path, out var problem)?.Find(ids[0]) != null && problem == null,
            "the existing v1 frame reader accepts the optional animation metadata");
        return 0;
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        Console.WriteLine($"PASS {description}");
    }
}
