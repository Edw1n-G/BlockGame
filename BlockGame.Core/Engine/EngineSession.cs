using Basics.Blocks;
using Basics.Common;
using Basics.Terrain.Generation;
using Basics.Jobs;
using Basics.PhysicsSystem;
using Basics.Settings;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using System;

namespace Basics.Engine;

/// <summary>
/// Repräsentiert eine eigenständige, isolierte Spiel- oder Benchmark-Sitzung.
/// Verwaltet einen EngineContext und dessen gesamten Lebenszyklus.
/// Ermöglicht saubere Benchmark-Iterationen und parallele A/B-Tests ohne Zustandslecks.
/// </summary>
public class EngineSession : IDisposable
{
    public EngineContext Context { get; }
    /// <summary>
    /// Gibt an, ob diese Sitzung im "Headless"-Modus (ohne Grafik) ausgeführt wird.
    /// </summary>
    /// <remarks>
    /// Dieser Wert ist derzeit informatisch: Der <see cref="EngineContext"/> initialisiert
    /// keine GPU-Ressourcen allein, unabhängig von diesem Flag. Die Spielansicht
    /// lädt GPU-Ressourcen separat auf dem Hauptthread.
    /// Das Flag dient dazu, dass Aufrufstellen denIntent dokumentieren können, und als
    /// Hook-Punkt, falls wir später echte Headless-Modus-Verzweigungen benötigen.
    /// </remarks>
    public bool IsHeadless { get; }

    public GameSettings Settings => Context.Settings;
    public ChunkProvider ChunkProvider => Context.ChunkProvider;
    public World World => Context.World;
    public Physics Physics => Context.Physics;
    public Raycaster Raycaster => Context.Raycaster;
    public TerrainGenerator TerrainGenerator => Context.TerrainGenerator;
    public JobScheduler JobScheduler => Context.JobScheduler;
    public BlockRegistry BlockRegistry => Context.BlockRegistry;
    public CoreAvailability CoreAvailability => Context.CoreAvailability;

     public EngineSession(EngineContext? context = null, bool isHeadless = true)
    {
        Context = context ?? new EngineContext(NullChunkGpuMeshFactory.Instance);
        IsHeadless = isHeadless;
    }

    public EngineSession(GameSettings settings, bool startScheduler = false, bool isHeadless = true)
    {
        Context = new EngineContext(NullChunkGpuMeshFactory.Instance, settings: settings, startJobScheduler: startScheduler);
        IsHeadless = isHeadless;
    }

    /// <summary>
    /// Aktiviert diesen Session-Kontext für den aufrufenden Thread / async Kontext.
    /// </summary>
    public IDisposable Activate()
    {
        return Context.Activate();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
