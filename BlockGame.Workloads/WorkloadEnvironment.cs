using System.Security.Cryptography;
using System.Text;
using Basics.Game.Configurations;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.Logic.TerrainManaging.Generation.Noise;

namespace BlockGame.Workloads;

/// <summary>Fixture-owned dependencies, never shared across contexts or benchmark cases.</summary>
public sealed class WorkloadEnvironment : IDisposable
{
    public string ContentRoot { get; }
    public BlockRegistry Registry { get; } = new();
    public NoiseCalculator Noise { get; }
    public TerrainGenerator Generator { get; }
    public uint FeatureSet => Noise.ActiveFeatureSet;
    public static string NativePath => Path.Combine(AppContext.BaseDirectory, "FastNoise.dll");

    public WorkloadEnvironment(int seed = 1)
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("Workloads require Windows x64.");
        if (!File.Exists(NativePath)) throw new FileNotFoundException("Required native binary missing", NativePath);
        // Explicit app-local content: never accept registry fallback or a different working directory.
        ContentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        foreach (string name in WorkloadCatalog.BlockNames.Skip(1))
            if (!Directory.Exists(ContentRoot) || !Directory.EnumerateFiles(ContentRoot, name.Split(':')[1] + ".json", SearchOption.AllDirectories).Any())
                throw new FileNotFoundException($"Required content missing: {name}", ContentRoot);
        foreach (string texture in new[] { "dirt", "dirt-grass", "grass", "snow", "stone" })
            if (!Directory.EnumerateFiles(ContentRoot, texture + ".png", SearchOption.AllDirectories).Any())
                throw new FileNotFoundException($"Required texture missing: {texture}", ContentRoot);
        Registry.InitializeHeadless(ContentRoot);
        var settings = WorkloadCatalog.Settings(seed);
        Noise = new NoiseCalculator(settings);
        Generator = new TerrainGenerator(Noise, settings, Registry);
    }

    public object Identity() => new
    {
        nativeSha256 = Hash(File.ReadAllBytes(NativePath)), featureSet = FeatureSet,
        nodeTree = WorkloadCatalog.NodeTree, nodeTreeSha256 = Hash(Encoding.UTF8.GetBytes(WorkloadCatalog.NodeTree)),
        content = Directory.EnumerateFiles(ContentRoot, "*", SearchOption.AllDirectories)
            .OrderBy(p => Path.GetRelativePath(ContentRoot, p), StringComparer.Ordinal)
            .Select(p => new { path = Path.GetRelativePath(ContentRoot, p).Replace('\\', '/'), sha256 = Hash(File.ReadAllBytes(p)) }).ToArray(),
        blocks = WorkloadCatalog.BlockNames.Select(name => new { name, id = Registry.GetBlockId(name), layers = Enumerable.Range(0, 6).Select(f => Registry.Get(Registry.GetBlockId(name), f)).ToArray() }).ToArray()
    };

    public static string Hash(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));
    public void Dispose() { Noise.Dispose(); Registry.Dispose(); }
}
