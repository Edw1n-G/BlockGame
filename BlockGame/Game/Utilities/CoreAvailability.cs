using System;
using System.IO;
using Basics.Game;

namespace Basics.Game.Utilities;

///<summary>
/// Berechnet die optimale Aufteilung der Threads für verschiedene Aufgaben.
/// Vorerst nur für Chunk-Generierung
/// Später für Pathfinding und andere CPU-intensive Aufgaben
///</summary>
public class CoreAvailability
{
    private int _taskCores;
    
    public static int TotalCores => Environment.ProcessorCount;
    public static int AvailableCores => Math.Max(1, TotalCores - 2);

    public int TaskCores => _taskCores;

    public CoreAvailability(int? customTaskCores = null)
    {
        if (customTaskCores.HasValue)
        {
            _taskCores = customTaskCores.Value;
        }
        else
        {
            DefaultConfig();
        }
    }

    public void InitializeInstance()
    {
        if (File.Exists("coreconfig.txt"))
        {
            LoadConfig("coreconfig.txt");
        }
        else
        {
            DefaultConfig();
        }
    }

    private void LoadConfig(string configPath)
    {
        // TODO: Configuration aus Datei laden
        DefaultConfig();
    }
    
    private void DefaultConfig()
    {
        _taskCores = AvailableCores / 2 + 4;
    }

    public int GetCores()
    {
        return _taskCores;
    }

    // =========================================================================
    // --- Statische Fassade für Ergonomie und Abwärtskompatibilität ---
    // =========================================================================

    private static CoreAvailability? _defaultInstance;
    public static CoreAvailability Current =>
        EngineContext.Current?.CoreAvailability ?? (_defaultInstance ??= new CoreAvailability());

    public static void Initialize() => Current.InitializeInstance();
    public static int GetTaskCores() => Current._taskCores;
}