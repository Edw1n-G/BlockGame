using System.Text.Json;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Game.Configurations;

/// <summary>
/// Registriert und verwaltet Blockdefinitionen sowie deren Texturen.
/// Implementiert ein Palette-System: Die kanonische Identität eines Blocks ist sein
/// Namespaced-Name (z. B. "core:stone"), die numerische Block-ID ist nur ein
/// sitzungsspezifisches Handle.
/// Die Palette wird beim Start genau einmal geladen — first come first serve in
/// Entdeckungsreihenfolge, ohne Bevorzugung einzelner Erweiterungen — und ändert sich
/// danach nicht mehr, während der Engine-Prozess läuft. Modulare Erweiterungen werden
/// vor dem Start in das Content-Verzeichnis gelegt (namespace/Blocks/*.json) und kommen
/// dadurch automatisch in die Palette; doppelte Block-IDs führen zu einer Exception.
/// Ein späteres Load-Order- oder Block-Overwriting-System würde ausschließlich die
/// ID-Zuweisung in <see cref="LoadBlocks"/> bzw. die Duplicate-Prüfung ersetzen.
/// Für Serialisierung (Saves/Netzwerk) exportiert die Palette Namen als Snapshot
/// und erstellt Remap-Tabellen für gespeicherte Daten.
/// </summary>
public class BlockRegistry : IDisposable
{
    private sealed class BlockFile
    {
        public string? Name { get; set; }
        public List<string>? Tags { get; set; }
        public BlockTextureFaces? Textures { get; set; }
    }

    /// <summary>
    /// Textur-Zuweisung für die 6 Faces eines Blocks (JSON-Struktur einer Blockdatei).
    /// Jedes Face fällt auf <see cref="Sides"/> zurück, wenn es nicht gesetzt ist.
    /// </summary>
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

    private Dictionary<string, ushort> _blockIdByName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HashSet<ushort>> _blockIdsByTag = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ushort> _textureLayersByName = new(StringComparer.OrdinalIgnoreCase);
    private List<BlockDefinition> _definitions = new();

