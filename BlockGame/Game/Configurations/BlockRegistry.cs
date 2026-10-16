using System.Text.Json;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Game.Configurations;

/// <summary>
/// Registriert und verwaltet Blockdefinitionen sowie deren Texturen.
/// Lädt Blockdefinitionen aus JSON-Dateien, erkennt Texturen aus dem Content-Verzeichnis
/// und berechnet die Texture-Layer-IDs für alle Blockfaces.
/// Stellt eine zentrale Datenstruktur für alle Blöcke im Spiel bereit.
/// </summary>
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

    /// <summary>
    /// Index des oberen Face (Top) in der Layer-Array.
    /// </summary>
    public const int Top = 0;
    
    /// <summary>
    /// Index des unteren Face (Bottom) in der Layer-Array.
    /// </summary>
    public const int Bottom = 1;
    
    /// <summary>
    /// Index des vorderen Face (Front) in der Layer-Array.
    /// </summary>
    public const int Front = 2;
    
    /// <summary>
    /// Index des hinteren Face (Back) in der Layer-Array.
    /// </summary>
    public const int Back = 3;
    
    /// <summary>
    /// Index des linken Face (Left) in der Layer-Array.
    /// </summary>
    public const int Left = 4;
    
    /// <summary>
    /// Index des rechten Face (Right) in der Layer-Array.
    /// </summary>
    public const int Right = 5;

    /// <summary>
    /// Die Block-ID für Luft (Air).
    /// </summary>
    public ushort Air => 0;

    /// <summary>
    /// Gibt an, ob die BlockRegistry initialisiert wurde.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gibt die Terrain-Textur zurück, die alle Texturen als Texture-Array enthält.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn die Textur noch nicht initialisiert wurde.</exception>
    public TextureArray TerrainTexture => _terrainTexture
        ?? throw new InvalidOperationException("BlockTextures wurde nicht initialisiert. Initialize() aufrufen.");

    /// <summary>
    /// Resolves den Content-Root-Pfad, der für das Laden von Blöcken und Texturen verwendet wird.
    /// Prüft verschiedene Pfade (Content, BaseDirectory/Content, BlockGame/Content, etc.).
    /// </summary>
    /// <param name="contentRoot">Der optionale Basis-Content-Pfad.</param>
    /// <returns>Der vollständige Pfad zum Content-Verzeichnis.</returns>
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

    /// <summary>
    /// Initialisiert die BlockRegistry mit Texturen und Blockdefinitionen.
    /// Lädt Texturen und Blöcke aus dem Content-Verzeichnis.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz für die Textur-Erstellung.</param>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
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

    /// <summary>
    /// Initialisiert die BlockRegistry ohne GPU-Texturen (für headless-Betrieb).
    /// Lädt nur die Blockdefinitionen und erkennt Texturen, erstellt aber keine Textur-Arrays.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
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

    /// <summary>
    /// Lädt Standard-Fallback-Blöcke, wenn kein Content vorhanden ist.
    /// </summary>
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

    /// <summary>
    /// Setzt die BlockRegistry auf den Ausgangszustand zurück.
    /// Gibt Texturen frei und löscht alle gespeicherten Definitionen.
    /// </summary>
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

    /// <summary>
    /// Gibt die Texture-ID (Layer-Index) für einen Block und ein Face zurück.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceIndex">Der Index des Faces (0-5).</param>
    /// <returns>Der Layer-Index der Textur für dieses Face.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn faceIndex außerhalb des gültigen Bereichs liegt.</exception>
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

    /// <summary>
    /// Gibt die ID eines Blocks anhand seines Namens zurück.
    /// </summary>
    /// <param name="blockName">Der Name des Blocks (z. B. "core:stone").</param>
    /// <returns>Die ID des Blocks.</returns>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der Block nicht gefunden wird.</exception>
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

    /// <summary>
    /// Prüft, ob ein Block den angegebenen Tag hat.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="tag">Der Tag, der überprüft werden soll.</param>
    /// <returns><c>true</c>, wenn der Block den Tag hat; andernfalls <c>false</c>.</returns>
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

    /// <summary>
    /// Gibt alle Block-IDs zurück, die den angegebenen Tag haben.
    /// </summary>
    /// <param name="tag">Der Tag, nach dem gesucht werden soll.</param>
    /// <returns>Ein schreibgeschütztes Kollektion mit Block-IDs.</returns>
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

    /// <summary>
    /// Sucht alle Texturen im Content-Verzeichnis und weist ihnen Layer-IDs zu.
    /// Wird für headless-Modus verwendet.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
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

    /// <summary>
    /// Lädt alle Texturen in ein TextureArray und weist ihnen Layer-IDs zu.
    /// Wird während der regulären Initialisierung verwendet.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    /// <exception cref="DirectoryNotFoundException">Wird ausgelöst, wenn der Content-Root nicht gefunden wird.</exception>
    /// <exception cref="InvalidDataException">Wird ausgelöst, wenn keine Texturen gefunden werden.</exception>
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

    /// <summary>
    /// Lädt alle Blockdefinitionen aus JSON-Dateien im Content-Verzeichnis.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
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

    /// <summary>
    /// Resolves die Texture-IDs für alle Faces eines Blocks.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="textures">Die Texturen, die im Block definiert sind.</param>
    /// <param name="sourcePath">Der Pfad zur Block-Datei.</param>
    /// <returns>Ein Array mit 6 Layer-IDs (eine pro Face).</returns>
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

    /// <summary>
    /// Resolves die Texture-ID für ein bestimmtes Face.
    /// </summary>
    /// <param name="textureName">Der Name der Textur.</param>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceName">Der Name des Faces.</param>
    /// <param name="sourcePath">Der Pfad zur Block-Datei.</param>
    /// <returns>Der Layer-Index der Textur.</returns>
    /// <exception cref="InvalidDataException">Wird ausgelöst, wenn die Textur nicht gefunden wird.</exception>
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

    /// <summary>
    /// Normalisiert einen Block- oder Textur-Bezeichner.
    /// Trimmt Whitespace und konvertiert zu Kleinbuchstaben.
    /// </summary>
    /// <param name="value">Der zu normalisierende Wert.</param>
    /// <returns>Der normalisierte Bezeichner.</returns>
    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        Reset();
    }
}