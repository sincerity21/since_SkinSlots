using System;
using System.Collections.Generic;
using System.IO;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace SkinSlotsCore;

public static class AcePackage
{
	public static bool IsEncryptedUAsset(ReadOnlySpan<byte> data)
	{
		if (data.Length >= 4)
		{
			return BitConverter.ToUInt32(data) == UAsset.ACE7_MAGIC;
		}
		return false;
	}

	public static (byte[] Asset, byte[]? Export) ReadPlain(string uassetPath)
	{
		byte[] array = File.ReadAllBytes(uassetPath);
		string path = Path.ChangeExtension(uassetPath, ".uexp");
		byte[] array2 = (File.Exists(path) ? File.ReadAllBytes(path) : null);
		if (!IsEncryptedUAsset(array))
		{
			return (Asset: array, Export: array2);
		}
		AC7Decrypt aC7Decrypt = new AC7Decrypt();
		AC7XorKey xorkey = new AC7XorKey(Path.GetFileNameWithoutExtension(uassetPath));
		byte[] item = aC7Decrypt.DecryptUAssetBytes(array, xorkey);
		byte[] item2 = ((array2 == null) ? null : aC7Decrypt.DecryptUexpBytes(array2, xorkey));
		return (Asset: item, Export: item2);
	}

	public static void WriteEncrypted(string uassetPath, byte[] plainAsset, byte[]? plainExport, Action<UAsset>? adjust = null)
	{
		Commit(uassetPath, plainAsset, plainExport, encrypt: true, adjust);
	}

	public static void WriteEncryptedBytes(string uassetPath, byte[] plainAsset, byte[]? plainExport)
	{
		WriteRaw(uassetPath, plainAsset, plainExport, encrypt: true);
	}

	public static void WritePlain(string uassetPath, byte[] plainAsset, byte[]? plainExport, Action<UAsset>? adjust = null)
	{
		Commit(uassetPath, plainAsset, plainExport, encrypt: false, adjust);
	}

	public static void WritePlainBytes(string uassetPath, byte[] plainAsset, byte[]? plainExport)
	{
		WriteRaw(uassetPath, plainAsset, plainExport, encrypt: false);
	}