    private TextureArray? _terrainTexture;
    private bool _initialized;
    private bool _texturesLoaded;
    /// <summary>
    /// Gibt an, ob die Registry verworfen wurde. Nach dem Verwerfen wird jeglicher Zugriff
    /// mit einer <see cref="ObjectDisposedException"/> abgelehnt — insbesondere damit ein
    /// Worker-Thread während Game.Exit die Palette nicht über InitializeHeadless neu aufbauen kann.
    /// </summary>
    private bool _disposed;

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
    /// Initialisiert die BlockRegistry. Lädt Texturen (wenn eine GL-Instanz vorhanden ist)
    /// und die Block-Palette aus dem Content-Verzeichnis.
    /// Beide Initialisierungs-Pfade (mit und ohne GL) laufen über diese eine Methode.
    /// Die Palette wird genau einmal geladen — beim ersten Aufruf — und bleibt danach
    /// unverändert, während der Engine-Prozess läuft. Erneute Aufrufe (z. B.
    /// <c>Game.Enter</c> nach dem EngineContext-Konstruktor) laden nur noch fehlende
    /// Texturen, ändern aber niemals Blockdefinitionen oder Block-IDs.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz für die Textur-Erstellung oder null für headless-Betrieb.</param>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    public void Initialize(GL? gl, string contentRoot = "Content")
    {
        ThrowIfDisposed();

        string root = ResolveContentRoot(contentRoot);
        bool hasContent = Directory.Exists(root);

        if (!_texturesLoaded)
        {
            if (gl != null)
            {
                // Das Spiel kann ohne Texturen nicht rendern — hier bewusst laut fehlschlagen.
                LoadTextures(gl, root);
                _texturesLoaded = true;
            }
            else if (hasContent)
            {
                DiscoverTextureLayers(root);
            }
        }

        if (!_initialized)
        {
            if (hasContent)
            {
                LoadBlocks(root);
            }
            else
            {
                LoadDefaultFallbackBlocks();
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Initialisiert die BlockRegistry ohne GPU-Texturen (für headless-Betrieb).
    /// Lädt nur die Blockdefinitionen und erkennt Texturen, erstellt aber keine Textur-Arrays.
    /// Delegiert an <see cref="Initialize(GL?, string)"/> — dadurch entsteht unabhängig vom
    /// Initialisierungspfad immer dieselbe Palette.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    public void InitializeHeadless(string contentRoot = "Content")
    {
        Initialize(null, contentRoot);
    }

    /// <summary>
    /// Lädt Standard-Fallback-Blöcke, wenn kein Content vorhanden ist (headless-Betrieb).
    /// Die Reihenfolge entspricht exakt der ordinalen Sortierung, die <see cref="LoadBlocks"/>
    /// für Content-Dateien verwendet (air, dirt, grass, snow, stone) — damit Fallback- und
    /// Content-Pfad für dieselben Namen dieselben IDs vergeben.
    /// </summary>
    private void LoadDefaultFallbackBlocks()
    {
        _definitions = new List<BlockDefinition>
        {
            new() { Name = "core:air", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag:air" } },
            new() { Name = "core:dirt", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:grass", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:snow", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) },
            new() { Name = "core:stone", Layers = new ushort[6], Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) }
        };
        _blockIdByName["core:air"] = 0;
        _blockIdByName["core:dirt"] = 1;
        _blockIdByName["core:grass"] = 2;
        _blockIdByName["core:snow"] = 3;
        _blockIdByName["core:stone"] = 4;

        foreach (BlockDefinition definition in _definitions)
        {
            ushort id = _blockIdByName[definition.Name];
            foreach (string tag in definition.Tags)
            {
                IndexTag(_blockIdsByTag, tag, id);
            }
        }
    }

    /// <summary>
    /// Setzt die BlockRegistry auf den Ausgangszustand zurück.
    /// Gibt Texturen frei und löscht alle gespeicherten Definitionen.
    /// Bewusst nicht über die statische Fassade <see cref="BlockLoader"/> erreichbar:
    /// Die Palette eines Engine-Prozesses soll nicht zur Laufzeit neu aufgebaut werden können.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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
    /// Gibt eine Momentaufnahme zurück — Aufrufer können die Palette nicht über den
    /// Rückgabewert verändern.
    /// </summary>
    /// <param name="tag">Der Tag, nach dem gesucht werden soll.</param>
    /// <returns>Ein schreibgeschütztes Kollektion mit Block-IDs.</returns>
    public IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            InitializeHeadless();
        }

        string normalized = NormalizeId(tag);
        return _blockIdsByTag.TryGetValue(normalized, out HashSet<ushort>? ids)
            ? ids.ToArray()
            : Array.Empty<ushort>();
    }

