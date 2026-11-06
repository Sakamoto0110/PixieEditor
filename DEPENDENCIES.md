# Dependencies

PixieEditor targets .NET 10. Its C# interop binding and native metadata come
from the explicitly selected installed PixieEngine SDK. The Editor maintains
its own product version.

PixieInspector.ImGui 0.3.0 supplies the default Inspector. The qualified
chain and lock features use the sealed 0.3.0-pixie021.3 core and view pair.
Hexa.NET.GLFW 1.0.5 supplies the managed bindings. Windows x64 builds and
publishes carry the separately built GLFW 3.5.1 DLL and its license from
eng/native/win-x64. The DLL is pinned in `src/Glfw.targets`.
