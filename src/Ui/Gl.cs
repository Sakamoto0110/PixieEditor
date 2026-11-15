using Hexa.NET.GLFW;

namespace PixieEditor.Ui;

// The few OpenGL functions the editor calls itself, loaded through GLFW for the current context. The
// engine and ImGui's OpenGL3 backend load their own.
internal static unsafe class Gl
{
    public const uint Rgba = 0x1908;
    public const uint UnsignedByte = 0x1401;
    public const uint PackAlignment = 0x0D05;

    private static delegate* unmanaged[Stdcall]<int, int, int, int, uint, uint, void*, void> ReadPixelsPtr;
    private static delegate* unmanaged[Stdcall]<uint, int, void> PixelStoreiPtr;
    private static delegate* unmanaged[Stdcall]<uint, int, uint*, void> CreateTextures;
    private static delegate* unmanaged[Stdcall]<uint, int, uint, int, int, void> TextureStorage2D;
    private static delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void> TextureSubImage2D;
    private static delegate* unmanaged[Stdcall]<uint, uint, int, void> TextureParameteri;
    private static delegate* unmanaged[Stdcall]<int, uint*, void> DeleteTextures;

    public static void Load()
    {
        ReadPixelsPtr = (delegate* unmanaged[Stdcall]<int, int, int, int, uint, uint, void*, void>)GLFW.GetProcAddress("glReadPixels");
        PixelStoreiPtr = (delegate* unmanaged[Stdcall]<uint, int, void>)GLFW.GetProcAddress("glPixelStorei");
        CreateTextures = (delegate* unmanaged[Stdcall]<uint, int, uint*, void>)GLFW.GetProcAddress("glCreateTextures");
        TextureStorage2D = (delegate* unmanaged[Stdcall]<uint, int, uint, int, int, void>)GLFW.GetProcAddress("glTextureStorage2D");
        TextureSubImage2D = (delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void>)GLFW.GetProcAddress("glTextureSubImage2D");
        TextureParameteri = (delegate* unmanaged[Stdcall]<uint, uint, int, void>)GLFW.GetProcAddress("glTextureParameteri");
        DeleteTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)GLFW.GetProcAddress("glDeleteTextures");
        if (ReadPixelsPtr == null || PixelStoreiPtr == null || CreateTextures == null ||
            TextureStorage2D == null || TextureSubImage2D == null || TextureParameteri == null || DeleteTextures == null)
            throw new InvalidOperationException("OpenGL functions could not be loaded.");
    }

    public static void ReadPixels(int x, int y, int width, int height, uint format, uint type, void* pixels) =>
        ReadPixelsPtr(x, y, width, height, format, type, pixels);

    public static void PixelStorei(uint name, int value) => PixelStoreiPtr(name, value);

    internal static uint UploadPreview(int width, int height, byte[] rgba)
    {
        const uint texture2D = 0x0DE1, rgba8 = 0x8058, minFilter = 0x2801, magFilter = 0x2800;
        const int nearest = 0x2600;
        uint texture;
        CreateTextures(texture2D, 1, &texture);
        TextureStorage2D(texture, 1, rgba8, width, height);
        fixed (byte* pixels = rgba)
            TextureSubImage2D(texture, 0, 0, 0, width, height, Rgba, UnsignedByte, pixels);
        TextureParameteri(texture, minFilter, nearest);
        TextureParameteri(texture, magFilter, nearest);
        return texture;
    }

    internal static void DeletePreview(uint texture) => DeleteTextures(1, &texture);
}
