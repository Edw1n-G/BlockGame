using System.IO.Compression;
using System.Text.Json;

namespace BlockGame.Benchmarking;

public sealed record BundleFile(string Path, long Bytes, string Sha256);
public sealed record BundleIndex(int SchemaVersion, string RunPath, BundleFile[] Files);

public static class PortableBundle
{
    public static void Seal(string directory)
    {
        directory = Path.GetFullPath(directory);
        string run = File.Exists(Path.Combine(directory,"benchmarks/benchmark-run.json")) ? "benchmarks/benchmark-run.json" : "benchmark-run.json";
        if (!File.Exists(Path.Combine(directory,run))) throw new InvalidDataException("Missing normalized run evidence");
        var files = SafeFiles(directory).Where(p => Path.GetRelativePath(directory,p) != "bundle.json")
            .Select(p => {
                if ((File.GetAttributes(p)&FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links cannot be bundled");
                return new BundleFile(Path.GetRelativePath(directory,p).Replace('\\','/'),new FileInfo(p).Length,EvidenceJson.Hash(File.ReadAllBytes(p)));
            }).OrderBy(f => f.Path,StringComparer.Ordinal).ToArray();
        EvidenceJson.Write(Path.Combine(directory,"bundle.json"),new BundleIndex(1,run,files));
        Verify(directory);
    }

    public static BenchmarkRun Verify(string path)
    {
        if (Directory.Exists(path))
        {
            var index = EvidenceJson.Read<BundleIndex>(Path.Combine(path,"bundle.json"));
            ValidateIndex(index);
            var actual = SafeFiles(path).Select(p => Path.GetRelativePath(path,p).Replace('\\','/')).Where(p => p != "bundle.json").Order(StringComparer.Ordinal);
            if (!actual.SequenceEqual(index.Files.Select(f => f.Path).Order(StringComparer.Ordinal))) throw new InvalidDataException("Unlisted/missing bundle files");
            foreach (var file in index.Files)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(path,file.Path));
                Check(file,bytes);
            }
            return EvidenceJson.Read<BenchmarkRun>(Path.Combine(path,index.RunPath));
        }
        using var zip = ZipFile.OpenRead(path);
        var entries = zip.Entries.Where(e => e.Name.Length > 0).ToArray();
        if (entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != entries.Length) throw new InvalidDataException("Duplicate ZIP entries");
        byte[] Read(string name) { using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException("Missing ZIP entry")).Open(); using var data = new MemoryStream(); stream.CopyTo(data); return data.ToArray(); }
        var bundle = JsonSerializer.Deserialize<BundleIndex>(Read("bundle.json"),EvidenceJson.Options) ?? throw new InvalidDataException("Missing index");
        ValidateIndex(bundle);
        if (!entries.Select(e => e.FullName).Where(n => n != "bundle.json").Order(StringComparer.Ordinal).SequenceEqual(bundle.Files.Select(f => f.Path).Order(StringComparer.Ordinal))) throw new InvalidDataException("Unlisted/missing ZIP entries");
        foreach (var file in bundle.Files) Check(file,Read(file.Path));
        return JsonSerializer.Deserialize<BenchmarkRun>(Read(bundle.RunPath),EvidenceJson.Options) ?? throw new InvalidDataException("Missing run");
    }

    public static void Pack(string directory, string zipPath)
    {
        if (Path.GetFullPath(zipPath).StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("ZIP must be outside bundle");
        Seal(directory);
        ZipFile.CreateFromDirectory(directory,zipPath);
        Verify(zipPath);
        File.WriteAllText(zipPath+".sha256",EvidenceJson.Hash(File.ReadAllBytes(zipPath))+"  "+Path.GetFileName(zipPath)+Environment.NewLine);
    }
    private static void Check(BundleFile file, byte[] data)
    { if (data.LongLength != file.Bytes || EvidenceJson.Hash(data) != file.Sha256) throw new InvalidDataException("Bundle hash mismatch: "+file.Path); }
    private static void ValidateIndex(BundleIndex index)
    {
        if (index.SchemaVersion != 1 || index.Files.Length == 0 || index.Files.Select(f => f.Path).Distinct().Count() != index.Files.Length || !index.Files.Any(f => f.Path == index.RunPath)) throw new InvalidDataException("Invalid bundle inventory");
        foreach (string path in index.Files.Select(f => f.Path).Append(index.RunPath))
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(s => s is "" or "." or "..") || path.Contains(':')) throw new InvalidDataException("Unsafe bundle path");
    }
    private static IEnumerable<string> SafeFiles(string directory)
    {
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bundle links are not allowed");
        foreach(string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes=File.GetAttributes(entry);
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bundle links are not allowed");
            if((attributes&FileAttributes.Directory)!=0)foreach(string file in SafeFiles(entry))yield return file;
            else yield return entry;
        }
    }
}
