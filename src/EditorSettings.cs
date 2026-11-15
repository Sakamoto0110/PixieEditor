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

    internal sealed record ProjectSdk(string Root, string ManifestSha256, string Profile, string Configuration)
    {
        internal EngineSdk Load(string folder)
        {
            if (!Path.IsPathFullyQualified(Root) || string.IsNullOrWhiteSpace(ManifestSha256))
                throw new EngineException("The project's saved SDK needs an absolute path and a catalog hash.");
            var sdk = EngineSdk.Load(Root, Profile, Configuration, "project-settings", expectedManifestSha256: ManifestSha256);
            if (!SdkRequirements.Validate(folder, sdk))
                throw new EngineException("The project no longer records requirements for its saved SDK.");
            return sdk;
        }
    }

    private string ProjectSdksPath => Path.Combine(Path.GetDirectoryName(StoragePath ?? FilePath)!, "project-sdks.json");

    private static string ProjectKey(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private Dictionary<string, ProjectSdk> ReadProjectSdks()
    {
        var path = ProjectSdksPath;
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ProjectSdk>>(File.ReadAllText(path))
                ?? throw new JsonException("Expected a map of project folders to SDKs.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new EngineException($"The project SDK settings could not be read: {error.Message}");
        }
    }

    internal ProjectSdk? FindProjectSdk(string folder)
    {
        var full = ProjectKey(folder);
        return ReadProjectSdks().FirstOrDefault(binding => string.Equals(binding.Key, full, PathComparison)).Value;
    }

    internal void RememberProjectSdk(string folder, EngineSdk sdk)
    {
        try
        {
            if (!SdkRequirements.Validate(folder, sdk)) return;
            var path = ProjectSdksPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var bindings = ReadProjectSdks();
            var full = ProjectKey(folder);
            var existing = bindings.Keys.FirstOrDefault(key => string.Equals(key, full, PathComparison));
            var binding = new ProjectSdk(sdk.Root, sdk.ManifestSha256, sdk.Profile, sdk.Configuration);
            if (existing != null && bindings[existing] == binding) return;
            bindings[existing ?? full] = binding;
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(bindings, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, path, overwrite: true);
            }
            finally { File.Delete(temporary); }
        }
        catch (Exception error) when (error is EngineException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[warning] The project's SDK could not be remembered: {error.Message}");
        }
    }

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