    /// <summary>
    /// Gibt den Namespaced-Namen eines Blocks anhand seiner ID zurück (Palette-Rücklookup).
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <returns>Der Namespaced-Name des Blocks (z. B. "core:stone").</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn die Block-ID nicht existiert.</exception>
    public string GetName(ushort blockId)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            InitializeHeadless();
        }

        if ((uint)blockId >= (uint)_definitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(blockId), $"Unbekannte Block-ID {blockId}.");
        }

        return _definitions[blockId].Name;
    }

    /// <summary>
    /// Versucht, den Namespaced-Namen eines Blocks anhand seiner ID zu ermitteln.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="name">Der gefundene Name oder null.</param>
    /// <returns><c>true</c>, wenn die ID existiert, andernfalls <c>false</c>.</returns>
    public bool TryGetName(ushort blockId, out string? name)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            InitializeHeadless();
        }

        if ((uint)blockId < (uint)_definitions.Count)
        {
            name = _definitions[blockId].Name;
            return true;
        }

        name = null;
        return false;
    }

    /// <summary>
    /// Exportiert die aktuelle Palette (ID → Namespaced-Name) als Snapshot.
    /// Für Serialisierung (Saves/Netzwerk): Zusammen mit den Blockdaten speichern,
    /// damit beim Laden über <see cref="CreateRemapTable"/> auf die dann aktuelle
    /// Palette abgebildet werden kann — unabhängig von Load-Reihenfolge oder Mod-Auswahl.
    /// </summary>
    /// <returns>Ein Array, in dem der Index die Block-ID und der Wert der Namespaced-Name ist.</returns>
    public string[] ExportPalette()
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            InitializeHeadless();
        }

        var palette = new string[_definitions.Count];
        for (int i = 0; i < _definitions.Count; i++)
        {
            palette[i] = _definitions[i].Name;
        }

        return palette;
    }

    /// <summary>
    /// Erstellt eine Remap-Tabelle, die Block-IDs einer gespeicherten Palette auf die
    /// IDs dieser Registry abbildet (Index = alte ID, Wert = neue ID).
    /// Unbekannte Namen (auch null/leer) werden auf Luft (0) abgebildet — entfernte Blöcke
    /// werden zu Luft, analog zu anderen Voxel-Engines. Dadurch sind gespeicherte Welten
    /// immun gegen geänderte Load-Reihenfolgen, umbenannte Blöcke oder entfernte
    /// Mod-Erweiterungen. Über nicht mehr existierende Namen wird auf Console.Error gewarnt,
    /// damit ein fehlender Mod beim nächsten Speichern nicht unbemerkt Blöcke verliert.
    /// </summary>
    /// <param name="previousPalette">Die gespeicherte Palette (Index = alte ID, Wert = Name).</param>
    /// <returns>Die Remap-Tabelle für <see cref="RemapBlockArray"/>.</returns>
    public int[] CreateRemapTable(IReadOnlyList<string> previousPalette)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            InitializeHeadless();
        }

        var remap = new int[previousPalette.Count];
        List<string>? unknownNames = null;
        for (int i = 0; i < previousPalette.Count; i++)
        {
            string? name = previousPalette[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                remap[i] = Air;
                continue;
            }

            if (_blockIdByName.TryGetValue(NormalizeId(name), out ushort id))
            {
                remap[i] = id;
            }
            else
            {
                remap[i] = Air;
                (unknownNames ??= new List<string>()).Add(name);
            }
        }

        if (unknownNames != null)
        {
            Console.Error.WriteLine(
                $"BlockRegistry: {unknownNames.Count} Blockname(n) aus der gespeicherten Palette " +
                $"existieren nicht mehr und werden zu Luft gemappt: {string.Join(", ", unknownNames)}");
        }

        return remap;
    }

    /// <summary>
    /// Mappt ein Block-ID-Array (z. B. ChunkData.Blocks) mit der Remap-Tabelle
    /// auf die IDs der aktuellen Registry. Arbeitet in-place ohne Allokation.
    /// </summary>
    /// <param name="blocks">Das Block-ID-Array, das in-place remappt wird.</param>
    /// <param name="remapTable">Die Remap-Tabelle von <see cref="CreateRemapTable"/>.</param>
    public static void RemapBlockArray(ushort[] blocks, int[] remapTable)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            int mapped = (uint)blocks[i] < (uint)remapTable.Length
                ? remapTable[blocks[i]]
                : 0; // Unbekannte ID → Luft (Air ist immer 0)
            blocks[i] = (ushort)mapped;
        }
    }

    /// <summary>
    /// Sucht alle Texturen im Content-Verzeichnis und weist ihnen Layer-IDs zu.
    /// Wird für headless-Modus verwendet. Der Textur-Layer-Map wird erst nach vollständigem
    /// Erfolg zugewiesen, damit ein fehlgeschlagener Load keinen halb gefüllten Zustand hinterlässt.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    private void DiscoverTextureLayers(string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            return;
        }

        List<(string textureId, string fullPath)> textureFiles = CollectTextureFiles(contentRoot);

        var layersByName = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (ushort i = 0; i < textureFiles.Count; i++)
        {
            layersByName[textureFiles[i].textureId] = i;
        }

        _textureLayersByName = layersByName;
    }

    /// <summary>
    /// Sammelt alle Texturdateien (namespace/Textures/**/*.png) und sortiert sie ordinal.
    /// Die sortierte Reihenfolge definiert die Layer-Indizes.
    /// Doppelte Textur-IDs (z. B. gleicher Dateiname in Unterordnern) führen zu einer
    /// <see cref="InvalidDataException"/>, damit keine Textur stillschweigend als unbenutzter
    /// Layer endet und der Name nicht von der Sortierreihenfolge abhängt.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    /// <returns>Die sortierte Liste der Texturdateien.</returns>
    private static List<(string textureId, string fullPath)> CollectTextureFiles(string contentRoot)
    {
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

        // Da sortiert, stehen Duplikate direkt nebeneinander
        for (int i = 1; i < textureFiles.Count; i++)
        {
            if (textureFiles[i].textureId == textureFiles[i - 1].textureId)
            {
                throw new InvalidDataException(
                    $"Doppelte Textur-ID '{textureFiles[i].textureId}': {textureFiles[i].fullPath} " +
                    $"kollidiert mit {textureFiles[i - 1].fullPath}.");
            }
        }

        return textureFiles;
    }

    /// <summary>
    /// Lädt alle Texturen in ein TextureArray und weist ihnen Layer-IDs zu.
    /// Wird während der regulären Initialisierung verwendet. Sowohl das TextureArray als
    /// auch die Layer-Map werden erst nach vollständigem Erfolg zugewiesen.
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

        List<(string textureId, string fullPath)> textureFiles = CollectTextureFiles(contentRoot);

        if (textureFiles.Count == 0)
        {
            throw new InvalidDataException($"Keine Texturen unter '{contentRoot}/<namespace>/Textures' gefunden.");
        }

        var layersByName = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (ushort i = 0; i < textureFiles.Count; i++)
        {
            layersByName[textureFiles[i].textureId] = i;
        }

        string[] orderedPaths = textureFiles.Select(x => x.fullPath).ToArray();
        TextureArray terrainTexture = new TextureArray(gl, orderedPaths);

        // Erst nach vollständigem Erfolg zuweisen — ein Fehlschlag im TextureArray-Konstruktor
        // hinterlässt keinen halb gefüllten Zustand.
        _terrainTexture = terrainTexture;
        _textureLayersByName = layersByName;
    }

    /// <summary>
    /// Lädt alle Blockdefinitionen aus JSON-Dateien im Content-Verzeichnis in die Palette.
    /// Die IDs werden first come first serve in der (ordinal sortierten) Entdeckungsreihenfolge
    /// vergeben — ohne Bevorzugung einzelner Erweiterungen oder Namespaces. Ein späteres
    /// Load-Order-System würde ausschließlich diese Zuweisung ersetzen; ein
    /// Block-Overwriting-System würde die Duplicate-Prüfung durch Override-Semantik ersetzen.
    /// Doppelte Blocknamen (z. B. zwei Dateien mit gleichem Dateinamen im selben Namespace,
    /// oder ein core:air.json, da Luft intern zuerst registriert wird) führen zu einer
    /// <see cref="InvalidDataException"/>, die beide kollidierenden Dateien nennt.
    /// Der Zustand wird lokal aufgebaut und erst nach vollständigem Erfolg zugewiesen —
    /// ein fehlgeschlagener Load hinterlässt keinen halb gefüllten Zustand, dessen Retry
    /// irreführend als Duplikat-Fehler fehlschlagen würde.
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    private void LoadBlocks(string contentRoot)
    {
        // Lokal aufbauen, erst am Ende zuweisen.
        var definitions = new List<BlockDefinition>
        {
            new()
            {
                Name = "core:air",
                Layers = new ushort[6],
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag:air" }
            }
        };
        var blockIdByName = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["core:air"] = Air
        };
        var blockIdsByTag = new Dictionary<string, HashSet<ushort>>(StringComparer.OrdinalIgnoreCase);
        IndexTag(blockIdsByTag, "tag:air", Air);
        var blockPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["core:air"] = "(interner Luft-Block, kein core:air.json erlaubt)"
        };

        if (Directory.Exists(contentRoot))
        {
            // Phase 1 — Entdeckung: alle Blockdateien sammeln. Die Sortierung macht die
            // Entdeckungsreihenfolge deterministisch; sie definiert die ID-Vergabe.
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

            // Phase 2 — Zuweisung: Parsen, validieren, IDs first come first serve vergeben.
            foreach ((string blockId, string fullPath) in blockFiles)
            {
                if (blockIdByName.ContainsKey(blockId))
                {
                    throw new InvalidDataException(
                        $"Doppelter Blockname '{blockId}': {fullPath} kollidiert mit {blockPathByName[blockId]}.");
                }

                BlockFile? block = JsonSerializer.Deserialize<BlockFile>(File.ReadAllText(fullPath), JsonOptions);
                if (block?.Textures == null)
                {
                    throw new InvalidDataException($"Blockdatei ohne gültige 'textures': {fullPath}");
                }

                if (definitions.Count >= ushort.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Zu viele Blöcke definiert ({definitions.Count}): Das ushort-ID-Limit ist erreicht.");
                }

                ushort id = (ushort)definitions.Count;
                ushort[] layers = ResolveLayers(blockId, block.Textures, fullPath);
                var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (block.Tags != null)
                {
                    foreach (string tag in block.Tags)
                    {
                        tags.Add(NormalizeId(tag));
                    }
                }

                definitions.Add(new BlockDefinition
                {
                    Name = blockId,
                    Layers = layers,
                    Tags = tags
                });

                blockIdByName[blockId] = id;
                blockPathByName[blockId] = fullPath;

                foreach (string tag in tags)
                {
                    IndexTag(blockIdsByTag, tag, id);
                }
            }
        }

        // Erst nach vollständigem Erfolg zuweisen.
        _definitions = definitions;
        _blockIdByName = blockIdByName;
        _blockIdsByTag = blockIdsByTag;
    }

    /// <summary>
    /// Registriert einen normalisierten Tag im Tag-Index.
    /// </summary>
    /// <param name="index">Der Tag-Index, der gefüllt wird.</param>
    /// <param name="normalizedTag">Der bereits normalisierte Tag.</param>
    /// <param name="id">Die Block-ID, die den Tag besitzt.</param>
    private static void IndexTag(Dictionary<string, HashSet<ushort>> index, string normalizedTag, ushort id)
    {
        if (!index.TryGetValue(normalizedTag, out HashSet<ushort>? entries))
        {
            entries = new HashSet<ushort>();
            index[normalizedTag] = entries;
        }

        entries.Add(id);
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
    /// Resolves die Textur-ID für ein bestimmtes Face.
    /// Strikt: Eine Blockdatei, die eine Textur referenziert, die nicht existiert, ist ein
    /// Content-Fehler und führt zu einer <see cref="InvalidDataException"/> — auch headless.
    /// Die Textur-Map ist zu diesem Zeitpunkt immer gefüllt, da LoadTextures bzw.
    /// DiscoverTextureLayers immer vor LoadBlocks läuft.
    /// </summary>
    /// <param name="textureName">Der Name der Textur.</param>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceName">Der Name des Faces.</param>
    /// <param name="sourcePath">Der Pfad zur Block-Datei.</param>
    /// <returns>Der Layer-Index der Textur.</returns>
    /// <exception cref="InvalidDataException">Wird ausgelöst, wenn die Textur fehlt oder nicht gefunden wird.</exception>
    private ushort ResolveTextureId(string? textureName, string blockId, string faceName, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(textureName))
        {
            throw new InvalidDataException($"Textur fehlt für {blockId}:{faceName} in {sourcePath}");
        }

        string normalized = NormalizeId(textureName);
        if (!_textureLayersByName.TryGetValue(normalized, out ushort layer))
        {
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
    /// Gibt alle Ressourcen frei. Danach wird jeglicher Zugriff mit einer
    /// <see cref="ObjectDisposedException"/> abgelehnt.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Zuerst als verworfen markieren: Ein Worker-Thread, der während Game.Exit
        // noch Get() aufruft, darf die Palette nicht über InitializeHeadless neu aufbauen.
        _disposed = true;

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
    /// Stellt sicher, dass die Registry nicht verworfen wurde.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Wird ausgelöst, wenn die Registry verworfen wurde.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BlockRegistry));
        }
    }
}