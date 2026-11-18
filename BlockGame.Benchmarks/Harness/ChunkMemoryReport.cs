using Basics.Common;
using Basics.Terrain;
using BlockGame.Benchmarks.Workloads;
using System.Security.Cryptography;
using System.Text;

namespace BlockGame.Benchmarks.Harness;

/// <summary>Retained block storage census; deliberately not a timing benchmark.</summary>
internal static class ChunkMemoryReport
{
    public static int Run(string[] args)
    {
        if (args.Length != 3 || args[1] != "--output")
            throw new ArgumentException("chunk-memory --output <new-directory>");
        string output = Path.GetFullPath(args[2]);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Output must be new or empty");
        Directory.CreateDirectory(output);
        using var environment = new WorkloadEnvironment();
        var rows = new List<object>();
        foreach (var (name, center) in new (string, ChunkCoord)[] {
            ("spawn", new(0,9,0,0)), ("surface", new(0,-58,0,0)), ("deep", new(0,-72,0,0)) })
        {
            ChunkCoord[] coords = WorkloadCatalog.Select(center, 12, 12)
                .OrderBy(c=>c.X).ThenBy(c=>c.Y).ThenBy(c=>c.Z).ToArray();
            // Warm native generation and pooled scratch before counting retained storage.
            environment.Generator.GenerateChunk(center);
            var chunks = new ChunkData[coords.Length];
            long before = GC.GetTotalMemory(true);
            for (int i=0;i<coords.Length;i++) chunks[i]=environment.Generator.GenerateChunk(coords[i]);
            long retained = GC.GetTotalMemory(true)-before;
            long payload = 0; int arrays = 0, air = 0, uniform = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var chunk in chunks)
            {
                if (chunk.Blocks is {} blocks) { arrays++; payload += blocks.LongLength * sizeof(ushort); }
                if (chunk.IsEmpty()) air++;
                ushort first = chunk.GetBlock(0,0,0);
                if (chunk.Blocks is null || chunk.Blocks.All(b=>b==first)) uniform++;
                hash.AppendData(Encoding.ASCII.GetBytes(OutputChecks.Terrain(chunk)));
            }
            rows.Add(new { name, center, radius=12, count=chunks.Length, arrays, air, uniform,
                blockPayloadBytes=payload, retainedManagedBytes=retained,
                logicalFingerprint=Convert.ToHexString(hash.GetHashAndReset()) });
            GC.KeepAlive(chunks);
        }
        RunEvidence.WriteJson(Path.Combine(output,"chunk-memory.json"),new {
            version=1, source=RunEvidence.SourceIdentity(), host=RunEvidence.RuntimeIdentity(),
            dependencies=environment.Identity(), rows,
            limitation="Headless retained generated chunks only: excludes provider, mesh buffers, GPU and process RSS. GC delta is approximate; block payload is exact."
        });
        Console.WriteLine(File.ReadAllText(Path.Combine(output,"chunk-memory.json")));
        return 0;
    }
}
