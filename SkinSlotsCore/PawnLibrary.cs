using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkinSlotsCore;

public static class PawnLibrary
{
	public const string DefaultRoot = "D:\\Modding\\ACE7\\UmodelAC7 + Decryption\\UmodelSaved\\Game\\Blueprint\\Player\\Pawn";

	private static readonly Regex PawnName = new Regex("^AcePlayerPawn_([a-z0-9]+)(?:_s(\\d+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Dictionary<string, Dictionary<string, PlaneSkins>> CensusCache = new Dictionary<string, Dictionary<string, PlaneSkins>>(StringComparer.OrdinalIgnoreCase);

	public static string StockSkinTable(string pawnRoot)
	{
		string text = Path.Combine(GameContentRoot(pawnRoot), "Blueprint", "Information", "SkinDataTable.uasset");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("Stock SkinDataTable was not found at " + text + ".", text);
		}
		string text2 = Path.ChangeExtension(text, ".uexp");
		if (!File.Exists(text2))
		{
			throw new FileNotFoundException("Stock SkinDataTable is missing its .uexp beside " + text + ".", text2);
		}
		return text;
	}

	public static string StockLocalizationDirectory(string pawnRoot)
	{
		string text = Path.Combine(GameContentRoot(pawnRoot), "Localization", "Game");
		string text2 = Path.Combine(text, "Cmn.dat");
		if (!File.Exists(text2))
		{
			throw new FileNotFoundException("Stock Cmn.dat was not found at " + text2 + ".", text2);
		}
		return text;
	}

	private static string GameContentRoot(string pawnRoot)
	{
		return Path.GetFullPath(Path.Combine(pawnRoot, "..", "..", ".."));
	}

	public static PlaneSkins Describe(string pawnRoot, string plane)
	{
		if (!Census(pawnRoot).TryGetValue(plane, out PlaneSkins value))
		{
			throw new FileNotFoundException($"No saved pawns for {plane}. Expected AcePlayerPawn_{plane}.uasset under {pawnRoot}.", pawnRoot);
		}
		return value;
	}

	public static IReadOnlyDictionary<string, PlaneSkins> Census(string pawnRoot)
	{
		string fullPath = Path.GetFullPath(pawnRoot);
		if (CensusCache.TryGetValue(fullPath, out Dictionary<string, PlaneSkins> value))
		{
			return value;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, List<int>> dictionary = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in Directory.EnumerateFiles(fullPath, "AcePlayerPawn_*.uasset", SearchOption.AllDirectories))
		{
			Match match = PawnName.Match(Path.GetFileNameWithoutExtension(item));
			if (!match.Success)
			{
				continue;
			}
			string text = match.Groups[1].Value.ToLowerInvariant();
			if (!match.Groups[2].Success)
			{
				hashSet.Add(text);
				continue;
			}
			if (!dictionary.TryGetValue(text, out var value2))
			{
				value2 = (dictionary[text] = new List<int>());
			}
			value2.Add(int.Parse(match.Groups[2].Value));
		}
		Dictionary<string, PlaneSkins> dictionary2 = new Dictionary<string, PlaneSkins>(StringComparer.OrdinalIgnoreCase);
		foreach (string item2 in hashSet.Concat(dictionary.Keys).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			dictionary.TryGetValue(item2, out var value3);
			List<int> list2 = value3 ?? new List<int>();
			int lastSuffix = ((list2.Count != 0) ? list2.Max() : 0);
			dictionary2[item2] = new PlaneSkins
			{
				Plane = item2,
				HasBase = hashSet.Contains(item2),
				LastSuffix = lastSuffix,
				SkinCount = (hashSet.Contains(item2) ? 1 : 0) + list2.Count
			};
		}
		CensusCache[fullPath] = dictionary2;
		return dictionary2;
	}