	private static void WriteRaw(string uassetPath, byte[] plainAsset, byte[]? plainExport, bool encrypt)
	{
		string? directoryName = Path.GetDirectoryName(uassetPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		if (encrypt)
		{
			AC7Decrypt decrypt = new AC7Decrypt();
			AC7XorKey key = new AC7XorKey(Path.GetFileNameWithoutExtension(uassetPath));
			plainAsset = decrypt.EncryptUAssetBytes(plainAsset, key);
			if (plainExport != null)
			{
				plainExport = decrypt.EncryptUexpBytes(plainExport, key);
			}
		}
		File.WriteAllBytes(uassetPath, plainAsset);
		if (plainExport != null)
		{
			File.WriteAllBytes(Path.ChangeExtension(uassetPath, ".uexp"), plainExport);
		}
	}

	public static void Commit(string uassetPath, byte[] plainAsset, byte[]? plainExport, bool encrypt, Action<UAsset>? adjust = null)
	{
		string directoryName = Path.GetDirectoryName(uassetPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string text = Path.Combine(Path.GetTempPath(), "SkinSlotsAsset_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			string text2 = Path.Combine(text, Path.GetFileName(uassetPath));
			File.WriteAllBytes(text2, plainAsset);
			if (plainExport != null)
			{
				File.WriteAllBytes(Path.ChangeExtension(text2, ".uexp"), plainExport);
			}
			Reserialize(text2, uassetPath, adjust);
			plainAsset = File.ReadAllBytes(text2);
			string path = Path.ChangeExtension(text2, ".uexp");
			plainExport = (File.Exists(path) ? File.ReadAllBytes(path) : null);
		}
		finally
		{
			try
			{
				Directory.Delete(text, recursive: true);
			}
			catch
			{
			}
		}
		if (encrypt)
		{
			AC7Decrypt aC7Decrypt = new AC7Decrypt();
			AC7XorKey xorkey = new AC7XorKey(Path.GetFileNameWithoutExtension(uassetPath));
			File.WriteAllBytes(uassetPath, aC7Decrypt.EncryptUAssetBytes(plainAsset, xorkey));
			if (plainExport != null)
			{
				File.WriteAllBytes(Path.ChangeExtension(uassetPath, ".uexp"), aC7Decrypt.EncryptUexpBytes(plainExport, xorkey));
			}
		}
		else
		{
			File.WriteAllBytes(uassetPath, plainAsset);
			if (plainExport != null)
			{
				File.WriteAllBytes(Path.ChangeExtension(uassetPath, ".uexp"), plainExport);
			}
		}
	}

	public static void WriteRetargetedTexture(string sourceUasset, string destUasset, string plane, string sourceSlot, string destSlot)
	{
		(byte[] Asset, byte[]? Export) plain = ReadPlain(sourceUasset);
		string temp = Path.Combine(Path.GetTempPath(), "SkinSlotsTex_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(temp);
		try
		{
			string tempUasset = Path.Combine(temp, Path.GetFileName(sourceUasset));
			File.WriteAllBytes(tempUasset, plain.Asset);
			if (plain.Export != null)
			{
				File.WriteAllBytes(Path.ChangeExtension(tempUasset, ".uexp"), plain.Export);
			}
			UAsset asset = new UAsset(tempUasset, EngineVersion.VER_UE4_18);
			if (!sourceSlot.Equals(destSlot, StringComparison.OrdinalIgnoreCase))
			{
				ReplaceNameText(asset, plane + "/" + sourceSlot + "/", plane + "/" + destSlot + "/");
				ReplaceNameText(asset, plane + "_" + sourceSlot + "_", plane + "_" + destSlot + "_");
			}
			string? directoryName = Path.GetDirectoryName(destUasset);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			asset.Write(destUasset);
		}
		finally
		{
			try
			{
				Directory.Delete(temp, recursive: true);
			}
			catch
			{
			}
		}
	}

	public static void ReplaceNameText(UAsset asset, string from, string to)
	{
		IReadOnlyList<FString> nameMapIndexList = asset.GetNameMapIndexList();
		for (int i = 0; i < nameMapIndexList.Count; i++)
		{
			string value = nameMapIndexList[i].Value;
			if (value.Contains(from, StringComparison.Ordinal))
			{
				asset.SetNameReference(i, FString.FromString(value.Replace(from, to, StringComparison.Ordinal)));
			}
		}
	}

	private static void Reserialize(string plainUasset, string destUasset, Action<UAsset>? adjust)
	{
		byte[] header = new byte[4];
		using (FileStream stream = File.OpenRead(plainUasset))
		{
			stream.Read(header, 0, 4);
		}
		if (IsEncryptedUAsset(header))
		{
			throw new InvalidOperationException(Path.GetFileName(plainUasset) + " is still encrypted. Decrypt it before editing.");
		}
		UAsset uAsset = new UAsset(plainUasset, EngineVersion.VER_UE4_18);
		AlignPackageName(uAsset, destUasset);
		adjust?.Invoke(uAsset);
		uAsset.Write(plainUasset);
		if (!new UAsset(plainUasset, EngineVersion.VER_UE4_18).VerifyBinaryEquality())
		{
			throw new InvalidOperationException(Path.GetFileName(plainUasset) + " still fails UAssetAPI binary equality after it was written.");
		}
	}

	private static void AlignPackageName(UAsset asset, string destUasset)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destUasset);
		if (KeepsShortPackageName(fileNameWithoutExtension))
		{
			return;
		}
		string value = "/Game/" + fileNameWithoutExtension;
		FString search = FString.FromString(value);
		if (asset.ContainsNameReference(search))
		{
			string text = GamePackage(destUasset);
			if (text != null && !text.Equals(value, StringComparison.Ordinal))
			{
				asset.SetNameReference(asset.SearchNameReference(search), FString.FromString(text));
			}
		}
	}

	private static bool KeepsShortPackageName(string name)
	{
		return name.EndsWith("_D", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_MREC", StringComparison.OrdinalIgnoreCase) || name.EndsWith("xMREC", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_N", StringComparison.OrdinalIgnoreCase);
	}

	private static string? GamePackage(string uassetPath)
	{
		string text = uassetPath.Replace('\\', '/');
		int num = text.IndexOf("/Nimbus/Content/", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return null;
		}
		string text2 = text;
		int num2 = num + "/Nimbus/Content/".Length;
		string text3 = text2.Substring(num2, text2.Length - num2);
		if (!text3.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		text2 = text3;
		num2 = ".uasset".Length;
		return "/Game/" + text2.Substring(0, text2.Length - num2);
	}
}
