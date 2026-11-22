using System.Text.Json;

namespace PixieEditor.Native;

// Portable requirements constrain a chosen installation; they never discover one.
internal static class SdkRequirements
{
    internal const string RelativePath = ".pixie-code/sdk.json";
    private static readonly HashSet<string> Allowed = ["schemaVersion", "sdkId", "projectContract", "exactSdk", "profile", "platform", "architecture", "compilerVersion", "cpuFeatures", "requiredCapabilities"];

    internal static bool Validate(string folder, EngineSdk sdk)
    {
        var path = ProjectCode.CheckedPath(folder, RelativePath);
        if (!File.Exists(path)) return false;
        try
        {
            using var document = EngineSdk.Parse(EngineSdk.ReadBytes(path, 64 * 1024));
            var requirement = document.RootElement;
            EngineSdk.Schema(requirement); EngineSdk.HashText(requirement, "sdkId");
            if (requirement.EnumerateObject().Any(x => !Allowed.Contains(x.Name)))
                throw new EngineException("Portable SDK requirements cannot contain installation paths or unknown settings.");
            if (!AcceptsProject(requirement, sdk))
                throw new EngineException("Project SDK compatibility is unverified or excluded by exactSdk. " +
                    "Choose the original SDK or explicitly adopt a qualified successor.");
            foreach (var field in new[] { "profile", "platform", "architecture", "compilerVersion" })
                if (!JsonElement.DeepEquals(requirement.GetProperty(field), sdk.Identity.GetProperty(field)))
                    throw new EngineException($"This project requires a different Engine SDK ({field}). " +
                        $"Required: {requirement.GetProperty(field).GetString()}; selected: {sdk.Identity.GetProperty(field).GetString()} " +
                        $"({sdk.Summary}). Choose a matching SDK for this project.");
            var requestedCpu = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in requirement.GetProperty("cpuFeatures").EnumerateArray())
                if (entry.GetString() is not { } feature || !requestedCpu.Add(feature))
                    throw new EngineException("Portable SDK requirements contain a malformed or duplicate CPU feature.");
            if (!requestedCpu.SetEquals(sdk.Identity.GetProperty("cpuFeatures").EnumerateArray().Select(x => x.GetString()!)))
                throw new EngineException("This project requires a different Engine SDK (cpuFeatures). Choose a matching SDK; adoption must be explicit.");
            if (requirement.TryGetProperty("requiredCapabilities", out var capabilities))
                foreach (var item in capabilities.EnumerateArray())
                {
                    var name = item.GetString();
                    if (name is not ("moduleLoading" or "nativeAutomation" or "candidateAuthoring") ||
                        !sdk.Identity.GetProperty("capabilities").GetProperty(name).GetBoolean())
                        throw new EngineException("The selected SDK does not provide a required project capability.");
                }
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new EngineException($"Cannot read this project's SDK requirements: {error.Message}"); }
    }

    // The catalog owns directional source compatibility; native build identity stays exact.
    internal static bool AcceptsProject(JsonElement requirement, EngineSdk sdk)
    {
        var exact = requirement.TryGetProperty("exactSdk", out var locked) && locked.GetBoolean();
        var original = EngineSdk.Text(requirement, "sdkId");
        if (original == sdk.Id) return true;
        if (exact || sdk.ProjectCompatibility is not { } policy) return false;
        if (policy.GetProperty("schemaVersion").GetInt32() != 1)
            throw new EngineException("Unsupported SDK project compatibility policy.");
        string? contract;
        if (requirement.TryGetProperty("projectContract", out var declared)) contract = declared.GetString();
        else if (policy.GetProperty("legacyRequirements").TryGetProperty(original, out var legacy)) contract = legacy.GetString();
        else return false;
        if (string.IsNullOrWhiteSpace(contract)) return false;
        return policy.GetProperty("supportedContracts").EnumerateArray().Any(x => x.GetString() == contract);
    }

    // The caller must explicitly request adoption; unknown or existing requirements are never overwritten.
    internal static void Adopt(string folder, EngineSdk sdk)
    {
        if (!Directory.Exists(folder)) throw new EngineException("Open or create the project before recording its SDK requirement.");
        var path = ProjectCode.CheckedPath(folder, RelativePath);
        if (File.Exists(path)) throw new EngineException("Existing SDK requirements were preserved. Choose their matching SDK or migrate them explicitly.");
        var data = new Dictionary<string, JsonElement>();
        foreach (var field in new[] { "schemaVersion", "sdkId", "profile", "platform", "architecture", "compilerVersion", "cpuFeatures" })
            data[field] = sdk.Identity.GetProperty(field);
        if (sdk.ProjectCompatibility is { } compatibility)
            data["projectContract"] = compatibility.GetProperty("currentContract");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true }));
                file.Flush(true);
            }
            File.Move(temporary, path, overwrite: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new EngineException($"Could not record this project's SDK requirement: {error.Message}"); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Console.Error.WriteLine($"[warning] SDK adoption temporary file remains at {temporary}: {error.Message}"); }
        }
    }
}
