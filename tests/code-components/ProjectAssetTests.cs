using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using PixieEditor.Editing;

internal static class ProjectAssetTests
{
    public static int Run()
    {
        using var fixture = Directory.CreateTempSubdirectory("PixieEditor-assets-").AsDisposable();
        var project = fixture.Info.CreateSubdirectory("Game").FullName;
        var source = fixture.Info.CreateSubdirectory("Sprite Collection").FullName;
        var nested = Directory.CreateDirectory(Path.Combine(source, "characters", "idle")).FullName;
        var image = Path.Combine(nested, "hero.PNG");
        File.WriteAllText(image, "asset bytes");
        File.WriteAllText(Path.Combine(source, "screen.ttf"), "font bytes");
        using (ZipFile.Open(Path.Combine(source, "archive.zip"), ZipArchiveMode.Create)) { }
        File.WriteAllText(Path.Combine(nested, "notes.txt"), "excluded");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(project, "build")).FullName, "generated.png"), "excluded");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(project, "legacy")).FullName, "existing.png"), "asset bytes");

        var imported = ProjectAssets.ImportFolder(project, source);
        Check(imported is { Copied: 2, Existing: 0, Skipped: 2, Problems.Length: 0 }, "recursive PNG and TTF import skips ZIPs and text");
        const string importedImage = "images/Sprite Collection/characters/idle/hero.png";
        const string importedFont = "fonts/Sprite Collection/screen.ttf";
        var catalog = ProjectAssets.Browse(project);
        Check(catalog.Problems.Length == 0 && catalog.Paths.SequenceEqual([importedFont, importedImage, "legacy/existing.png"]),
            "catalog includes existing assets and import paths and excludes generated output");
        Check(File.Exists(image) && File.ReadAllText(Path.Combine(project, importedImage)) == "asset bytes",
            "nested source remains in place and bytes are preserved");
        Check(ProjectAssets.ImportFolder(project, source) is { Copied: 0, Existing: 2, Skipped: 2, Problems.Length: 0 },
            "reimport reuses identical files");

        File.WriteAllText(image, "new asset bytes");
        Check(ProjectAssets.ImportFolder(project, source) is { Copied: 1, Existing: 1, Skipped: 2, Problems.Length: 0 } &&
            File.ReadAllText(Path.Combine(project, importedImage)) == "asset bytes" &&
            File.ReadAllText(Path.Combine(project, "images/Sprite Collection/characters/idle/hero 2.png")) == "new asset bytes",
            "changed contents are imported under a numbered name");
        Check(ProjectAssets.Import(project, Path.Combine(project, importedImage)) == (importedImage, null),
            "a direct asset reference retains its project path");

        var large = fixture.Info.CreateSubdirectory("Large");
        for (var family = 0; family < 3; ++family)
        {
            var group = large.CreateSubdirectory($"family {family}/subgroup");
            for (var sprite = 0; sprite < 100; ++sprite)
                File.WriteAllText(Path.Combine(group.FullName, $"sprite {sprite}.png"), "asset bytes");
        }
        Check(ProjectAssets.ImportFolder(project, large.FullName) is { Copied: 300, Problems.Length: 0 } &&
            ProjectAssets.Browse(project).Paths.Count(path => path.StartsWith("images/Large/", StringComparison.Ordinal)) == 300,
            "a 300-file collection preserves distinct nested paths in the catalog");
        Check(ProjectAssets.ImportFolder(project, large.FullName) is { Copied: 0, Existing: 300, Problems.Length: 0 },
            "a 300-file reimport makes no new copies");

        var atlasDirectory = Directory.CreateDirectory(Path.Combine(project, "assets")).FullName;
        var descriptor = Path.Combine(project, AtlasFixture.AtlasPath);
        var atlasText = AtlasFixture.Descriptor();
        File.WriteAllText(descriptor, atlasText);
        File.WriteAllText(Path.Combine(source, "external.atlas.json"), atlasText);
        Check(ProjectAssets.ImportFolder(project, source) is { Copied: 0, Existing: 2, Skipped: 3 },
            "recursive import leaves atlas descriptors in the source folder");
        Check(ProjectAssets.Browse(project).Paths.Count(ProjectAssets.IsAtlas) == 1 &&
            ProjectAssets.Browse(project).Paths.Contains(AtlasFixture.AtlasPath), "catalog includes project atlas descriptors");
        Check(ProjectAtlas.Read(project, AtlasFixture.AtlasPath, out var problem) is { Frames.Count: 2 } &&
            problem?.Contains("missing", StringComparison.OrdinalIgnoreCase) == true, "missing PNG reports a diagnostic and keeps the frame list");
        File.WriteAllText(Path.Combine(atlasDirectory, "sheet.png"), "image placeholder");
        Check(ProjectAtlas.Read(project, AtlasFixture.AtlasPath, out problem)?.Find(AtlasFixture.FrameId) is
            { X: 0, Width: 1, Height: 2, PivotY: 1 } && problem == null, "read a frame by stable ID with duplicate display names");
        foreach (var invalid in new[]
        {
            atlasText.Replace("assets/sheet.png", "../sheet.png", StringComparison.Ordinal),
            atlasText.Replace("assets/sheet.png", "assets/NUL.png", StringComparison.Ordinal),
            atlasText.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
            atlasText.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal),
            atlasText.Replace(AtlasFixture.OtherId, AtlasFixture.FrameId, StringComparison.Ordinal),
            AtlasFixture.Descriptor(x: 2),
        })
        {
            File.WriteAllText(descriptor, invalid);
            Check(ProjectAtlas.Read(project, AtlasFixture.AtlasPath, out problem) == null && problem != null,
                "malformed atlas is diagnosed");
        }
        File.WriteAllText(descriptor, AtlasFixture.Descriptor("Renamed", 1), new System.Text.UTF8Encoding(true));
        Check(ProjectAtlas.Read(project, AtlasFixture.AtlasPath, out problem)?.Find(AtlasFixture.FrameId)
            is { Name: "Renamed", X: 1 } && problem == null, "BOM, rename and repack retain frame identity");

        var external = fixture.Info.CreateSubdirectory("External").FullName;
        var linkedTarget = Path.Combine(project, "images", "Blocked");
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(linkedTarget);
            start.ArgumentList.Add(external);
            using var junction = Process.Start(start)!;
            junction.WaitForExit();
            Check(junction.ExitCode == 0, "Windows junction fixture created");
        }
        else
            Directory.CreateSymbolicLink(linkedTarget, external);
        try
        {
            var blockedSource = fixture.Info.CreateSubdirectory("Blocked").FullName;
            File.WriteAllText(Path.Combine(blockedSource, "blocked.png"), "asset bytes");
            Check(ProjectAssets.ImportFolder(project, blockedSource) is { Copied: 0, Problems.Length: 1 } &&
                !Directory.EnumerateFileSystemEntries(external).Any(), "linked import destination cannot write outside the project");
        }
        finally { Directory.Delete(linkedTarget); }
        return 0;
    }

    private static void Check(bool result, string label)
    {
        if (!result) throw new InvalidOperationException(label);
        Console.WriteLine($"PASS {label}");
    }

    private static Fixture AsDisposable(this DirectoryInfo folder) => new(folder);

    private sealed class Fixture(DirectoryInfo info) : IDisposable
    {
        public DirectoryInfo Info { get; } = info;
        public void Dispose() => Info.Delete(recursive: true);
    }
}

internal static class AtlasFixture
{
    internal const string AtlasPath = "assets/actors.atlas.json";
    internal const string ImagePath = "assets/sheet.png";
    internal const string FrameId = "1b681986317d465c9f01b3f303ff89fa";
    internal const string OtherId = "a9f4e02f43904c2ea9baa8af9f3c848e";

    internal static string Descriptor(string name = "Idle", int x = 0, bool includeFrame = true, int width = 2) =>
        JsonSerializer.Serialize(new
        {
            format = "pixie-sprite-atlas",
            version = 1,
            image = ImagePath,
            imageSize = new[] { width, 2 },
            frames = includeFrame ? new[] { Frame(FrameId, name, x), Frame(OtherId, name, 0) } : [Frame(OtherId, name, 0)],
        });

    private static object Frame(string id, string name, int x) => new { id, name, rect = new[] { x, 0, 1, 2 }, pivot = new[] { 0.0, 1.0 } };
}
