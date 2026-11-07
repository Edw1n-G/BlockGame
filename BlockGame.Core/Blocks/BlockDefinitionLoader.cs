using Basics.Engine;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Basics.Blocks;

/// <summary>
/// Statische Fassade für Block- und Texturabfragen.
/// Delegiert alle Operationen an die aktive BlockRegistry des aktuellen EngineContexts
/// oder erstellt eine Standardinstanz, wenn kein Context verfügbar ist.
/// </summary>
public static class BlockDefinitionLoader
{
    internal sealed class BlockFile
    {
        public string? Name { get; set; }
        public List<string>? Tags { get; set; }
        public BlockTextureFaces? Textures { get; set; }
    }

    internal sealed class BlockTextureFaces
    {
        public string? Top { get; set; }
        public string? Bottom { get; set; }
        public string? Front { get; set; }
        public string? Back { get; set; }
        public string? Left { get; set; }
        public string? Right { get; set; }
        public string? Sides { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static (List<(string BlockId, string FullPath, BlockFile Data)> Blocks,
        List<(string TextureId, string FullPath)> Textures) Load(string root)
    {
        var blockFiles = new List<(string Id, string FullPath)>();
        var textures = new List<(string TextureId, string FullPath)>();
        foreach (string namespaceDir in Directory.EnumerateDirectories(root))
        {
            string namespaceName = Path.GetFileName(namespaceDir).ToLowerInvariant();
            string blocksDir = Path.Combine(namespaceDir, "Blocks");
            if (Directory.Exists(blocksDir))
            {
                foreach (string file in Directory.EnumerateFiles(blocksDir, "*.json", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    blockFiles.Add(($"{namespaceName}:{name}", file));
                }
            }

            string textureDir = Path.Combine(namespaceDir, "Textures");
            if (Directory.Exists(textureDir))
            {
                foreach (string file in Directory.EnumerateFiles(textureDir, "*.png", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    textures.Add(($"{namespaceName}:{name}", file));
                }
            }
        }

        blockFiles.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        textures.Sort((a, b) => string.CompareOrdinal(a.TextureId, b.TextureId));

        var blocks = new List<(string BlockId, string FullPath, BlockFile Data)>();
        foreach (var (blockId, fullPath) in blockFiles)
        {
            var data = JsonSerializer.Deserialize<BlockFile>(File.ReadAllText(fullPath), JsonOptions);
            if (data?.Textures is null)
                throw new InvalidDataException($"Blockdatei ohne gültige 'textures': {fullPath}");
            blocks.Add((blockId, fullPath, data));
        }
        return (blocks, textures);
    }

    private static BlockRegistry? _defaultRegistry;

    /// <summary>
    /// Gibt die aktuelle BlockRegistry zurück.
    /// Wenn kein EngineContext vorhanden ist, wird eine Standardinstanz erstellt.
    /// </summary>
    public static BlockRegistry Current =>
        EngineContext.Current?.BlockRegistry ?? (_defaultRegistry ??= CreateDefaultRegistry());

    /// <summary>
    /// Erstellt eine neue BlockRegistry mit Headless-Initialisierung als Fallback.
    /// </summary>
    private static BlockRegistry CreateDefaultRegistry()
    {
        var registry = new BlockRegistry();
        if (System.IO.Directory.Exists("Content"))
        {
            try
            {
                registry.InitializeHeadless("Content");
            }
            catch
            {
                // Headless Fallback falls Content unvollständig ist
            }
        }
        return registry;
    }

    /// <summary>
    /// Index des oberen Face (Top) in der Layer-Array.
    /// </summary>
    public const int Top = BlockRegistry.Top;
    
    /// <summary>
    /// Index des unteren Face (Bottom) in der Layer-Array.
    /// </summary>
    public const int Bottom = BlockRegistry.Bottom;
    
    /// <summary>
    /// Index des vorderen Face (Front) in der Layer-Array.
    /// </summary>
    public const int Front = BlockRegistry.Front;
    
    /// <summary>
    /// Index des hinteren Face (Back) in der Layer-Array.
    /// </summary>
    public const int Back = BlockRegistry.Back;
    
    /// <summary>
    /// Index des linken Face (Left) in der Layer-Array.
    /// </summary>
    public const int Left = BlockRegistry.Left;
    
    /// <summary>
    /// Index des rechten Face (Right) in der Layer-Array.
    /// </summary>
    public const int Right = BlockRegistry.Right;

    /// <summary>
    /// Die Block-ID für Luft (Air).
    /// </summary>
    public static ushort Air => 0;

    /// <summary>
    /// Initialisiert die BlockRegistry ohne GPU-Texturen (für headless-Betrieb).
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    public static void InitializeHeadless(string contentRoot = "Content")
    {
        Current.InitializeHeadless(contentRoot);
    }

    /// <summary>
    /// Setzt die BlockRegistry auf den Ausgangszustand zurück.
    /// </summary>
    public static void Reset()
    {
        Current.Reset();
    }

    /// <summary>
    /// Gibt die Texture-ID (Layer-Index) für einen Block und ein Face zurück.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceIndex">Der Index des Faces (0-5).</param>
    /// <returns>Der Layer-Index der Textur für dieses Face.</returns>
    public static ushort Get(int blockId, int faceIndex)
    {
        return Current.Get(blockId, faceIndex);
    }

    /// <summary>
    /// Gibt die ID eines Blocks anhand seines Namens zurück.
    /// </summary>
    /// <param name="blockName">Der Name des Blocks (z. B. "core:stone").</param>
    /// <returns>Die ID des Blocks.</returns>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der Block nicht gefunden wird.</exception>
    public static ushort GetBlockId(string blockName)
    {
        return Current.GetBlockId(blockName);
    }

    /// <summary>
    /// Prüft, ob ein Block den angegebenen Tag hat.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="tag">Der Tag, der überprüft werden soll.</param>
    /// <returns><c>true</c>, wenn der Block den Tag hat; andernfalls <c>false</c>.</returns>
    public static bool HasTag(ushort blockId, string tag)
    {
        return Current.HasTag(blockId, tag);
    }

    /// <summary>
    /// Gibt alle Block-IDs zurück, die den angegebenen Tag haben.
    /// </summary>
    /// <param name="tag">Der Tag, nach dem gesucht werden soll.</param>
    /// <returns>Ein schreibgeschütztes Kollektion mit Block-IDs.</returns>
    public static IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        return Current.GetByTag(tag);
    }
}