	public static string TemplatePath(string pawnRoot, string plane, string sourceSlot, out string templateSlot)
	{
		templateSlot = ((sourceSlot == "00") ? "03" : sourceSlot);
		string text = Path.Combine(pawnRoot, "Skin", $"AcePlayerPawn_{plane}_s{templateSlot}.uasset");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException($"No stock skin pawn for {plane} slot {templateSlot}. The pawn library only has official jets. Expected {text}", text);
		}
		return text;
	}

	public static HashSet<string> ShippedRoles(IEnumerable<string> files, string plane, string sourceSlot)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string file in files)
		{
			if (StringRewriter.TryMaterialRole(Path.GetFileNameWithoutExtension(file), plane, sourceSlot, out string role))
			{
				hashSet.Add(role);
			}
		}
		return hashSet;
	}

	public static IReadOnlyList<string> BodySlots(string pawnRoot, string plane)
	{
		return SavedSlots(pawnRoot, plane, (string slot, string file) => Path.GetFileName(file).Equals(plane + "_" + slot + "_Inst.uasset", StringComparison.OrdinalIgnoreCase));
	}

	public static IReadOnlyList<string> MrecSlots(string pawnRoot, string plane)
	{
		return SavedSlots(pawnRoot, plane, (string slot, string file) => Path.GetFileName(file).Equals(plane + "_" + slot + "_MREC.uasset", StringComparison.OrdinalIgnoreCase));
	}

	public static IReadOnlyList<string> NormalSlots(string pawnRoot, string plane)
	{
		return SavedSlots(pawnRoot, plane, (string slot, string file) => Path.GetFileName(file).Equals(plane + "_" + slot + "_N.uasset", StringComparison.OrdinalIgnoreCase));
	}

	public static string StockTexture(string pawnRoot, string plane, string sourceSlot, string suffix)
	{
		string directory = Path.Combine(GameContentRoot(pawnRoot), "Vehicles", "Aircraft", plane, sourceSlot);
		string name = plane + "_" + sourceSlot + "_" + suffix + ".uasset";
		string path = Path.Combine(directory, name);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"No saved {suffix} for {plane} slot {sourceSlot}. Expected {name} under {directory}.", directory);
		}
		string export = Path.ChangeExtension(path, ".uexp");
		if (!File.Exists(export))
		{
			throw new FileNotFoundException("The saved " + name + " is missing its .uexp.", export);
		}
		return path;
	}

	public static IReadOnlyList<string> DecalSlots(string pawnRoot, string plane)
	{
		return SavedSlots(pawnRoot, plane, (string slot, string file) =>
		{
			string name = Path.GetFileName(file);
			return name.Contains("Decal", StringComparison.OrdinalIgnoreCase) && name.Contains("Inst", StringComparison.OrdinalIgnoreCase);
		});
	}

	private static List<string> SavedSlots(string pawnRoot, string plane, Func<string, string, bool> match)
	{
		string text = Path.Combine(GameContentRoot(pawnRoot), "Vehicles", "Aircraft", plane);
		List<string> list = new List<string>();
		if (!Directory.Exists(text))
		{
			return list;
		}
		foreach (string item in Directory.EnumerateDirectories(text))
		{
			string fileName = Path.GetFileName(item);
			if (fileName.Length != 2 || !fileName.All(char.IsDigit))
			{
				continue;
			}
			if (Directory.EnumerateFiles(item, "*.uasset").Any((string file) => match(fileName, file)))
			{
				list.Add(fileName);
			}
		}
		list.Sort(StringComparer.Ordinal);
		return list;
	}

	public static string StockDecal(string pawnRoot, string plane, string sourceSlot)
	{
		string text = Path.Combine(GameContentRoot(pawnRoot), "Vehicles", "Aircraft", plane, sourceSlot);
		if (!Directory.Exists(text))
		{
			throw new FileNotFoundException($"No saved decals for {plane} slot {sourceSlot}. Expected them under {text}.", text);
		}
		string text2 = Directory.EnumerateFiles(text, "*.uasset").FirstOrDefault((string file) => Path.GetFileName(file).Contains("Decal", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(file).Contains("Inst", StringComparison.OrdinalIgnoreCase));
		if (text2 == null)
		{
			throw new FileNotFoundException($"No saved decal instance for {plane} slot {sourceSlot} in {text}.", text);
		}
		string text3 = Path.ChangeExtension(text2, ".uexp");
		if (!File.Exists(text3))
		{
			throw new FileNotFoundException("The saved decal " + Path.GetFileName(text2) + " is missing its .uexp.", text3);
		}
		return text2;
	}

	public static string? TryStockBody(string pawnRoot, string plane, string sourceSlot)
	{
		string directory = Path.Combine(GameContentRoot(pawnRoot), "Vehicles", "Aircraft", plane, sourceSlot);
		if (!Directory.Exists(directory))
		{
			return null;
		}
		string? match = Directory.EnumerateFiles(directory, "*.uasset").FirstOrDefault((string file) => Path.GetFileName(file).Equals(plane + "_" + sourceSlot + "_Inst.uasset", StringComparison.OrdinalIgnoreCase));
		if (match == null || !File.Exists(Path.ChangeExtension(match, ".uexp")))
		{
			return null;
		}
		return match;
	}

	public static string StockBody(string pawnRoot, string plane, string sourceSlot)
	{
		string text = TryStockBody(pawnRoot, plane, sourceSlot);
		if (text == null)
		{
			string text2 = Path.Combine(GameContentRoot(pawnRoot), "Vehicles", "Aircraft", plane, sourceSlot);
			throw new FileNotFoundException($"No saved material instance for {plane} slot {sourceSlot}. Expected {plane}_{sourceSlot}_Inst.uasset under {text2}.", text2);
		}
		return text;
	}

	public static bool ShipsBody(HashSet<string> roles)
	{
		return roles.Contains("Inst");
	}

	public static bool IsMaterialTexture(string fileName, string plane)
	{
		string name = Path.GetFileNameWithoutExtension(fileName);
		if (!name.Contains(plane, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return name.EndsWith("_D", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_MREC", StringComparison.OrdinalIgnoreCase) || name.EndsWith("xMREC", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsNormalTexture(string fileName, string plane)
	{
		string name = Path.GetFileNameWithoutExtension(fileName);
		return name.Contains(plane, StringComparison.OrdinalIgnoreCase) && name.EndsWith("_N", StringComparison.OrdinalIgnoreCase);
	}

	public static bool ShipsMaterialTexture(IEnumerable<string> files, string plane)
	{
		return files.Any((string file) => file.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && IsMaterialTexture(file, plane));
	}

	public static bool ShipsNormalTexture(IEnumerable<string> files, string plane)
	{
		return files.Any((string file) => file.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && IsNormalTexture(file, plane));
	}

	public static string? FindShippedPawn(IEnumerable<string> files, string plane)
	{
		string prefix = "AcePlayerPawn_" + plane + "_s";
		foreach (string file in files)
		{
			if (!file.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (Path.GetFileName(file).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return file;
			}
		}
		return null;
	}

	public static bool TryPawnSuffix(string pawnUasset, string plane, out string suffix)
	{
		string name = Path.GetFileNameWithoutExtension(pawnUasset);
		string prefix = "AcePlayerPawn_" + plane + "_s";
		suffix = "";
		if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.Length < prefix.Length + 2)
		{
			return false;
		}
		suffix = name.Substring(prefix.Length, 2);
		return suffix.All(char.IsDigit);
	}

	public static bool HasHangarMaterial(HashSet<string> roles)
	{
		foreach (string role in roles)
		{
			if (role.Contains("Inst", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
