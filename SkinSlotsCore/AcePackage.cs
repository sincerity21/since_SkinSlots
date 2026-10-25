using UAssetAPI;

namespace SkinSlotsCore;

/// <summary>
/// Ace Combat 7 packages are XOR-encrypted under the file name.
/// The .uexp continues the same XOR stream as the .uasset, so one key has to walk both files in order.
/// </summary>
public static class AcePackage
{
    public static bool IsEncryptedUAsset(ReadOnlySpan<byte> data)
    {
        return data.Length >= 4 && BitConverter.ToUInt32(data) == UAsset.ACE7_MAGIC;
    }

    public static (byte[] Asset, byte[]? Export) ReadPlain(string uassetPath)
    {
        var asset = File.ReadAllBytes(uassetPath);
        var exportPath = Path.ChangeExtension(uassetPath, ".uexp");
        byte[]? export = File.Exists(exportPath) ? File.ReadAllBytes(exportPath) : null;
        if (!IsEncryptedUAsset(asset))
            return (asset, export);

        var decrypt = new AC7Decrypt();
        var key = new AC7XorKey(Path.GetFileNameWithoutExtension(uassetPath));
        var plainAsset = decrypt.DecryptUAssetBytes(asset, key);
        byte[]? plainExport = export == null ? null : decrypt.DecryptUexpBytes(export, key);
        return (plainAsset, plainExport);
    }

    public static void WriteEncrypted(string uassetPath, byte[] plainAsset, byte[]? plainExport)
    {
        var directory = Path.GetDirectoryName(uassetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var decrypt = new AC7Decrypt();
        var key = new AC7XorKey(Path.GetFileNameWithoutExtension(uassetPath));
        File.WriteAllBytes(uassetPath, decrypt.EncryptUAssetBytes(plainAsset, key));
        if (plainExport != null)
            File.WriteAllBytes(Path.ChangeExtension(uassetPath, ".uexp"), decrypt.EncryptUexpBytes(plainExport, key));
    }
}
