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
                SlotsDirectory = Option(args, "--slots-dir"),
                AssignmentPath = Option(args, "--assignments")
            };

            var result = SkinSlotBuilder.Build(request, Console.WriteLine);
            Console.WriteLine($"Pak: {result.PakPath}");
            Console.WriteLine($"Manifest: {result.ManifestPath}");
            Console.WriteLine($"Row: {result.Manifest.RowName}  pawn: {result.Manifest.Pawn}");
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
            throw new InvalidOperationException($"--slot must be a folder index from 50 to 99. Got '{text}'.");
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
            SkinSlotsCli <skin.pak or unpacked folder> [options]

              --unrealpak <UnrealPak.exe>   Required for a .pak source and for packing.
              --name <text>                 Hangar name written into the manifest.
              --description <text>          Hangar description written into the manifest.
              --output <file.pak>           Output pak. Defaults beside the source.
              --pawns <folder>              Stock AcePlayerPawn library.
              --slot <50-99>                Force a private folder index. Otherwise the next free one is used.
              --slots-dir <folder>          Also copy the manifest here for the UE4SS mod.
              --assignments <file.json>     Slot ledger. Defaults to %AppData%\since_SkinSlots\assignments.json.
              --self-test                   Clone the stock F-16C s02 pawn to slot 50 and check the retarget.

            The pak contains the cloned skin pawn and the moved materials. It does not contain SkinDataTable.
            Copy the .skin_slot.ini into ue4ss/Mods/SkinSlots/Scripts/slots/ and the pak into ~mods.
            """);
    }
}
