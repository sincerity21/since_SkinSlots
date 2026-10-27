using SkinSlotsCore;

namespace SkinSlotsCli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                PrintHelp();
                return args.Length == 0 ? 1 : 0;
            }

            if (args[0] == "--self-test")
            {
                var pawns = Option(args, "--pawns") ?? PawnLibrary.DefaultRoot;
                PawnCloneCheck.Run(pawns);
                Console.WriteLine("Self-test passed.");
                return 0;
            }

            var source = args[0];
            var request = new SkinSlotBuildRequest
            {
                SourcePath = source,
                UnrealPakExe = Option(args, "--unrealpak"),
                OutputPakPath = Option(args, "--output"),
                PawnRoot = Option(args, "--pawns") ?? PawnLibrary.DefaultRoot,
                DisplayName = Option(args, "--name"),
                Description = Option(args, "--description"),
                Slot = ParseSlot(Option(args, "--slot")),
                ModsDirectory = Option(args, "--mods")
            };

            var result = SkinSlotBuilder.Build(request, Console.WriteLine);
            Console.WriteLine($"Skin pak: {result.PakPath}");
            Console.WriteLine($"Table pak: {result.DataPakPath}");
            Console.WriteLine($"Pawn: {result.Manifest.Pawn}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int? ParseSlot(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (!int.TryParse(text, out var slot))
            throw new InvalidOperationException($"--slot is the hangar number, such as 13 for the F-16C. Got '{text}'.");
        return slot;
    }

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            sincerity's Skin Re-Slotter
            SkinSlotsCli <skin.pak or unpacked folder> [options]

              --unrealpak <UnrealPak.exe>   Required for a .pak source and for packing.
              --name <text>                 Hangar name written into localization.
              --description <text>          Hangar description written into localization.
              --output <file.pak>           Skin pak path. Defaults to SkinReSlotter_F-16C_Name_Skin13_P.pak in the mods folder.
              --pawns <folder>              Stock AcePlayerPawn library. The saved SkinDataTable and localization sit beside it.
              --mods <folder>               Game ~mods folder. A SkinDataTable or localization in there is used instead of the saved game files.
              --slot <number>               Hangar slot, such as 13. Otherwise the next slot after the latest skin in the table is used.
              --self-test                   Count the saved pawns and clone the stock F-16C s02 pawn to s12.

            A skin made for slot 3 is cloned from the stock s02 pawn. Slot 13 uses pawn s12.
            The skin pak holds the textures and the pawn. SkinReSlotter_Data_P holds the table and the names, with enough leading tildes to mount last.
            A skin pak that is already in the mods folder is left alone. The table pak is written again.
            Put both paks in the game ~mods folder.
            """);
    }
}
