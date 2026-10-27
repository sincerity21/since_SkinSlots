namespace SkinSlotsCore;

public sealed class SkinSourcePreview
{
	public required string Plane { get; init; }

	public required int SourceSkinNumber { get; init; }

	public required string SourceSlotIndex { get; init; }

	public required string Roles { get; init; }

	public required bool HasHangarMaterial { get; init; }

	public required bool UsesStockBody { get; init; }

	public required bool ShipsMaterial { get; init; }

	public required bool ShipsNormal { get; init; }

	public string? MaterialReferenceSlot { get; init; }

	public string? NormalReferenceSlot { get; init; }

	public required string TemplatePawn { get; init; }

	public required int FileCount { get; init; }

	public required int StockSkinCount { get; init; }

	public required int LastStockSuffix { get; init; }

	public required int NextSuffix { get; init; }

	public required string PlaneLabel { get; init; }

	public required bool TableFromMods { get; init; }

	public required int[] SkinNos { get; init; }

	public required bool BulkOnly { get; init; }

	public required bool SharedTextures { get; init; }

	public required bool HasTemplatePawn { get; init; }

	public required bool ShippedPawn { get; init; }

	public required bool ShipsDecal { get; init; }

	public required bool ShipsInstance { get; init; }

	public required bool ShipsDiffuse { get; init; }

	public required bool ShipsDiffuseUasset { get; init; }

	public required bool ShipsMrec { get; init; }

	public required bool ShipsMrecUasset { get; init; }

	public required bool ShipsNormalFile { get; init; }

	public required bool ShipsNormalUasset { get; init; }

	public required bool MaterialFromShared { get; init; }

	public required bool NormalFromShared { get; init; }

	public string? SharedMaterialSlot { get; init; }

	public int NextHangarSlot => NextSuffix + 1;

	public int ThroughHangar => NextSuffix;

	public string LastStockLabel => $"slot {((LastStockSuffix <= 0) ? 1 : (LastStockSuffix + 1))}";
}
