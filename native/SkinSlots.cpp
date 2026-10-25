// SkinSlots.dll
// Loaded by main.lua. It records every *.skin_slot.ini sitting in the slots folder.
// The hangar row itself is inserted by that script through UE4SS's UDataTable:AddRow,
// which is the native AddRow for this build.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <filesystem>
#include <fstream>
#include <string>

namespace
{
    std::filesystem::path ModuleDirectory()
    {
        wchar_t buffer[MAX_PATH]{};
        HMODULE module = nullptr;
        if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCWSTR>(&ModuleDirectory), &module))
        {
            GetModuleFileNameW(module, buffer, MAX_PATH);
        }
        return std::filesystem::path(buffer).parent_path();
    }

    void LogLine(const std::filesystem::path& directory, const std::string& line)
    {
        std::ofstream out(directory / "SkinSlots.log", std::ios::app);
        if (out)
            out << line << "\n";
    }

    int CountManifests(const std::filesystem::path& directory)
    {
        auto slots = directory / "slots";
        if (!std::filesystem::exists(slots))
            return 0;
        int count = 0;
        for (const auto& entry : std::filesystem::directory_iterator(slots))
        {
            if (!entry.is_regular_file())
                continue;
            if (entry.path().extension() == ".ini" && entry.path().filename().wstring().find(L".skin_slot.ini") != std::wstring::npos)
                ++count;
        }
        return count;
    }
}

extern "C" __declspec(dllexport) int luaopen_skinslots(void*)
{
    auto directory = ModuleDirectory();
    LogLine(directory, "SkinSlots.dll loaded");
    LogLine(directory, "manifests=" + std::to_string(CountManifests(directory)));
    return 0;
}

extern "C" __declspec(dllexport) int skinslots_tick(void*)
{
    return 0;
}
