using System.Text;
using SkinConverterCore.Services;

namespace SkinSlotsCore;

public static class StringRewriter
{
    public static byte[] Rewrite(byte[] data, Func<string, string?> map)
    {
        var edits = new List<(int Start, int Length, byte[] Bytes)>();
        var index = 0;
        while (index < data.Length)
        {
            if (data[index] < 32 || data[index] > 126)
            {
                index++;
                continue;
            }

            var start = index;
            while (index < data.Length && data[index] >= 32 && data[index] <= 126)
                index++;

            var length = index - start;
            if (length < 4)
                continue;

            var text = Encoding.ASCII.GetString(data, start, length);
            var updated = map(text);
            if (updated == null || updated == text)
                continue;

            if (updated.Length != text.Length)
            {
                throw new InvalidOperationException(
                    $"Retarget changed the length of '{text}' to '{updated}'. Two-digit slots keep the name map in place, so this string cannot be patched in the cooked asset.");
            }

            edits.Add((start, length, Encoding.ASCII.GetBytes(updated)));
        }

        if (edits.Count == 0)
            return data;

        var copy = (byte[])data.Clone();
        foreach (var (start, length, bytes) in edits)
            Buffer.BlockCopy(bytes, 0, copy, start, length);
        return copy;
    }

    public static string? RetargetMaterialText(string text, string plane, string sourceSlot, string destSlot)
    {
        var updated = SkinSlotConverter.TransformSymbolString(text, plane, sourceSlot, destSlot);
        return updated == text ? null : updated;
    }

    public static bool TryMaterialRole(string text, string plane, string slot, out string role)
    {
        role = "";
        var token = plane + "_" + slot + "_";
        var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        role = text[(index + token.Length)..];
        var cut = role.IndexOfAny(['/', '\\', '.', ' ']);
        if (cut >= 0)
            role = role[..cut];
        return role.Length > 0;
    }

    public static string RetargetFileName(string fileName, string plane, string sourceSlot, string destSlot)
    {
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var updated = SkinSlotConverter.TransformSymbolString(stem, plane, sourceSlot, destSlot);
        return updated + extension;
    }
}
