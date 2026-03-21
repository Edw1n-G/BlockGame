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
        Console.WriteLine("--- Core Availability ---");
        Console.WriteLine($"Total Cores: {TotalCores}");
        Console.WriteLine($"Available Cores: {AvailableCores}");
        Console.WriteLine($"Terrain Generation Cores: {_terrainGenerationCores}");
        Console.WriteLine($"Chunk Meshing Cores: {_ChunkMeshingCores}");
    }

    static void LoadConfig(String configPath)
    {
        // TODO: Configuration aus Datei laden
    }
    
    static void DefaultConfig()
    {
        _terrainGenerationCores = AvailableCores/2 + 4;
        _ChunkMeshingCores = AvailableCores/2 - 4;
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