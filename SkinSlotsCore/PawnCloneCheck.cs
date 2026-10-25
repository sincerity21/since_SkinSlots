namespace SkinSlotsCore;

public static class PawnCloneCheck
{
    public static void Run(string pawnRoot)
    {
        var template = PawnLibrary.TemplatePath(pawnRoot, "f16c", "02", out var templateSlot);
        if (templateSlot != "02")
            throw new InvalidOperationException($"Expected template slot 02, got {templateSlot}.");

        var zero = PawnLibrary.TemplatePath(pawnRoot, "f16c", "00", out var zeroTemplate);
        if (zeroTemplate != "01" || !zero.EndsWith("AcePlayerPawn_f16c_s01.uasset", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Skin 01 should clone the s01 child, not the base pawn.");

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Inst" };
        var dest = Path.Combine(Path.GetTempPath(), "SkinSlotsCheck_" + Guid.NewGuid().ToString("N"), "AcePlayerPawn_f16c_s50.uasset");
        try
        {
            SkinSlotBuilder.StagePawn(template, dest, "f16c", "02", "50", roles, null);
            var (asset, _) = AcePackage.ReadPlain(dest);
            var text = System.Text.Encoding.ASCII.GetString(asset);
            Require(text.Contains("AcePlayerPawn_f16c_s50"), "cloned pawn was not renamed");
            Require(!text.Contains("AcePlayerPawn_f16c_s02"), "template pawn name survived");
            Require(text.Contains("f16c_50_Inst"), "shipped body material was not retargeted");
            Require(text.Contains("/f16c/50/"), "shipped material folder was not retargeted");
            Require(text.Contains("f16c_02_Decal_Inst"), "unshipped decal should stay on the stock slot");
            Require(text.Contains("f16c_02_CP_Inst"), "unshipped cockpit should stay on the stock slot");
            Require(text.Contains("AcePlayerPawn_f16c_C"), "parent pawn reference was lost");
            Require(AcePackage.IsEncryptedUAsset(File.ReadAllBytes(dest)), "output pawn was not re-encrypted under the new file name");
        }
        finally
        {
            var dir = Path.GetDirectoryName(dest);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
