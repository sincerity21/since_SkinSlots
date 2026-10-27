using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ace7LocalizationFormat.Formats;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace SkinSlotsCore;

public static class MountedSkin
{
	public sealed class PlaneTableInfo
	{
		public required int MaxSkinNo { get; init; }

		public required int[] SkinNos { get; init; }
	}

	private sealed class LocatedFile : IDisposable
	{
		public required string Path { get; init; }

		public string? Cleanup { get; init; }

		public void Dispose()
		{
			if (Cleanup != null && Directory.Exists(Cleanup))
			{
				try
				{
					Directory.Delete(Cleanup, recursive: true);
				}
				catch
				{
				}
			}
		}
	}

	private sealed class LocatedLocalization : IDisposable
	{
		public required string CmnPath { get; init; }

		public required string DirectoryPath { get; init; }

		public string? Cleanup { get; init; }

		public void Dispose()
		{
			if (Cleanup != null && Directory.Exists(Cleanup))
			{
				try
				{
					Directory.Delete(Cleanup, recursive: true);
				}
				catch
				{
				}
			}
		}
	}

	private sealed class OpenedTable : IDisposable
	{
		public required UAsset Asset { get; init; }

		public required DataTableExport Export { get; init; }

		public required bool Encrypted { get; init; }

		public required string Temp { get; init; }

		public void Dispose()
		{
			TryDelete(Temp);
		}
	}

	public const string DataPakStem = "SkinReSlotter_Data_P";

	public const string MarkerFileName = "SkinReSlotter.marker";

	public const string MarkerToken = "since.SkinReSlotter.DataPak.1";

	public const string DefaultPaksDirectory = "D:\\SteamLibrary\\steamapps\\common\\ACE COMBAT 7\\Game\\Content\\Paks";

	public static int MountTildes(string? paksDirectory)
	{
		return Rank(ResolvePaks(paksDirectory));
	}

	private static int Rank(string root)
	{
		int val = LeadingTildes(LastPakContaining(root, "SkinDataTable.uasset", enabledOnly: false, skipOwnDataPak: true));
		int val2 = LeadingTildes(LastPakContaining(root, "Cmn.dat", enabledOnly: false, skipOwnDataPak: true));
		return Math.Max(val, val2) + 1;
	}

	public static string? WinningPak(string directory, string needle)
	{
		return LastPakContaining(directory, needle, enabledOnly: true);
	}

