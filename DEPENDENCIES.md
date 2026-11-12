# Dependencies

PixieEditor targets .NET 10. Its C# interop binding and native metadata come
from the explicitly selected installed PixieEngine SDK. The Editor maintains
its own product version.

PixieInspector.ImGui 0.3.0 supplies the default Inspector. The qualified
chain and lock features use the sealed 0.3.0-pixie021.3 core and view pair.
Hexa.NET.GLFW 1.0.5 supplies the managed bindings. Windows x64 builds carry
the GLFW DLL and license from the pinned build SDK and verify the DLL against
the selected profile and configuration. A public Editor package carries
the SDK's authoring/Release DLL. At launch, the Editor uses the selected SDK
profile's GLFW. The packaged DLL supports the start screen before an SDK is
selected. Selecting an SDK with different GLFW bytes restarts the Editor
before it opens or creates a project.
