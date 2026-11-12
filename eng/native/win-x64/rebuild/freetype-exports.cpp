#include "imgui.h"
#include "imgui_internal.h"
#include "cimgui.h"

// The MSVC preprocessor route in the pinned cimgui generator omits these
// FreeType wrappers. Preserve the export signatures in Hexa's binding input.
CIMGUI_API const ImFontLoader* ImGuiFreeType_GetFontLoader()
{
    return ImGuiFreeType::GetFontLoader();
}

CIMGUI_API void ImGuiFreeType_SetAllocatorFunctions(
    void* (*allocate)(size_t, void*), void (*release)(void*, void*), void* userData)
{
    ImGuiFreeType::SetAllocatorFunctions(allocate, release, userData);
}

CIMGUI_API bool ImGuiFreeType_DebugEditFontLoaderFlags(ImGuiFreeTypeLoaderFlags* flags)
{
    return ImGuiFreeType::DebugEditFontLoaderFlags(flags);
}
