using Basics.Blocks;
using Basics.Common;
using Basics.Terrain.Generation;
using Basics.Terrain.Generation.Noise;
using Basics.Jobs;
using Basics.PhysicsSystem;
using Basics.Settings;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using System;
using System.IO;
using System.Threading;

namespace Basics.Engine;

/// <summary>
/// Hält alle Subsystem-Instanzen für eine isolierte Engine-Ausführung.
/// Erlaubt parallele oder sequentielle Ausführung mehrerer Engine-Instanzen
/// im selben Prozess ohne Datenlecks oder statische Kollisionen.
/// </summary>
public class EngineContext : IDisposable
{
    private static readonly AsyncLocal<EngineContext?> _currentContext = new();
    private static EngineContext? _globalContext;

    /// <summary>
    /// Der aktuell aktive Engine-Kontext für den aufrufenden Ausführungskontext (Thread / async Flow).
    /// Fällt auf den globalen Kontext zurück, falls kein thread-lokaler Scope aktiv ist.
    /// </summary>
    public static EngineContext? Current
    {
        get => _currentContext.Value ?? _globalContext;
        set => _currentContext.Value = value;
    }

    /// <summary>
    /// Setzt den globalen Standard-Kontext für die gesamte Anwendung.
    /// </summary>
    public static void SetGlobal(EngineContext? context)
    {
        _globalContext = context;
    }

    /// <summary>
    /// Aktiviert diesen Kontext im aktuellen Ausführungskontext (AsyncLocal).
    /// Kann in einer using-Anweisung verwendet werden.
    /// </summary>
    public IDisposable Activate()
    {
        return new ContextScope(this);
    }

    /// <summary>
    /// Aktiviert den übergebenen Kontext im aktuellen Ausführungskontext (AsyncLocal).
    /// </summary>
    public static IDisposable Use(EngineContext context)
    {
        return new ContextScope(context);
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly EngineContext? _previous;

        public ContextScope(EngineContext context)
        {
            _previous = _currentContext.Value;
            _currentContext.Value = context;
        }

        public void Dispose()
        {
            _currentContext.Value = _previous;
        }
    }

    // Subsysteme
    public GameSettings Settings { get; }
    public CoreAvailability CoreAvailability { get; }
    public BlockRegistry BlockRegistry { get; }
    public NoiseCalculator NoiseCalculator { get; }
    public TerrainGenerator TerrainGenerator { get; }
    public JobScheduler JobScheduler { get; }
    public ChunkProvider ChunkProvider { get; }
    public Raycaster Raycaster { get; }
    public Physics Physics { get; }
    public World World { get; }

    public EngineContext(
        IChunkGpuMeshFactory gpuMeshFactory,
        GameSettings? settings = null,
        CoreAvailability? coreAvailability = null,
        BlockRegistry? blockRegistry = null,
        NoiseCalculator? noiseCalculator = null,
        TerrainGenerator? terrainGenerator = null,
        JobScheduler? jobScheduler = null,
        ChunkProvider? chunkProvider = null,
        Raycaster? raycaster = null,
        Physics? physics = null,
        World? world = null,
        bool startJobScheduler = false,
        int? workerCores = null)
    {
        Settings = settings ?? new GameSettings();
        CoreAvailability = coreAvailability ?? new CoreAvailability();
        BlockRegistry = blockRegistry ?? new BlockRegistry();

        if (!BlockRegistry.IsInitialized)
        {
            BlockRegistry.InitializeHeadless();
        }

        NoiseCalculator = noiseCalculator ?? new NoiseCalculator(Settings);
        TerrainGenerator = terrainGenerator ?? new TerrainGenerator(NoiseCalculator, Settings, BlockRegistry);
        JobScheduler = jobScheduler ?? new JobScheduler();
        ChunkProvider = chunkProvider ?? new ChunkProvider(gpuMeshFactory, JobScheduler, Settings);
        Raycaster = raycaster ?? new Raycaster(ChunkProvider);
        Physics = physics ?? new Physics(Raycaster);
        World = world ?? new World(ChunkProvider);

        if (startJobScheduler)
        {
            var jobContext = new JobContext(TerrainGenerator, ChunkProvider, BlockRegistry);
            int cores = workerCores ?? CoreAvailability.GetCores();
            JobScheduler.Start(cores, jobContext);
        }
    }

    public bool Shutdown(TimeSpan? timeout = null)
    {
        if (!JobScheduler.Stop(timeout)) return false;
        ChunkProvider.Dispose();
        NoiseCalculator.Dispose();
        BlockRegistry.Dispose();

        if (ReferenceEquals(_globalContext, this))
        {
            _globalContext = null;
        }
        if (ReferenceEquals(_currentContext.Value, this))
        {
            _currentContext.Value = null;
        }
        return true;
    }

    public void Dispose() => Shutdown();
}
