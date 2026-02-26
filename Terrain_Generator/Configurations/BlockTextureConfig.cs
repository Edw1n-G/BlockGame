//=============================================
// THIS CODE IS AI GENERATED - USE WITH CAUTION
//=============================================

using System.Text.Json;

namespace Basics.Configurations;

// Repräsentiert die UV-Koordinaten einer einzelnen Fläche im Texture Atlas
public class FaceUV
{
    public float UMin { get; set; }
    public float VMin { get; set; }
    public float UMax { get; set; }
    public float VMax { get; set; }
}

// Alle 6 Flächen eines Blocks
public class BlockFaces
{
    public required FaceUV Top    { get; set; }
    public required FaceUV Bottom { get; set; }
    public required FaceUV Front  { get; set; }
    public required FaceUV Back   { get; set; }
    public required FaceUV Left   { get; set; }
    public required FaceUV Right  { get; set; }
}

// Ein einzelner Block-Eintrag
public class BlockTextureEntry
{
    public int              BlockId { get; set; }
    public required BlockFaces Faces   { get; set; }
}

// Root-Objekt der JSON-Datei
public class BlockTextureConfigRoot
{
    public required List<BlockTextureEntry> Blocks { get; set; }
}

// Hilfsklasse zum Laden und Abrufen der Textur-Koordinaten
public static class BlockTextures
{
    private static FaceUV[,]? _faceTable;

    public const int Top    = 0;
    public const int Bottom = 1;
    public const int Front  = 2;
    public const int Back   = 3;
    public const int Left   = 4;
    public const int Right  = 5;

    public static void Initialize(string jsonPath)
    {
        if (_faceTable != null) return;

        string json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<BlockTextureConfigRoot>(json)
                   ?? throw new InvalidDataException("TextureConfig.json konnte nicht geladen werden.");

        int maxId = root.Blocks.Max(b => b.BlockId);
        _faceTable = new FaceUV[maxId + 1, 6];

        foreach (var block in root.Blocks)
        {
            _faceTable[block.BlockId, Top]    = block.Faces.Top;
            _faceTable[block.BlockId, Bottom] = block.Faces.Bottom;
            _faceTable[block.BlockId, Front]  = block.Faces.Front;
            _faceTable[block.BlockId, Back]   = block.Faces.Back;
            _faceTable[block.BlockId, Left]   = block.Faces.Left;
            _faceTable[block.BlockId, Right]  = block.Faces.Right;
        }
    }

    public static FaceUV Get(int blockId, int faceIndex)
    {
        if (_faceTable == null)
            throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");
        return _faceTable[blockId, faceIndex];
    }
}

