using System.Text.Json;
using System.Text.Json.Serialization;
using PixieEditor.Native;

namespace PixieEditor;

// What the editor remembers for this user on this machine: the recent projects and where the last
// one was made. It lives in the user's application data, never in a project, so a project folder
// carries nothing of the machine it was edited on.
internal sealed class EditorSettings
{
    private const int MaxRecent = 10;

    public List<string> RecentProjects { get; set; } = [];
    public string? LastLocation { get; set; }
    public string? SdkRoot { get; set; }
    public string? SdkManifestSha256 { get; set; }
    public string SdkProfile { get; set; } = "authoring";
    public string NativeConfiguration { get; set; } = "Release";
    [JsonIgnore] internal string? StoragePath { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixieEditor", "settings.json");

    internal static string LayoutPath => Path.Combine(Path.GetDirectoryName(FilePath)!, "imgui.ini");

    internal static bool HasProjectLayout(string path)
    {
        try
        {
            // ImGui can write an ini file on the start screen before any project panels exist.
            return File.ReadLines(path).Contains("[Window][Viewport]", StringComparer.Ordinal);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true; // Keep an existing layout if it cannot be inspected.
        }
    }

    public static EditorSettings Load(string? storagePath = null)
    {
        try
        {
            if (File.Exists(storagePath ?? FilePath))
            {
                var settings = JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(storagePath ?? FilePath)) ?? new EditorSettings();
                settings.StoragePath = storagePath;
                return settings;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"[warning] The editor's settings could not be read: {e.Message}");
        }

        return new EditorSettings { StoragePath = storagePath };
    }

    internal void ConfirmSdk(EngineSdk sdk)
    {
        sdk.VerifyUnchanged();
        SdkRoot = sdk.Root;
        SdkProfile = sdk.Profile;
        NativeConfiguration = sdk.Configuration;
        SdkManifestSha256 = sdk.ManifestSha256;
        Save();
    }

    internal EngineSdk? RecallSdk()
    {
        if (string.IsNullOrWhiteSpace(SdkRoot)) return null;
        if (!Path.IsPathFullyQualified(SdkRoot))
            throw new EngineException("The saved SDK path must be absolute.");
        if (SdkManifestSha256 == null)
            throw new EngineException("Confirm the saved SDK once to record its catalog hash.");
        return EngineSdk.Load(SdkRoot, SdkProfile, NativeConfiguration, "settings",
            expectedManifestSha256: SdkManifestSha256);
    }

    // Puts the project first among the recent ones, and its parent as the place to make the next one.
    public void Remember(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        RecentProjects.RemoveAll(p => string.Equals(p, full, PathComparison));
        RecentProjects.Insert(0, full);
        if (RecentProjects.Count > MaxRecent)
            RecentProjects.RemoveRange(MaxRecent, RecentProjects.Count - MaxRecent);
        LastLocation = Path.GetDirectoryName(full) ?? LastLocation;
        Save();
    }

    public void Forget(string folder)
    {
        RecentProjects.RemoveAll(p => string.Equals(p, folder, PathComparison));
        Save();
    }

    private void Save()
    {
        try
        {
            var path = StoragePath ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[warning] The editor's settings could not be saved: {e.Message}");
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
