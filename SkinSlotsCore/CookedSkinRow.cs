namespace SkinSlotsCore;

public sealed class CookedSkinRow
{
	public required int SkinId { get; init; }

	public required int SortNumber { get; init; }

	public required string RowName { get; init; }

	public required string TableSource { get; init; }

	public required string LocalizationSource { get; init; }

	public required string NameKey { get; init; }

	public required string DescriptionKey { get; init; }

	public required int MountTildes { get; init; }
}
