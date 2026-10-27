namespace SkinSlotsCore;

public sealed class SkinSlotBuildRequest
{
	public required string SourcePath { get; init; }

	public string? UnrealPakExe { get; init; }

	public string? OutputPakPath { get; init; }

	public string? OutputDirectory { get; init; }

	public string PawnRoot { get; init; } = "D:\\Modding\\ACE7\\UmodelAC7 + Decryption\\UmodelSaved\\Game\\Blueprint\\Player\\Pawn";

	public string? DisplayName { get; init; }

	public string? Description { get; init; }

	public IReadOnlyDictionary<char, SkinLocale>? Languages { get; init; }

	public int? Slot { get; init; }

	public string? AssignmentPath { get; init; }

	public string? ModsDirectory { get; init; }

	public string? BodySlot { get; init; }

	public string? DecalSlot { get; init; }

	public string? MaterialSlot { get; init; }

	public string? NormalSlot { get; init; }
}

public sealed class SkinLocale
{
	public string? Name { get; init; }

	public string? Description { get; init; }
}

public sealed record SkinLanguage(char Letter, string Name)
{
	public static IReadOnlyList<SkinLanguage> All { get; } = Ace7LocalizationFormat.Formats.DatConstants.DatLetters.OrderBy(pair => pair.Key).Select(pair => new SkinLanguage(pair.Key, pair.Value == "Brazilian Portugese" ? "Brazilian Portuguese" : pair.Value)).ToArray();
}
