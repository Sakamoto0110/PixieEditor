using System.Diagnostics;
using System.Runtime.InteropServices;
using HexaGen.Runtime;

namespace PixieEditor.Native;

// Hexa's generated bindings resolve GLFW once per process. The Editor backend and the
// Engine runtime also import glfw3.dll, so the process must retain one admitted provider.
internal sealed class WindowsGlfw
{
    internal static bool Applies => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    private readonly string PathToDll;
    private readonly string Sha256;

    internal WindowsGlfw(EngineSdk? sdk)
    {
        if (sdk?.Glfw is { } glfw)
        {
            PathToDll = EngineSdk.CheckedPath(sdk.Root, glfw.Path);
            Sha256 = glfw.Sha256;
            if (EngineSdk.HashFile(PathToDll) != Sha256)
                throw new EngineException("The selected SDK's GLFW changed after validation.");
        }
        else
        {
            PathToDll = Path.Combine(AppContext.BaseDirectory, "glfw3.dll");
            if (!File.Exists(PathToDll))
                PathToDll = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "glfw3.dll");
            Sha256 = EngineSdk.HashFile(PathToDll);
        }

        LibraryLoader.ResolvePath += ResolvePath;
    }

    private bool ResolvePath(string name, out string? path)
    {
        path = string.Equals(name, "glfw3.dll", StringComparison.OrdinalIgnoreCase) ? PathToDll : null;
        return path != null;
    }

    internal bool Matches(EngineSdk sdk) => sdk.Glfw?.Sha256 == Sha256;

    internal unsafe void VerifyCurrentContext(nint expected)
    {
        using var process = Process.GetCurrentProcess();
        var providers = process.Modules.Cast<ProcessModule>()
            .Where(module => string.Equals(module.ModuleName, "glfw3.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (providers.Length != 1 || !string.Equals(providers[0].FileName, PathToDll, StringComparison.OrdinalIgnoreCase) ||
            EngineSdk.HashFile(providers[0].FileName) != Sha256)
            throw new EngineException("The Editor process is not using the admitted Windows x64 GLFW provider.");

        var getCurrentContext = (delegate* unmanaged[Cdecl]<nint>)NativeLibrary.GetExport(providers[0].BaseAddress, "glfwGetCurrentContext");
        if (getCurrentContext() != expected)
            throw new EngineException("The admitted GLFW provider does not own the Editor's current OpenGL context.");
        Console.Error.WriteLine($"[info] GLFW provider: {providers[0].FileName} SHA256={Sha256}");
    }
}
