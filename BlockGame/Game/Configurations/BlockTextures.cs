using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Basics.Game.texture;
using Basics.Graphics;
using Silk.NET.OpenGL;

namespace Basics.Configurations;

public static class BlockTextures
{
    private sealed class BlockFile
    {
        public string? Name { get; set; }
        public List<string>? Tags { get; set; }
        public BlockTextureFaces? Textures { get; set; }
    }

    private sealed class BlockTextureFaces
    {
        public string? Top { get; set; }
        public string? Bottom { get; set; }
        public string? Front { get; set; }
        public string? Back { get; set; }
        public string? Left { get; set; }
        public string? Right { get; set; }
        public string? Sides { get; set; }
    }

    private sealed class BlockDefinition
    {
        public required string Name { get; init; }
        public required ushort[] Layers { get; init; }
        public required HashSet<string> Tags { get; init; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, ushort> BlockIdByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<ushort>> BlockIdsByTag = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ushort> TextureLayersByName = new(StringComparer.OrdinalIgnoreCase);
    private static List<BlockDefinition> _definitions = new();

    private static TextureArray? _terrainTexture;
    private static bool _initialized;

    public const int Top = 0;
    public const int Bottom = 1;
    public const int Front = 2;
    public const int Back = 3;
    public const int Left = 4;
    public const int Right = 5;

    public static ushort Air => 0;

    public static TextureArray TerrainTexture => _terrainTexture
        ?? throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");

    public static void Initialize(GL gl, string contentRoot = "Content")
    {
        if (_initialized)
        {
            return;
        }

        LoadTextures(gl, contentRoot);
        LoadBlocks(contentRoot);

        _initialized = true;
    }

    public static ushort Get(int blockId, int faceIndex)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");
        }

        if ((uint)blockId >= _definitions.Count)
        {
            return 0;
        }

        if ((uint)faceIndex >= 6)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex), "Face index muss zwischen 0 und 5 liegen.");
        }

        return _definitions[blockId].Layers[faceIndex];
    }

    public static ushort GetBlockId(string blockName)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");
        }

        string normalized = NormalizeId(blockName);
        if (!BlockIdByName.TryGetValue(normalized, out ushort id))
        {
            throw new KeyNotFoundException($"Unbekannter Block '{blockName}'.");
        }

        return id;
    }

    public static bool HasTag(ushort blockId, string tag)
    {
        if ((uint)blockId >= _definitions.Count)
        {
            return false;
        }

        return _definitions[blockId].Tags.Contains(NormalizeId(tag));
    }

    public static IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        string normalized = NormalizeId(tag);
        return BlockIdsByTag.TryGetValue(normalized, out HashSet<ushort>? ids)
            ? ids
            : Array.Empty<ushort>();
    }

    private static void LoadTextures(GL gl, string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content root nicht gefunden: {Path.GetFullPath(contentRoot)}");
        }

        var textureFiles = new List<(string textureId, string fullPath)>();

        foreach (string namespaceDir in Directory.EnumerateDirectories(contentRoot))
        {
            string namespaceName = Path.GetFileName(namespaceDir).ToLowerInvariant();
            string textureDir = Path.Combine(namespaceDir, "Textures");
            if (!Directory.Exists(textureDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(textureDir, "*.png", SearchOption.TopDirectoryOnly))
            {
                string textureName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                textureFiles.Add(($"{namespaceName}:{textureName}", file));
            }
        }

        if (textureFiles.Count == 0)
        {
            throw new InvalidDataException($"Keine Texturen unter '{contentRoot}/<namespace>/Textures' gefunden.");
        }

        textureFiles.Sort((a, b) => string.CompareOrdinal(a.textureId, b.textureId));
        string[] orderedPaths = textureFiles.Select(x => x.fullPath).ToArray();
        _terrainTexture = new TextureArray(gl, orderedPaths);

        for (ushort i = 0; i < textureFiles.Count; i++)
        {
            TextureLayersByName[textureFiles[i].textureId] = i;
        }
    }

    private static void LoadBlocks(string contentRoot)
    {
        _definitions = new List<BlockDefinition>
        {
            new()
            {
                Name = "core:air",
                Layers = new ushort[6],
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag:air" }
            }
        };
        BlockIdByName["core:air"] = Air;

        var blockFiles = new List<(string blockId, string fullPath)>();
        foreach (string namespaceDir in Directory.EnumerateDirectories(contentRoot))
        {
            string namespaceName = Path.GetFileName(namespaceDir).ToLowerInvariant();
            string blocksDir = Path.Combine(namespaceDir, "Blocks");
            if (!Directory.Exists(blocksDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(blocksDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                string blockName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                blockFiles.Add(($"{namespaceName}:{blockName}", file));
            }
        }

        blockFiles.Sort((a, b) => string.CompareOrdinal(a.blockId, b.blockId));
        foreach ((string blockId, string fullPath) in blockFiles)
        {
            BlockFile? block = JsonSerializer.Deserialize<BlockFile>(File.ReadAllText(fullPath), JsonOptions);
            if (block?.Textures == null)
            {
                throw new InvalidDataException($"Blockdatei ohne gültige 'textures': {fullPath}");
            }

            ushort id = (ushort)_definitions.Count;
            ushort[] layers = ResolveLayers(blockId, block.Textures, fullPath);
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (block.Tags != null)
            {
                foreach (string tag in block.Tags)
                {
                    string normalizedTag = NormalizeId(tag);
                    tags.Add(normalizedTag);

                    if (!BlockIdsByTag.TryGetValue(normalizedTag, out HashSet<ushort>? entries))
                    {
                        entries = new HashSet<ushort>();
                        BlockIdsByTag[normalizedTag] = entries;
                    }

                    entries.Add(id);
                }
            }

            _definitions.Add(new BlockDefinition
            {
                Name = blockId,
                Layers = layers,
                Tags = tags
            });

            BlockIdByName[blockId] = id;
        }
    }

    private static ushort[] ResolveLayers(string blockId, BlockTextureFaces textures, string sourcePath)
    {
        return new ushort[]
        {
            ResolveTextureId(textures.Top ?? textures.Sides, blockId, "top", sourcePath),
            ResolveTextureId(textures.Bottom ?? textures.Sides, blockId, "bottom", sourcePath),
            ResolveTextureId(textures.Front ?? textures.Sides, blockId, "front", sourcePath),
            ResolveTextureId(textures.Back ?? textures.Sides, blockId, "back", sourcePath),
            ResolveTextureId(textures.Left ?? textures.Sides, blockId, "left", sourcePath),
            ResolveTextureId(textures.Right ?? textures.Sides, blockId, "right", sourcePath)
        };
    }

    private static ushort ResolveTextureId(string? textureName, string blockId, string faceName, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(textureName))
        {
            throw new InvalidDataException($"Textur fehlt für {blockId}:{faceName} in {sourcePath}");
        }

        string normalized = NormalizeId(textureName);
        if (!TextureLayersByName.TryGetValue(normalized, out ushort layer))
        {
            throw new InvalidDataException($"Unbekannte Textur '{textureName}' für {blockId}:{faceName} in {sourcePath}");
        }

        return layer;
    }

    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
