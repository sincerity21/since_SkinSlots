namespace SkinSlotsCore;

public sealed class SkinSlotBuildResult
{
	public required string PakPath { get; init; }

	public required string DataPakPath { get; init; }

	public required bool ContentSkipped { get; init; }

	public required SkinSlotManifest Manifest { get; init; }
}
