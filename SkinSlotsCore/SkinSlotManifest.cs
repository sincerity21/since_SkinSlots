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
}
