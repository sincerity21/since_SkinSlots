using System.IO;
using System.Text.Json;
using SkinSlotsCore;

namespace SkinSlotsGui;

public sealed class GuiSettings
{
    public const string DefaultUnrealPak =
        @"D:\Modding\ACE7\UnrealPak-Enhanced-v2.0x-ZR1009\Engine\Binaries\Win64\UnrealPak.exe";

    public const string DefaultModsDirectory =
        @"D:\SteamLibrary\steamapps\common\ACE COMBAT 7\Game\Content\Paks\~mods";

    public string ModsDirectory { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string UnrealPakExe { get; set; } = "";
    public string PawnRoot { get; set; } = "";
    public string OutputPakPath { get; set; } = "";
    public string AssignmentPath { get; set; } = "";
    public bool ChooseSlot { get; set; }
    public int Slot { get; set; } = 13;

    public static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "since_SkinSlots",
            "gui.json");

    public static GuiSettings Load()
    {
        GuiSettings settings;
        try
        {
            settings = File.Exists(Path)
                ? JsonSerializer.Deserialize<GuiSettings>(File.ReadAllText(Path)) ?? new GuiSettings()
                : new GuiSettings();
        }
        catch (JsonException)
        {
            settings = new GuiSettings();
        }

        if (string.IsNullOrWhiteSpace(settings.UnrealPakExe) && File.Exists(DefaultUnrealPak))
            settings.UnrealPakExe = DefaultUnrealPak;
        if (string.IsNullOrWhiteSpace(settings.PawnRoot) && Directory.Exists(PawnLibrary.DefaultRoot))
            settings.PawnRoot = PawnLibrary.DefaultRoot;
        if (string.IsNullOrWhiteSpace(settings.ModsDirectory) && Directory.Exists(DefaultModsDirectory))
            settings.ModsDirectory = DefaultModsDirectory;
        if (settings.Slot < 2 || settings.Slot > AssignmentStore.LastPawnSuffix + 1)
            settings.Slot = 13;
        return settings;
    }

    public void Save()
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
