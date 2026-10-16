using System.Text.Json;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Game.Configurations;

public class BlockRegistry : IDisposable
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

    private readonly Dictionary<string, ushort> _blockIdByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<ushort>> _blockIdsByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ushort> _textureLayersByName = new(StringComparer.OrdinalIgnoreCase);
    private List<BlockDefinition> _definitions = new();

    private TextureArray? _terrainTexture;
    private bool _initialized;
    private bool _texturesLoaded;

    public const int Top = 0;
    public const int Bottom = 1;
    public const int Front = 2;
    public const int Back = 3;
    public const int Left = 4;
    public const int Right = 5;

    public ushort Air => 0;

    public bool IsInitialized => _initialized;

    public TextureArray TerrainTexture => _terrainTexture
        ?? throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");

    public static string ResolveContentRoot(string contentRoot = "Content")
    {
        if (Directory.Exists(contentRoot)) return contentRoot;

        string inBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, contentRoot);
        if (Directory.Exists(inBaseDir)) return inBaseDir;

        string inBlockGame = Path.Combine(Directory.GetCurrentDirectory(), "BlockGame", contentRoot);
        if (Directory.Exists(inBlockGame)) return inBlockGame;

        string parentBlockGame = Path.Combine(Directory.GetCurrentDirectory(), "..", "BlockGame", contentRoot);
        if (Directory.Exists(parentBlockGame)) return Path.GetFullPath(parentBlockGame);

        return contentRoot;
    }

    public void Initialize(GL gl, string contentRoot = "Content")
    {
        string root = ResolveContentRoot(contentRoot);
        if (_initialized && _texturesLoaded)
        {
            return;
        }

        LoadTextures(gl, root);
        if (!_initialized)
        {
            LoadBlocks(root);
            _initialized = true;
        }
        _texturesLoaded = true;
    }

    public void InitializeHeadless(string contentRoot = "Content")
    {
        if (_initialized)
        {
            return;
        }

        string root = ResolveContentRoot(contentRoot);

        if (Directory.Exists(root))
        {
            DiscoverTextureLayers(root);
            LoadBlocks(root);
        }
        else
        {
            LoadDefaultFallbackBlocks();
        }

        _initialized = true;
    }

    private void LoadDefaultFallbackBlocks()
    {
        _definitions = new List<BlockDefinition>
        {
            new() { Name = "core:air", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag:air" } },
            new() { Name = "core:dirt", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:grass", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:stone", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:snow", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) }
        };
        _blockIdByName["core:air"] = 0;
        _blockIdByName["core:dirt"] = 1;
        _blockIdByName["core:grass"] = 2;
        _blockIdByName["core:stone"] = 3;
        _blockIdByName["core:snow"] = 4;
    }

    public void Reset()
    {
        _blockIdByName.Clear();
        _blockIdsByTag.Clear();
        _textureLayersByName.Clear();
        _definitions.Clear();
        _terrainTexture?.Dispose();
        _terrainTexture = null;
        _initialized = false;
        _texturesLoaded = false;
    }

    public ushort Get(int blockId, int faceIndex)
    {
        if (!_initialized)
        {
            InitializeHeadless();
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

    public ushort GetBlockId(string blockName)
    {
        if (!_initialized)
        {
            InitializeHeadless();
        }

        string normalized = NormalizeId(blockName);
        if (!_blockIdByName.TryGetValue(normalized, out ushort id))
        {
            throw new KeyNotFoundException($"Unbekannter Block '{blockName}'.");
        }

        return id;
    }

    public bool HasTag(ushort blockId, string tag)
    {
        if (!_initialized)
        {
            InitializeHeadless();
        }

        if ((uint)blockId >= _definitions.Count)
        {
            return false;
        }

        return _definitions[blockId].Tags.Contains(NormalizeId(tag));
    }

    public IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        if (!_initialized)
        {
            InitializeHeadless();
        }

        string normalized = NormalizeId(tag);
        return _blockIdsByTag.TryGetValue(normalized, out HashSet<ushort>? ids)
            ? ids
            : Array.Empty<ushort>();
    }

    private void DiscoverTextureLayers(string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            return;
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

            foreach (string file in Directory.EnumerateFiles(textureDir, "*.png", SearchOption.AllDirectories))
            {
                string textureName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                textureFiles.Add(($"{namespaceName}:{textureName}", file));
            }
        }

        textureFiles.Sort((a, b) => string.CompareOrdinal(a.textureId, b.textureId));
        for (ushort i = 0; i < textureFiles.Count; i++)
        {
            _textureLayersByName[textureFiles[i].textureId] = i;
        }
    }

    private void LoadTextures(GL gl, string contentRoot)
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

            foreach (string file in Directory.EnumerateFiles(textureDir, "*.png", SearchOption.AllDirectories))
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
            _textureLayersByName[textureFiles[i].textureId] = i;
        }
    }

    private void LoadBlocks(string contentRoot)
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
        _blockIdByName["core:air"] = Air;

        if (!Directory.Exists(contentRoot))
        {
            return;
        }

        var blockFiles = new List<(string blockId, string fullPath)>();
        foreach (string namespaceDir in Directory.EnumerateDirectories(contentRoot))
        {
            string namespaceName = Path.GetFileName(namespaceDir).ToLowerInvariant();
            string blocksDir = Path.Combine(namespaceDir, "Blocks");
            if (!Directory.Exists(blocksDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(blocksDir, "*.json", SearchOption.AllDirectories))
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

                    if (!_blockIdsByTag.TryGetValue(normalizedTag, out HashSet<ushort>? entries))
                    {
                        entries = new HashSet<ushort>();
                        _blockIdsByTag[normalizedTag] = entries;
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

            _blockIdByName[blockId] = id;
        }
    }

    private ushort[] ResolveLayers(string blockId, BlockTextureFaces textures, string sourcePath)
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

    private ushort ResolveTextureId(string? textureName, string blockId, string faceName, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(textureName))
        {
            throw new InvalidDataException($"Textur fehlt für {blockId}:{faceName} in {sourcePath}");
        }

        string normalized = NormalizeId(textureName);
        if (!_textureLayersByName.TryGetValue(normalized, out ushort layer))
        {
            if (!_texturesLoaded && !_textureLayersByName.ContainsKey(normalized))
            {
                return 0;
            }
            throw new InvalidDataException($"Unbekannte Textur '{textureName}' für {blockId}:{faceName} in {sourcePath}");
        }

        return layer;
    }

    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public void Dispose()
    {
        Reset(); // Reset() already disposes _terrainTexture correctly
    }
}
