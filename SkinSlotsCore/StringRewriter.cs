using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkinConverterCore.Services;

namespace SkinSlotsCore;

public static class StringRewriter
{
	public static byte[] Rewrite(byte[] data, Func<string, string?> map)
	{
		if (AcePackage.IsEncryptedUAsset(data))
		{
			throw new InvalidOperationException("The asset is still encrypted. Decrypt it before editing.");
		}
		List<(int, int, byte[])> list = new List<(int, int, byte[])>();
		int i = 0;
		while (i < data.Length)
		{
			if (data[i] < 32 || data[i] > 126)
			{
				i++;
				continue;
			}
			int num = i;
			for (; i < data.Length && data[i] >= 32 && data[i] <= 126; i++)
			{
			}
			int num2 = i - num;
			if (num2 < 4)
			{
				continue;
			}
			string text = Encoding.ASCII.GetString(data, num, num2);
			string text2 = map(text);
			if (text2 != null && !(text2 == text))
			{
				if (text2.Length != text.Length)
				{
					throw new InvalidOperationException($"Retarget changed the length of '{text}' to '{text2}'. Two-digit slots keep the name map in place, so this string cannot be patched in the cooked asset.");
				}
				list.Add((num, num2, Encoding.ASCII.GetBytes(text2)));
			}
		}
		if (list.Count == 0)
		{
			return data;
		}
		byte[] array = (byte[])data.Clone();
		foreach (var item in list)
		{
			Buffer.BlockCopy(dstOffset: item.Item1, count: item.Item2, src: item.Item3, srcOffset: 0, dst: array);
		}
		return array;
	}

	public static string? RetargetMaterialText(string text, string plane, string sourceSlot, string destSlot)
	{
		string text2 = SkinSlotConverter.TransformSymbolString(text, plane, sourceSlot, destSlot);
		if (!(text2 == text))
		{
			return text2;
		}
		return null;
	}

	public static bool TryMaterialRole(string text, string plane, string slot, out string role)
	{
		role = "";
		string text2 = plane + "_" + slot + "_";
		int num = text.IndexOf(text2, StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return false;
		}
		int num2 = num + text2.Length;
		role = text.Substring(num2, text.Length - num2);
		int num3 = role.IndexOfAny(new char[4] { '/', '\\', '.', ' ' });
		if (num3 >= 0)
		{
			role = role.Substring(0, num3);
		}
		return role.Length > 0;
	}

	public static string RetargetFileName(string fileName, string plane, string sourceSlot, string destSlot)
	{
		string extension = Path.GetExtension(fileName);
		string name = SkinSlotConverter.TransformSymbolString(Path.GetFileNameWithoutExtension(fileName), plane, sourceSlot, destSlot);
		name = name.Replace(plane + "_" + sourceSlot + "_Dam_Inst", plane + "_" + destSlot + "_Dam_Inst", StringComparison.OrdinalIgnoreCase);
		return name + extension;
	}
}
