using System;
using System.IO;
using Basics.Game;

namespace Basics.Utilities;

///<summary>
/// Berchnet die optimale Aufteilung der Threads für verschiedene Aufgaben.
/// Vorerst nur für Chunk-Generierung
/// Später für Pathfinding und andere CPU-intensive Aufgaben
/// </summary>
public class CoreAvailability
{
    private static int _terrainGenerationCores;
    private static int _ChunkMeshingCores;
    private static int _exampleTaskCores;
    
    public static int TotalCores => Environment.ProcessorCount;
    public static int AvailableCores => Math.Max(1, TotalCores - 2);// Es muss immer ein Thread für den Render und Logik Prozess da sein deshalb -2

    public static void Initialize()
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

    static void LoadConfig(String configPath)
    {
        _terrainGenerationCores = 1; //Load values from cinfig
        _ChunkMeshingCores = 1;
        _exampleTaskCores = 0;
    }
    
    static void DefaultConfig()
    {
        _terrainGenerationCores = AvailableCores - 2; // Alle verfügbaren Kerne für die Terrain-Generierung nutzen
        _ChunkMeshingCores = 2; // Die 2 die ich oben abziehe
        _exampleTaskCores = 0; // Keine Kerne für andere Aufgaben reservieren
    }

    public static int GetTerrainGenerationCores()
    {
        return _terrainGenerationCores;
    }

    public static int GetChunkMeshingCores()
    {
        return _ChunkMeshingCores;
    }
    
}