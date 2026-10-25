namespace SkinSlotsCore;

public sealed class SkinSlotManifest
{
    public required string Plane { get; init; }
    public required int SkinNo { get; init; }
    public required int SkinId { get; init; }
    public required string Pawn { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int SortNumber { get; init; }
    public required string TemplatePawn { get; init; }
    public required string ShippedRoles { get; init; }

    public string RowName => $"SS_{Plane}_{SkinNo:D2}";

    public void Write(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var text = string.Join(Environment.NewLine, new[]
        {
            "plane=" + Plane,
            "skin_no=" + SkinNo.ToString("D2"),
            "skin_id=" + SkinId,
            "pawn=" + Pawn,
            "name=" + Name.Replace("\r", " ").Replace("\n", " "),
            "description=" + Description.Replace("\r", " ").Replace("\n", " "),
            "sort_number=" + SortNumber,
            "row_name=" + RowName,
            "template_pawn=" + TemplatePawn,
            "shipped_roles=" + ShippedRoles
        });
        File.WriteAllText(path, text + Environment.NewLine);
    }
}