	public static IReadOnlyList<string> PaksContaining(string directory, string needle, bool enabledOnly)
	{
		if (!Directory.Exists(directory))
		{
			return Array.Empty<string>();
		}
		IEnumerable<string> enumerable = Directory.EnumerateFiles(directory, "*.pak", SearchOption.AllDirectories);
		if (!enabledOnly)
		{
			enumerable = enumerable.Concat(Directory.EnumerateFiles(directory, "*.pak.disabled", SearchOption.AllDirectories));
		}
		List<string> list = new List<string>();
		foreach (string item in enumerable.OrderBy<string, string>((string file) => file, StringComparer.Ordinal))
		{
			if (TailContains(item, needle))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static string WithMountPrefix(string pakPath, int tildeCount)
	{
		string path = Path.GetDirectoryName(pakPath) ?? ".";
		string fileName = Path.GetFileName(pakPath);
		string text = fileName.TrimStart('~');
		if (text.StartsWith('!'))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		if (LeadingTildes(fileName) >= tildeCount && fileName.Contains('!', StringComparison.Ordinal))
		{
			return pakPath;
		}
		return Path.Combine(path, new string('~', tildeCount) + "!" + text);
	}

	public static CookedSkinRow Cook(string stageRoot, string? paksDirectory, string tablePath, string localizationDirectory, string plane, int skinNo, string pawnPath, string displayName, string description, Action<string>? log, IReadOnlyDictionary<char, SkinLocale>? languages = null)
	{
		(int, int, string) tuple = WriteTable(stageRoot, new LocatedFile
		{
			Path = tablePath
		}, plane, skinNo, pawnPath, log);
		(string, string) tuple2 = WriteNames(stageRoot, new LocatedLocalization
		{
			CmnPath = Path.Combine(localizationDirectory, "Cmn.dat"),
			DirectoryPath = localizationDirectory
		}, plane, skinNo, displayName, description, languages, log);
		return new CookedSkinRow
		{
			SkinId = tuple.Item1,
			SortNumber = tuple.Item2,
			RowName = tuple.Item3,
			TableSource = tablePath,
			LocalizationSource = Path.Combine(localizationDirectory, "Cmn.dat"),
			NameKey = tuple2.Item1,
			DescriptionKey = tuple2.Item2,
			MountTildes = MountRank(paksDirectory)
		};
	}

	public static PlaneTableInfo ReadPlane(string tablePath, string plane)
	{
		using OpenedTable openedTable = OpenTable(tablePath);
		UAsset asset = openedTable.Asset;
		List<StructPropertyData> data = openedTable.Export.Table.Data;
		List<int> list = new List<int>();
		for (int i = 0; i < data.Count; i++)
		{
			if (PlaneOf(asset, data[i]).Equals(plane, StringComparison.OrdinalIgnoreCase))
			{
				list.Add(SkinNoOf(asset, data[i]));
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException("SkinDataTable has no " + plane + " row to copy.");
		}
		return new PlaneTableInfo
		{
			MaxSkinNo = list.Max(),
			SkinNos = list.ToArray()
		};
	}

	public static string PlaneLabel(string localizationDirectory, string plane)
	{
		string path = Path.Combine(localizationDirectory, "Cmn.dat");
		string path2 = Path.Combine(localizationDirectory, "A.dat");
		if (!File.Exists(path) || !File.Exists(path2))
		{
			return plane;
		}
		int variableStringNumber = new CmnFile(path).GetVariableStringNumber("Aircraft_Name_" + plane);
		if (variableStringNumber < 0)
		{
			return plane;
		}
		DatFile datFile = new DatFile(path2);
		if (variableStringNumber >= datFile.Strings.Count)
		{
			return plane;
		}
		string text = datFile.Strings[variableStringNumber].TrimEnd('\0').Trim();
		if (text.Length != 0)
		{
			return text;
		}
		return plane;
	}

	public static (string NameKey, string DescriptionKey) Keys(string plane, int skinNo)
	{
		string item = $"AircraftSkin_Description_{plane}_{skinNo:D2}";
		return (NameKey: (skinNo >= 6) ? $"AircraftSkintype_Name_{plane}_DLC{skinNo - 5}" : $"AircraftSkintype_Name_{plane}_{skinNo:D2}", DescriptionKey: item);
	}

	private static string ResolvePaks(string? paksDirectory)
	{
		if (!string.IsNullOrWhiteSpace(paksDirectory) && Directory.Exists(paksDirectory))
		{
			return paksDirectory;
		}
		if (Directory.Exists("D:\\SteamLibrary\\steamapps\\common\\ACE COMBAT 7\\Game\\Content\\Paks"))
		{
			return "D:\\SteamLibrary\\steamapps\\common\\ACE COMBAT 7\\Game\\Content\\Paks";
		}
		throw new DirectoryNotFoundException("The game Paks folder was not found. The pak name needs it to sort after the current SkinDataTable.");
	}

	private static int MountRank(string? paksDirectory)
	{
		if (!string.IsNullOrWhiteSpace(paksDirectory) && Directory.Exists(paksDirectory))
		{
			return Rank(paksDirectory);
		}
		if (Directory.Exists("D:\\SteamLibrary\\steamapps\\common\\ACE COMBAT 7\\Game\\Content\\Paks"))
		{
			return Rank("D:\\SteamLibrary\\steamapps\\common\\ACE COMBAT 7\\Game\\Content\\Paks");
		}
		return 1;
	}

	private static (int SkinId, int SortNumber, string RowName) WriteTable(string stageRoot, LocatedFile source, string plane, int skinNo, string pawnPath, Action<string>? log)
	{
		using OpenedTable openedTable = OpenTable(source.Path);
		UAsset asset = openedTable.Asset;
		List<StructPropertyData> rows = openedTable.Export.Table.Data;
		List<int> list = new List<int>();
		for (int i = 0; i < rows.Count; i++)
		{
			if (PlaneOf(asset, rows[i]).Equals(plane, StringComparison.OrdinalIgnoreCase))
			{
				list.Add(i);
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException("SkinDataTable has no " + plane + " row to copy.");
		}
		int num = list.FirstOrDefault((int index) => SkinNoOf(asset, rows[index]) == skinNo, -1);
		StructPropertyData structPropertyData;
		int skinId;
		if (num >= 0)
		{
			structPropertyData = rows[num];
			skinId = IntOf(asset, structPropertyData, "SkinID");
			log?.Invoke($"{plane} skin {skinNo:D2} is already {StringOf(asset, structPropertyData, "PlaneReference")} ({StringOf(asset, structPropertyData, "DLCID")}). That row now points at the new pawn.");
		}
		else
		{
			int num2 = list[list.Count - 1];
			StructPropertyData structPropertyData2 = rows[num2];
			for (skinId = IntOf(asset, structPropertyData2, "SkinID") + 1; rows.Any((StructPropertyData item) => IntOf(asset, item, "SkinID") == skinId || item.Name.ToString() == "Row_" + skinId); skinId++)
			{
			}
			structPropertyData = (StructPropertyData)structPropertyData2.Clone();
			structPropertyData.Name = FName.FromString(asset, "Row_" + skinId);
			rows.Insert(num2 + 1, structPropertyData);
			log?.Invoke($"Added {plane} skin {skinNo + 1} as Row_{skinId}, directly after {structPropertyData2.Name}.");
		}
		SetInt(asset, structPropertyData, "SkinID", skinId);
		SetInt(asset, structPropertyData, "SkinNo", skinNo);
		SetInt(asset, structPropertyData, "SortNumber", skinId);
		SetString(asset, structPropertyData, "PlaneStringID", plane);
		SetString(asset, structPropertyData, "PlaneReference", pawnPath);
		SetString(asset, structPropertyData, "DLCID", "Patch");
		string text = StageRelative(source.Path);
		string destUasset = Path.Combine(stageRoot, text.Replace('/', Path.DirectorySeparatorChar));
		WriteAsset(asset, destUasset, openedTable.Encrypted);
		log?.Invoke("Staged " + text);
		return (SkinId: skinId, SortNumber: skinId, RowName: "Row_" + skinId);
	}

	private static (string NameKey, string DescriptionKey) WriteNames(string stageRoot, LocatedLocalization source, string plane, int skinNo, string displayName, string description, IReadOnlyDictionary<char, SkinLocale>? languages, Action<string>? log)
	{
		(string, string) result = Keys(plane, skinNo);
		CmnFile cmnFile = new CmnFile(source.CmnPath);
		List<DatFile> list = new List<DatFile>();
		foreach (char item in DatConstants.DatLetters.Keys.OrderBy((char letter) => letter))
		{
			string text = Path.Combine(source.DirectoryPath, item + ".dat");
			if (!File.Exists(text))
			{
				throw new FileNotFoundException($"Localization is missing {item}.dat beside {source.CmnPath}.", text);
			}
			list.Add(new DatFile(text));
		}
		string englishDescription = string.IsNullOrWhiteSpace(description) ? displayName : description;
		PutText(cmnFile, list, result.Item1, displayName, Translation(languages, (SkinLocale locale) => locale.Name));
		PutText(cmnFile, list, result.Item2, englishDescription, Translation(languages, (SkinLocale locale) => locale.Description));
		string text2 = StageRelative(source.CmnPath);
		string path = Path.Combine(stageRoot, text2.Replace('/', Path.DirectorySeparatorChar));
		cmnFile.Write(path);
		foreach (DatFile item2 in list)
		{
			string text3 = StageRelative(Path.Combine(source.DirectoryPath, item2.Letter + ".dat"));
			item2.Write(Path.Combine(stageRoot, text3.Replace('/', Path.DirectorySeparatorChar)), item2.Letter);
		}
		log?.Invoke($"Staged {result.Item1} and {result.Item2} in {text2} and {list.Count} language files.");
		DatFile dat = list.First((DatFile datFile) => datFile.Letter == 'A');
		int variableStringNumber = cmnFile.GetVariableStringNumber(result.Item1);
		int variableStringNumber2 = cmnFile.GetVariableStringNumber(result.Item2);
		log?.Invoke("English name: " + TextAt(dat, variableStringNumber));
		log?.Invoke("English description: " + TextAt(dat, variableStringNumber2));
		foreach (DatFile item3 in list)
		{
			if (item3.Letter == 'A' || languages == null || !languages.TryGetValue(item3.Letter, out SkinLocale locale))
			{
				continue;
			}
			if (!string.IsNullOrWhiteSpace(locale.Name))
			{
				log?.Invoke(item3.Language + " name: " + locale.Name.Trim());
			}
			if (!string.IsNullOrWhiteSpace(locale.Description))
			{
				log?.Invoke(item3.Language + " description: " + locale.Description.Trim());
			}
		}
		return result;
	}

	private static Func<char, string?>? Translation(IReadOnlyDictionary<char, SkinLocale>? languages, Func<SkinLocale, string?> pick)
	{
		if (languages == null || languages.Count == 0)
		{
			return null;
		}
		return (char letter) =>
		{
			if (letter == 'A' || !languages.TryGetValue(letter, out SkinLocale locale))
			{
				return null;
			}
			string? text = pick(locale);
			return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
		};
	}

	private static string TextAt(DatFile dat, int index)
	{
		if (index < 0 || index >= dat.Strings.Count)
		{
			return "";
		}
		return dat.Strings[index].TrimEnd('\0');
	}

	private static void PutText(CmnFile cmn, IReadOnlyList<DatFile> dats, string key, string text, Func<char, string?>? translation)
	{
		string text2 = WithTerminator(text);
		int variableStringNumber;
		if (!cmn.AddVariable(key, cmn.Root, out variableStringNumber))
		{
			variableStringNumber = cmn.GetVariableStringNumber(key);
			if (variableStringNumber < 0)
			{
				throw new InvalidOperationException("Localization key " + key + " could not be added.");
			}
		}
		foreach (DatFile dat in dats)
		{
			Assign(dat, variableStringNumber, text2);
		}
		if (translation == null)
		{
			return;
		}
		foreach (DatFile dat2 in dats)
		{
			string? text3 = translation(dat2.Letter);
			if (text3 != null)
			{
				Assign(dat2, variableStringNumber, WithTerminator(text3));
			}
		}
	}

	private static string WithTerminator(string text)
	{
		string text2 = string.IsNullOrEmpty(text) ? "\0" : text;
		if (!text2.EndsWith('\0'))
		{
			text2 += "\0";
		}
		return text2;
	}

	private static void Assign(DatFile dat, int number, string text)
	{
		while (dat.Strings.Count <= number)
		{
			dat.Strings.Add("\0");
		}
		dat.Strings[number] = text;
	}

	private static OpenedTable OpenTable(string uassetPath)
	{
		byte[] array = new byte[4];
		using (FileStream fileStream = File.OpenRead(uassetPath))
		{
			if (fileStream.Read(array, 0, 4) < 4)
			{
				throw new InvalidDataException("SkinDataTable is empty: " + uassetPath);
			}
		}
		bool encrypted = AcePackage.IsEncryptedUAsset(array);
		(byte[] Asset, byte[]? Export) tuple = AcePackage.ReadPlain(uassetPath);
		byte[] item = tuple.Asset;
		byte[] item2 = tuple.Export;
		string text = Path.Combine(Path.GetTempPath(), "SkinSlotsTable_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "SkinDataTable.uasset");
		File.WriteAllBytes(path, item);
		if (item2 != null)
		{
			File.WriteAllBytes(Path.ChangeExtension(path, ".uexp"), item2);
		}
		UAsset uAsset = new UAsset(path, EngineVersion.VER_UE4_18);
		if (uAsset.Exports.Count == 0 || !(uAsset.Exports[0] is DataTableExport dataTableExport) || dataTableExport.Table?.Data == null)
		{
			throw new InvalidOperationException("SkinDataTable at " + uassetPath + " has no rows.");
		}
		return new OpenedTable
		{
			Asset = uAsset,
			Export = dataTableExport,
			Encrypted = encrypted,
			Temp = text
		};
	}

	private static void WriteAsset(UAsset asset, string destUasset, bool encrypt)
	{
		string text = Path.Combine(Path.GetTempPath(), "SkinSlotsWrite_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			string text2 = Path.Combine(text, "SkinDataTable.uasset");
			asset.Write(text2);
			byte[] plainAsset = File.ReadAllBytes(text2);
			string path = Path.ChangeExtension(text2, ".uexp");
			byte[] plainExport = (File.Exists(path) ? File.ReadAllBytes(path) : null);
			AcePackage.Commit(destUasset, plainAsset, plainExport, encrypt);
		}
		finally
		{
			TryDelete(text);
		}
	}

	private static string PlaneOf(UAsset asset, StructPropertyData row)
	{
		return StringOf(asset, row, "PlaneStringID");
	}

	private static int SkinNoOf(UAsset asset, StructPropertyData row)
	{
		return IntOf(asset, row, "SkinNo");
	}

	private static int IntOf(UAsset asset, StructPropertyData row, string name)
	{
		foreach (PropertyData item in row.Value)
		{
			if (!(item.Name?.Value?.Value != name) && item is IntPropertyData intPropertyData)
			{
				return intPropertyData.Value;
			}
		}
		throw new InvalidOperationException($"SkinDataTable row {row.Name} is missing {name}.");
	}

	private static string StringOf(UAsset asset, StructPropertyData row, string name)
	{
		foreach (PropertyData item in row.Value)
		{
			if (!(item.Name?.Value?.Value != name) && item is StrPropertyData strPropertyData)
			{
				return strPropertyData.Value?.Value ?? "";
			}
		}
		throw new InvalidOperationException($"SkinDataTable row {row.Name} is missing {name}.");
	}

	private static void SetInt(UAsset asset, StructPropertyData row, string name, int value)
	{
		foreach (PropertyData item in row.Value)
		{
			if (!(item.Name?.Value?.Value != name) && item is IntPropertyData intPropertyData)
			{
				intPropertyData.Value = value;
				return;
			}
		}
		throw new InvalidOperationException($"SkinDataTable row {row.Name} is missing {name}.");
	}

	private static void SetString(UAsset asset, StructPropertyData row, string name, string value)
	{
		foreach (PropertyData item in row.Value)
		{
			if (!(item.Name?.Value?.Value != name) && item is StrPropertyData strPropertyData)
			{
				strPropertyData.Value = new FString(value);
				return;
			}
		}
		throw new InvalidOperationException($"SkinDataTable row {row.Name} is missing {name}.");
	}

	private static string StageRelative(string sourceFile)
	{
		string text = sourceFile.Replace('\\', '/');
		int startIndex = 0;
		while ((startIndex = text.IndexOf("/Game/", startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
		{
			string text2 = text;
			int num = startIndex + "/Game/".Length;
			string text3 = text2.Substring(num, text2.Length - num);
			if (text3.StartsWith("Blueprint/", StringComparison.OrdinalIgnoreCase) || text3.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase) || text3.StartsWith("Vehicles/", StringComparison.OrdinalIgnoreCase))
			{
				return "Nimbus/Content/" + text3;
			}
			startIndex += "/Game/".Length;
		}
		List<string> list = new List<string> { Path.GetFileName(sourceFile) };
		string text4 = Path.GetDirectoryName(sourceFile);
		while (!string.IsNullOrEmpty(text4))
		{
			string fileName = Path.GetFileName(text4);
			if (fileName.Equals("Nimbus", StringComparison.OrdinalIgnoreCase))
			{
				list.Add("Nimbus");
				list.Reverse();
				return string.Join('/', list);
			}
			list.Add(fileName);
			string directoryName = Path.GetDirectoryName(text4);
			if (directoryName == text4)
			{
				break;
			}
			text4 = directoryName;
		}
		return "Nimbus/Content/Localization/Game/" + Path.GetFileName(sourceFile);
	}

	public static bool IsOwnDataPak(string path)
	{
		string text = Path.GetFileName(path);
		if (text.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			int length = ".disabled".Length;
			text = text2.Substring(0, text2.Length - length);
		}
		if (text.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 4);
		}
		text = text.TrimStart('~');
		if (text.StartsWith('!'))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		return text.Equals("SkinReSlotter_Data_P", StringComparison.OrdinalIgnoreCase);
	}

	private static string? LastPakContaining(string paksDirectory, string needle, bool enabledOnly, bool skipOwnDataPak = false)
	{
		if (!Directory.Exists(paksDirectory))
		{
			return null;
		}
		IEnumerable<string> enumerable = Directory.EnumerateFiles(paksDirectory, "*.pak", SearchOption.AllDirectories);
		if (!enabledOnly)
		{
			enumerable = enumerable.Concat(Directory.EnumerateFiles(paksDirectory, "*.pak.disabled", SearchOption.AllDirectories));
		}
		string result = null;
		foreach (string item in enumerable.OrderBy<string, string>((string file) => file, StringComparer.Ordinal))
		{
			if ((!skipOwnDataPak || !IsOwnDataPak(item)) && TailContains(item, needle))
			{
				result = item;
			}
		}
		return result;
	}

	private static bool TailContains(string path, string needle)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(needle);
		using FileStream fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		int num = (int)Math.Min(8388608L, fileStream.Length);
		if (num < bytes.Length)
		{
			return false;
		}
		fileStream.Seek(-num, SeekOrigin.End);
		byte[] array = new byte[num];
		int i;
		int num2;
		for (i = 0; i < num; i += num2)
		{
			num2 = fileStream.Read(array, i, num - i);
			if (num2 <= 0)
			{
				break;
			}
		}
		for (int j = 0; j <= i - bytes.Length; j++)
		{
			bool flag = true;
			for (int k = 0; k < bytes.Length; k++)
			{
				if (array[j + k] != bytes[k])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	private static int LeadingTildes(string? pakPath)
	{
		if (string.IsNullOrEmpty(pakPath))
		{
			return 0;
		}
		string? fileName = Path.GetFileName(pakPath);
		int num = 0;
		string text = fileName;
		for (int i = 0; i < text.Length && text[i] == '~'; i++)
		{
			num++;
		}
		return num;
	}

	private static void TryDelete(string directory)
	{
		try
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
		catch
		{
		}
	}
}
