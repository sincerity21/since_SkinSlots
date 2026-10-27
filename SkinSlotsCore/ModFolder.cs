using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkinConverterCore.Services;

namespace SkinSlotsCore;

public static class ModFolder
{
	private static readonly Dictionary<string, (long Ticks, string Directory)> Extracts = new Dictionary<string, (long, string)>(StringComparer.OrdinalIgnoreCase);

	public static ModFolderScan Scan(string? modsDirectory)
	{
		if (string.IsNullOrWhiteSpace(modsDirectory) || !Directory.Exists(modsDirectory))
		{
			return new ModFolderScan();
		}
		string fullPath = Path.GetFullPath(modsDirectory);
		string text = FindDataPak(fullPath);
		return new ModFolderScan
		{
			Directory = fullPath,
			DataPak = text,
			SkinTablePak = (text ?? MountedSkin.WinningPak(fullPath, "SkinDataTable.uasset")),
			LocalizationPak = (text ?? MountedSkin.WinningPak(fullPath, "Cmn.dat"))
		};
	}

	public static void WriteMarker(string stageRoot)
	{
		string path = Path.Combine(stageRoot, "Nimbus", "SkinReSlotter.marker");
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, "since.SkinReSlotter.DataPak.1" + Environment.NewLine);
	}

	public static bool MarkerIsValid(string root)
	{
		if (!Directory.Exists(root))
		{
			return false;
		}
		foreach (string item in Directory.EnumerateFiles(root, "SkinReSlotter.marker", SearchOption.AllDirectories))
		{
			if (File.ReadAllText(item).Contains("since.SkinReSlotter.DataPak.1", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public static string? FindDataPak(string? directory)
	{
		if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
		{
			return null;
		}
		return MountedSkin.WinningPak(directory, "SkinReSlotter.marker");
	}

	public static InheritedSources Open(string? modsDirectory, string pawnRoot, string? unrealPakExe, Action<string>? log)
	{
		ModFolderScan modFolderScan = Scan(modsDirectory);
		string text = PawnLibrary.StockSkinTable(pawnRoot);
		string text2 = PawnLibrary.StockLocalizationDirectory(pawnRoot);
		string text3 = null;
		string text4 = null;
		string text5 = null;
		if (modFolderScan.DataPak != null && MarkerConfirmed(modFolderScan.DataPak, unrealPakExe, log))
		{
			string root = Extract(modFolderScan.DataPak, unrealPakExe, log);
			text = Find(root, "SkinDataTable.uasset");
			if (!File.Exists(Path.ChangeExtension(text, ".uexp")))
			{
				throw new FileNotFoundException("SkinDataTable in " + Path.GetFileName(modFolderScan.DataPak) + " is missing its .uexp.", text);
			}
			text2 = Path.GetDirectoryName(Find(root, "Cmn.dat")) ?? text2;
			text3 = modFolderScan.DataPak;
			text4 = modFolderScan.DataPak;
			text5 = modFolderScan.DataPak;
			log?.Invoke("SkinReSlotter data pak: " + Path.GetFileName(modFolderScan.DataPak) + ". The table and the names will be written back under this name.");
		}
		else if (modFolderScan.SkinTablePak != null)
		{
			text = Find(Extract(modFolderScan.SkinTablePak, unrealPakExe, log), "SkinDataTable.uasset");
			if (!File.Exists(Path.ChangeExtension(text, ".uexp")))
			{
				throw new FileNotFoundException("SkinDataTable in " + Path.GetFileName(modFolderScan.SkinTablePak) + " is missing its .uexp.", text);
			}
			text3 = modFolderScan.SkinTablePak;
			log?.Invoke("SkinDataTable inherited from " + Path.GetFileName(modFolderScan.SkinTablePak));
		}
		else
		{
			log?.Invoke("No SkinDataTable in the mods folder. Using the saved game table.");
		}
		if (text5 == null && modFolderScan.LocalizationPak != null)
		{
			text2 = Path.GetDirectoryName(Find(Extract(modFolderScan.LocalizationPak, unrealPakExe, log), "Cmn.dat")) ?? text2;
			text4 = modFolderScan.LocalizationPak;
			log?.Invoke("Localization inherited from " + Path.GetFileName(modFolderScan.LocalizationPak));
		}
		else if (text5 == null)
		{
			log?.Invoke("No localization in the mods folder. Using the saved game files.");
		}
		return new InheritedSources
		{
			TablePath = text,
			LocalizationDirectory = text2,
			TableFromMods = (text3 != null),
			LocalizationFromMods = (text4 != null),
			TablePak = text3,
			LocalizationPak = text4,
			DataPakPath = text5
		};
	}

	private static bool MarkerConfirmed(string pakPath, string? unrealPakExe, Action<string>? log)
	{
		if (MarkerIsValid(Extract(pakPath, unrealPakExe, log)))
		{
			return true;
		}
		log?.Invoke(Path.GetFileName(pakPath) + " names SkinReSlotter.marker, but the marker inside is not the SkinReSlotter data pak.");
		return false;
	}

	public static string DataPakFileName(int tildes)
	{
		if (tildes < 1)
		{
			tildes = 1;
		}
		return new string('~', tildes) + "!SkinReSlotter_Data_P.pak";
	}

	public static string? FindPak(string directory, string fileName)
	{
		if (!Directory.Exists(directory))
		{
			return null;
		}
		foreach (string item in Directory.EnumerateFiles(directory, "*.pak", SearchOption.AllDirectories))
		{
			if (Path.GetFileName(item).Equals(fileName, StringComparison.OrdinalIgnoreCase))
			{
				return item;
			}
		}
		return null;
	}

	public static void RemoveOlderDataPaks(string directory, string keepPath)
	{
		if (!Directory.Exists(directory))
		{
			return;
		}
		string fullPath = Path.GetFullPath(keepPath);
		HashSet<string> hashSet = new HashSet<string>(MountedSkin.PaksContaining(directory, "SkinReSlotter.marker", enabledOnly: false).Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
		foreach (string item in Directory.EnumerateFiles(directory, "*.pak", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(directory, "*.pak.disabled", SearchOption.AllDirectories)))
		{
			if (!Path.GetFullPath(item).Equals(fullPath, StringComparison.OrdinalIgnoreCase) && (MountedSkin.IsOwnDataPak(item) || hashSet.Contains(Path.GetFullPath(item))))
			{
				File.Delete(item);
			}
		}
	}

	public static void Invalidate(string? pakPath)
	{
		if (string.IsNullOrWhiteSpace(pakPath))
		{
			return;
		}
		string item;
		lock (Extracts)
		{
			if (!Extracts.Remove(Path.GetFullPath(pakPath), out (long, string) value))
			{
				return;
			}
			item = value.Item2;
		}
		if (!string.IsNullOrEmpty(item))
		{
			TryDelete(item);
		}
	}

	private static string Extract(string pakPath, string? unrealPakExe, Action<string>? log)
	{
		string fullPath = Path.GetFullPath(pakPath);
		long ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
		lock (Extracts)
		{
			if (Extracts.TryGetValue(fullPath, out (long, string) value) && value.Item1 == ticks && Directory.Exists(value.Item2))
			{
				return value.Item2;
			}
		}
		if (unrealPakExe == null || !File.Exists(unrealPakExe))
		{
			throw new FileNotFoundException("UnrealPak.exe is required to read a mod SkinDataTable or localization.", unrealPakExe);
		}
		string text = Path.Combine(Path.GetTempPath(), "SkinSlotsMods_" + Guid.NewGuid().ToString("N"));
		PakService.ExtractPak(unrealPakExe, fullPath, text, log);
		string text2 = null;
		lock (Extracts)
		{
			if (Extracts.TryGetValue(fullPath, out (long, string) value2))
			{
				text2 = value2.Item2;
			}
			Extracts[fullPath] = (ticks, text);
		}
		if (text2 != null && !text2.Equals(text, StringComparison.OrdinalIgnoreCase))
		{
			TryDelete(text2);
		}
		return text;
	}

	private static string Find(string root, string fileName)
	{
		List<string> list = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).ToList();
		if (list.Count == 0)
		{
			throw new FileNotFoundException("Extracted pak has no " + fileName + ".", root);
		}
		return list.OrderByDescending((string path) => path.Contains("Information", StringComparison.OrdinalIgnoreCase) || path.Contains("Localization", StringComparison.OrdinalIgnoreCase)).First();
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
