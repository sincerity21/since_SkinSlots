using SkinConverterCore.Services;

namespace SkinSlotsCore;

public sealed class SkinSlotBuildRequest
{
    public required string SourcePath { get; init; }
    public string? UnrealPakExe { get; init; }
    public string? OutputPakPath { get; init; }
    public string PawnRoot { get; init; } = PawnLibrary.DefaultRoot;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public int? Slot { get; init; }
    public string? SlotsDirectory { get; init; }
    public string? AssignmentPath { get; init; }
}

public sealed class SkinSlotBuildResult
{
    public required string PakPath { get; init; }
    public required string ManifestPath { get; init; }
    public required SkinSlotManifest Manifest { get; init; }
}

public static class SkinSlotBuilder
{
    public static SkinSlotBuildResult Build(SkinSlotBuildRequest request, Action<string>? log = null)
    {
        if (!File.Exists(request.SourcePath) && !Directory.Exists(request.SourcePath))
            throw new FileNotFoundException("Skin source was not found.", request.SourcePath);

        var assignmentPath = string.IsNullOrWhiteSpace(request.AssignmentPath)
            ? AssignmentStore.DefaultPath
            : request.AssignmentPath;
        var store = AssignmentStore.Load(assignmentPath);

        string? extract = null;
        try
        {
            var sourceRoot = request.SourcePath;
            if (File.Exists(request.SourcePath))
            {
                if (request.UnrealPakExe == null || !File.Exists(request.UnrealPakExe))
                    throw new FileNotFoundException("UnrealPak.exe is required to read a .pak.", request.UnrealPakExe);
                extract = Path.Combine(Path.GetTempPath(), "SkinSlots_" + Guid.NewGuid().ToString("N"));
                PakService.ExtractPak(request.UnrealPakExe, request.SourcePath, extract, log);
                sourceRoot = extract;
            }

            var detected = SkinSlotDetector.Detect(sourceRoot, log);
            var plane = detected.PlaneStringId.ToLowerInvariant();
            var sourceSlot = detected.SourceSlotIndex;
            if (sourceSlot.Length != 2)
                throw new InvalidOperationException($"Source slot '{sourceSlot}' is not a two-digit folder index.");

            var roles = PawnLibrary.ShippedRoles(Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories), plane, sourceSlot);
            if (!PawnLibrary.HasHangarMaterial(roles))
            {
                throw new InvalidOperationException(
                    "This skin has no material instance (_Inst, _Decal_Inst, or _CP_Inst). The new pawn can only point at a material the pak ships.");
            }

            var templatePath = PawnLibrary.TemplatePath(request.PawnRoot, plane, sourceSlot, out var templateSlot);
            var skinNo = store.AllocateSlot(plane, request.Slot);
            var skinId = store.AllocateSkinId();
            var destSlot = skinNo.ToString("D2");
            log?.Invoke($"Template {Path.GetFileName(templatePath)} -> slot {destSlot}. Shipped roles: {string.Join(", ", roles)}");

            var stage = Path.Combine(Path.GetTempPath(), "SkinSlotsStage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                StageReplacer(sourceRoot, stage, plane, sourceSlot, destSlot, log);
                var pawnName = $"AcePlayerPawn_{plane}_s{destSlot}";
                var pawnDest = Path.Combine(stage, "Nimbus", "Content", "Blueprint", "Player", "Pawn", "Skin", pawnName + ".uasset");
                StagePawn(templatePath, pawnDest, plane, templateSlot, destSlot, roles, log);

                var outputPak = request.OutputPakPath;
                if (string.IsNullOrWhiteSpace(outputPak))
                {
                    var sourceForName = File.Exists(request.SourcePath) ? request.SourcePath : request.SourcePath.TrimEnd('\\', '/');
                    outputPak = OutputPathHelper.DefaultPakOutputPath(sourceForName + (File.Exists(request.SourcePath) ? "" : ".pak"), skinNo);
                }
                outputPak = OutputPathHelper.EnsurePakPatchSuffix(outputPak);

                if (request.UnrealPakExe == null || !File.Exists(request.UnrealPakExe))
                    throw new FileNotFoundException("UnrealPak.exe is required to pack the new slot.", request.UnrealPakExe);

                PakService.CreatePak(request.UnrealPakExe, stage, outputPak, log);

                var display = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? Path.GetFileNameWithoutExtension(outputPak)
                    : request.DisplayName.Trim();
                var manifest = new SkinSlotManifest
                {
                    Plane = plane,
                    SkinNo = skinNo,
                    SkinId = skinId,
                    Pawn = $"/Game/Blueprint/Player/Pawn/Skin/{pawnName}.{pawnName}_C",
                    Name = display,
                    Description = request.Description?.Trim() ?? "",
                    SortNumber = 1000 + skinNo,
                    TemplatePawn = Path.GetFileNameWithoutExtension(templatePath),
                    ShippedRoles = string.Join(",", roles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase))
                };
                var manifestPath = Path.ChangeExtension(outputPak, ".skin_slot.ini");
                manifest.Write(manifestPath);
                if (!string.IsNullOrWhiteSpace(request.SlotsDirectory))
                {
                    Directory.CreateDirectory(request.SlotsDirectory);
                    var copy = Path.Combine(request.SlotsDirectory, Path.GetFileName(manifestPath));
                    File.Copy(manifestPath, copy, overwrite: true);
                    log?.Invoke($"Copied manifest to {copy}");
                }

                store.Save(assignmentPath);
                log?.Invoke($"Assigned {plane} skin {destSlot} as {manifest.RowName}.");
                return new SkinSlotBuildResult { PakPath = outputPak, ManifestPath = manifestPath, Manifest = manifest };
            }
            finally
            {
                TryDelete(stage);
            }
        }
        finally
        {
            if (extract != null)
                TryDelete(extract);
        }
    }

    public static void StagePawn(
        string templateUasset,
        string destUasset,
        string plane,
        string templateSlot,
        string destSlot,
        IReadOnlySet<string> shippedRoles,
        Action<string>? log = null)
    {
        var (asset, export) = AcePackage.ReadPlain(templateUasset);
        asset = StringRewriter.Rewrite(asset, text => MapPawn(text, plane, templateSlot, destSlot, shippedRoles));
        if (export != null)
            export = StringRewriter.Rewrite(export, text => MapPawn(text, plane, templateSlot, destSlot, shippedRoles));
        AcePackage.WriteEncrypted(destUasset, asset, export);
        log?.Invoke($"Cloned pawn {Path.GetFileName(destUasset)}");
    }

    public static string? MapPawn(string text, string plane, string templateSlot, string destSlot, IReadOnlySet<string> shippedRoles)
    {
        var pawnToken = $"AcePlayerPawn_{plane}_s{templateSlot}";
        var pawnDest = $"AcePlayerPawn_{plane}_s{destSlot}";
        if (text.Contains(pawnToken, StringComparison.OrdinalIgnoreCase))
        {
            var renamed = text;
            while (renamed.Contains(pawnToken, StringComparison.OrdinalIgnoreCase))
                renamed = ReplaceOrdinal(renamed, pawnToken, pawnDest);
            return renamed;
        }

        if (!StringRewriter.TryMaterialRole(text, plane, templateSlot, out var role))
            return null;
        if (!shippedRoles.Contains(role))
            return null;
        return StringRewriter.RetargetMaterialText(text, plane, templateSlot, destSlot);
    }

    private static void StageReplacer(string sourceRoot, string stageRoot, string plane, string sourceSlot, string destSlot, Action<string>? log)
    {
        foreach (var file in Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".uexp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ubulk", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            var relativeDir = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
            var retargetedDir = RetargetDirectory(relativeDir, plane, sourceSlot, destSlot);
            var retargetedName = StringRewriter.RetargetFileName(Path.GetFileName(file), plane, sourceSlot, destSlot);
            var dest = Path.Combine(stageRoot, retargetedDir.Replace('/', Path.DirectorySeparatorChar), retargetedName);

            if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                var (asset, export) = AcePackage.ReadPlain(file);
                asset = StringRewriter.Rewrite(asset, text => StringRewriter.RetargetMaterialText(text, plane, sourceSlot, destSlot));
                if (export != null)
                    export = StringRewriter.Rewrite(export, text => StringRewriter.RetargetMaterialText(text, plane, sourceSlot, destSlot));
                AcePackage.WriteEncrypted(dest, asset, export);

                var bulk = Path.ChangeExtension(file, ".ubulk");
                if (File.Exists(bulk))
                {
                    var bulkName = StringRewriter.RetargetFileName(Path.GetFileName(bulk), plane, sourceSlot, destSlot);
                    var bulkDest = Path.Combine(Path.GetDirectoryName(dest)!, bulkName);
                    File.Copy(bulk, bulkDest, overwrite: true);
                }

                log?.Invoke($"Retargeted {Path.GetFileName(file)} -> {retargetedName}");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
        }
    }

    public static string RetargetDirectory(string relativeDir, string plane, string sourceSlot, string destSlot)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            relativeDir,
            $@"(Aircraft/{System.Text.RegularExpressions.Regex.Escape(plane)}/){sourceSlot}(?=/|$)",
            $"${{1}}{destSlot}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string ReplaceOrdinal(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return text;
        return text[..index] + newValue + text[(index + oldValue.Length)..];
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // The staged files are a temp copy. Leaving them behind does not change the packed pak.
        }
    }
}
