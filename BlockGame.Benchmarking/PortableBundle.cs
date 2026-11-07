using System.IO.Compression;
using System.Security.Cryptography;

namespace BlockGame.Benchmarking;

public static class PortableBundle
{
    public static string Pack(string runDirectory, string zipPath)
    {
        ZipFile.CreateFromDirectory(runDirectory, zipPath);
        using var zip = File.OpenRead(zipPath);
        string hash = Convert.ToHexStringLower(SHA256.HashData(zip));
        File.WriteAllText(zipPath + ".sha256", $"{hash}  {Path.GetFileName(zipPath)}{Environment.NewLine}");
        return zipPath;
    }

    public static void Verify(string zipPath)
    {
        string hashPath = zipPath + ".sha256";
        if (!File.Exists(hashPath))
            throw new InvalidDataException("bundle hash file missing");

        string expected = File.ReadAllText(hashPath).Split(' ', 2)[0].Trim();
        using var zip = File.OpenRead(zipPath);
        string actual = Convert.ToHexStringLower(SHA256.HashData(zip));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("bundle hash mismatch");
    }
}
