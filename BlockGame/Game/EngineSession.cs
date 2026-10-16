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
    /// Gibt an, ob diese Sitzung im "Headless"-Modus (ohne Grafik) ausgeführt wird.
    /// </summary>
    /// <remarks>
    /// Dieser Wert ist derzeit informatisch: Der <see cref="EngineContext"/> initialisiert
    /// keine GL-Ressourcen allein, unabhängig von diesem Flag. GL wird nur dann verwendet,
    /// wenn <see cref="Renderer.Setup()"/>/<see cref="BlockRegistry.Initialize(GL, string)"/>
    /// explizit aus einem fenstergesteuerten Game-Zustand aufgerufen werden.
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
