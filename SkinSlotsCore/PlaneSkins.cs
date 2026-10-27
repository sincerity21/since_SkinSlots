namespace SkinSlotsCore;

public sealed class PlaneSkins
{
	public required string Plane { get; init; }

	public required bool HasBase { get; init; }

	public required int LastSuffix { get; init; }

	public required int SkinCount { get; init; }

	public int NextSuffix => LastSuffix + 1;

	public int NextHangarSlot => NextSuffix + 1;

	public string LastPawnLabel
	{
		get
		{
			if (LastSuffix > 0)
			{
				return $"s{LastSuffix:D2}";
			}
			return "base pawn";
		}
	}
}
