using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Basics.Configurations;

// Alle 6 Flächen eines Blocks – direkt als int (Texture-Array-Layer)
public class BlockFaces
{
    public byte Top    { get; set; }
    public byte Bottom { get; set; }
    public byte Front  { get; set; }
    public byte Back   { get; set; }
    public byte Left   { get; set; }
    public byte Right  { get; set; }
}

// Ein einzelner Block-Eintrag
public class BlockTextureEntry
{
    public byte BlockId { get; set; }
    public required BlockFaces Faces { get; set; }
}

// Root-Objekt der JSON-Datei
public class BlockTextureConfigRoot
{
    public required List<BlockTextureEntry> Blocks { get; set; }
}

// Hilfsklasse zum Laden und Abrufen der Texture-Array-Layer
public static class BlockTextures
{
    private static byte[,]? _layerTable; // [blockId, faceIndex] → Layer-Index

    public const byte Top    = 0;
    public const byte Bottom = 1;
    public const byte Front  = 2;
    public const byte Back   = 3;
    public const byte Left   = 4;
    public const byte Right  = 5;

    public static void Initialize(string jsonPath)
    {
        if (_layerTable != null) return;

        string json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<BlockTextureConfigRoot>(json)
                   ?? throw new InvalidDataException("TextureConfig.json konnte nicht geladen werden.");

        byte maxId = root.Blocks.Max(b => b.BlockId);
        _layerTable = new byte[maxId + 1, 6];

        foreach (var block in root.Blocks)
        {
            _layerTable[block.BlockId, Top]    = block.Faces.Top;
            _layerTable[block.BlockId, Bottom] = block.Faces.Bottom;
            _layerTable[block.BlockId, Front]  = block.Faces.Front;
            _layerTable[block.BlockId, Back]   = block.Faces.Back;
            _layerTable[block.BlockId, Left]   = block.Faces.Left;
            _layerTable[block.BlockId, Right]  = block.Faces.Right;
        }
    }

    /// <summary>
    /// Gibt den Texture-Array-Layer für eine bestimmte Block-ID und Face zurück.
    /// </summary>
    public static byte Get(int blockId, int faceIndex)
    {
        if (_layerTable == null)
            throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");
        return _layerTable[blockId, faceIndex];
    }
}

