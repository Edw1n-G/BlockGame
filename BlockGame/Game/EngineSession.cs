using System;
using Basics.Configurations;
using Basics.Game.Configurations;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.PhysicsSystem;
using Basics.Game.Utilities;

namespace Basics.Game;

/// <summary>
/// Repräsentiert eine eigenständige, isolierte Spiel- oder Benchmark-Sitzung.
/// Verwaltet einen EngineContext und dessen gesamten Lebenszyklus.
/// Ermöglicht saubere Benchmark-Iterationen und parallele A/B-Tests ohne Zustandslecks.
/// </summary>
public class EngineSession : IDisposable
{
    public EngineContext Context { get; }
    /// <summary>
    /// ...
    /// <para>
    /// <paramref name="isHeadless"/> is informational for now: EngineContext never
    /// initializes GL resources on its own regardless of this flag. GL is only
    /// touched when Renderer.Setup()/BlockRegistry.Initialize(gl,...) are called
    /// explicitly from a windowed Game state. This flag exists so call sites can
    /// self-document intent and as a hook point if we ever need real headless-mode
    /// branching later.
    /// </para>
    /// </summary>
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
        Context = context ?? new EngineContext();
        IsHeadless = isHeadless;
    }

    public EngineSession(GameSettings settings, bool startScheduler = false, bool isHeadless = true)
    {
        Context = new EngineContext(settings: settings, startJobScheduler: startScheduler);
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
