namespace SkinSlotsCore;

public static class PawnLibrary
{
    public const string DefaultRoot =
        @"D:\Modding\ACE7\UmodelAC7 + Decryption\UmodelSaved\Game\Blueprint\Player\Pawn";

    public static string TemplatePath(string pawnRoot, string plane, string sourceSlot, out string templateSlot)
    {
        // Skin 01 lives on the base pawn. The thin override child starts at s01,
        // and that child already has the material slots a replacer needs.
        templateSlot = sourceSlot == "00" ? "01" : sourceSlot;
        var path = Path.Combine(pawnRoot, "Skin", $"AcePlayerPawn_{plane}_s{templateSlot}.uasset");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No stock skin pawn for {plane} slot {templateSlot}. The pawn library only has official jets. Expected {path}",
                path);
        }

        return path;
    }

    public static HashSet<string> ShippedRoles(IEnumerable<string> files, string plane, string sourceSlot)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (StringRewriter.TryMaterialRole(stem, plane, sourceSlot, out var role))
                roles.Add(role);
        }

        return roles;
    }

    public static bool HasHangarMaterial(HashSet<string> roles)
    {
        foreach (var role in roles)
        {
            if (role.Contains("Inst", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
