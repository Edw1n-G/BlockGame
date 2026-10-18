namespace Basics.Game.Content.Registries;

/// <summary>
/// Unveränderliche, schreibgeschützte Block-Registry: wird ausschließlich über
/// <see cref="BlockRegistryBuilder.Build"/> erstellt und ist danach für die
/// Lebensdauer des Engine-Prozesses fix — von jedem Thread aus lesbar, da die
/// Klasse nach dem Konstruktor keinen veränderlichen Zustand mehr besitzt.
/// <para>
/// Palette-System: Die kanonische Identität eines Blocks ist sein Namespaced-Name
/// (z. B. "core:stone"), die numerische Block-ID ist nur ein sitzungsspezifisches
/// Handle. Luft ist immer ID 0 (hartes Invariant — Physik und Mesher testen gegen 0).
/// </para>
/// Für Serialisierung (Saves/Netzwerk) exportiert die Palette Namen als Snapshot
/// und erstellt über <see cref="PaletteSnapshot"/> Remap-Tabellen für gespeicherte Daten.
/// </summary>
public sealed class BlockRegistry
{
    /// <summary>
    /// Index des oberen Face (Top) im Layer-Array.
    /// </summary>
    public const int Top = 0;

    /// <summary>
    /// Index des unteren Face (Bottom) im Layer-Array.
    /// </summary>
    public const int Bottom = 1;

    /// <summary>
    /// Index des vorderen Face (Front) im Layer-Array.
    /// </summary>
    public const int Front = 2;

    /// <summary>
    /// Index des hinteren Face (Back) im Layer-Array.
    /// </summary>
    public const int Back = 3;

    /// <summary>
    /// Index des linken Face (Left) im Layer-Array.
    /// </summary>
    public const int Left = 4;

    /// <summary>
    /// Index des rechten Face (Right) im Layer-Array.
    /// </summary>
    public const int Right = 5;

    private const int FaceCount = 6;

    private readonly Palette<BlockDefinition> _palette;
    private readonly TagIndex _tags;

    /// <summary>
    /// Erzeugt eine Registry aus einer eingefrorenen Palette. Nur intern —
    /// außen ausschließlich über <see cref="BlockRegistryBuilder.Build"/>.
    /// </summary>
    internal BlockRegistry(Palette<BlockDefinition> palette, TagIndex tags, TextureCatalog textures)
    {
        _palette = palette;
        _tags = tags;
        Textures = textures;

        // Flache Heißpfad-Tabellen aus den Definitionen bauen: Der Mesher ruft
        // pro Face zu, deshalb liegen die Layer und Flags flach vor.
        int count = palette.Count;
        FaceLayers = new ushort[count * FaceCount];
        Flags = new BlockFlags[count];
        for (ushort i = 0; i < count; i++)
        {
            BlockDefinition definition = palette[i];
            if (definition.Shape is CubeShape cube)
            {
                for (int face = 0; face < FaceCount; face++)
                {
                    FaceLayers[i * FaceCount + face] = cube.FaceLayers[face];
                }
            }

            Flags[i] = new BlockFlags(
                IsOpaque: i != 0, // Luft ist nie undurchsichtig; derzeit alle anderen Blöcke
                RenderKind: i == 0 ? BlockRenderKind.None : definition.Shape.Kind);
        }
    }

    /// <summary>Die Block-ID für Luft (Air).</summary>
    public ushort Air => 0;

    /// <summary>Anzahl der Blöcke in der Palette.</summary>
    public int Count => _palette.Count;

    /// <summary>
    /// Der CPU-seitige Texturkatalog (Ressourcenname → Layer, plus geordnete Pfade).
    /// Der Renderer baut daraus das GL-Texturarray (TextureArrayBuilder).
    /// </summary>
    public TextureCatalog Textures { get; }

    /// <summary>
    /// Flache Layer-Tabelle für den Heißpfad: <c>[blockId * 6 + faceIndex]</c> → Texture-Layer.
    /// IDs stammen immer aus derselben Registry, deshalb ist der Zugriff ohne Prüfung sicher.
    /// </summary>
    public ushort[] FaceLayers { get; }

    /// <summary>
    /// Flache Flag-Tabelle für den Heißpfad: <c>[blockId]</c> → Flags (IsOpaque, RenderKind).
    /// </summary>
    public BlockFlags[] Flags { get; }

    /// <summary>
    /// Gibt die Textur-ID (Layer-Index) für einen Block und ein Face zurück.
    /// Für den Heißpfad (Mesher) stattdessen <see cref="FaceLayers"/> verwenden.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceIndex">Der Index des Faces (0-5).</param>
    /// <returns>Der Layer-Index der Textur für dieses Face; Luft (unbekannte ID) ergibt 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn faceIndex außerhalb des gültigen Bereichs liegt.</exception>
    public ushort Get(int blockId, int faceIndex)
    {
        if ((uint)faceIndex >= FaceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex), "Face index muss zwischen 0 und 5 liegen.");
        }

        if ((uint)blockId >= (uint)_palette.Count)
        {
            return 0;
        }

        return FaceLayers[blockId * FaceCount + faceIndex];
    }

    /// <summary>
    /// Gibt die ID eines Blocks anhand seines Namens zurück.
    /// </summary>
    /// <param name="blockName">Der Name des Blocks (z. B. "core:stone").</param>
    /// <returns>Die ID des Blocks.</returns>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der Block nicht gefunden wird.</exception>
    public ushort GetBlockId(string blockName)
    {
        ResourceId id = ResourceId.Parse(blockName);
        if (!_palette.TryIdOf(id, out ushort blockId))
        {
            throw new KeyNotFoundException($"Unbekannter Block '{blockName}'.");
        }

        return blockId;
    }

    /// <summary>
    /// Prüft, ob ein Block den angegebenen Tag hat.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="tag">Der Tag, der überprüft werden soll.</param>
    /// <returns><c>true</c>, wenn der Block den Tag hat; andernfalls <c>false</c>.</returns>
    public bool HasTag(ushort blockId, string tag)
    {
        return (uint)blockId < (uint)_palette.Count && _tags.Has(blockId, tag);
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
        return _tags.Get(tag);
    }

    /// <summary>
    /// Gibt den Namespaced-Namen eines Blocks anhand seiner ID zurück (Palette-Rücklookup).
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <returns>Der Namespaced-Name des Blocks (z. B. "core:stone").</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn die Block-ID nicht existiert.</exception>
    public string GetName(ushort blockId)
    {
        if ((uint)blockId >= (uint)_palette.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(blockId), $"Unbekannte Block-ID {blockId}.");
        }

        return _palette.NameOf(blockId).FullName;
    }

    /// <summary>
    /// Versucht, den Namespaced-Namen eines Blocks anhand seiner ID zu ermitteln.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="name">Der gefundene Name oder null.</param>
    /// <returns><c>true</c>, wenn die ID existiert, andernfalls <c>false</c>.</returns>
    public bool TryGetName(ushort blockId, out string? name)
    {
        if ((uint)blockId < (uint)_palette.Count)
        {
            name = _palette.NameOf(blockId).FullName;
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
        return _palette.Export();
    }

    /// <summary>
    /// Erstellt eine Remap-Tabelle, die Block-IDs einer gespeicherten Palette auf die
    /// IDs dieser Registry abbildet (Index = alte ID, Wert = neue ID). Delegiert an
    /// <see cref="PaletteSnapshot.CreateRemap"/> — reine Funktion, funktioniert für
    /// jede Palette.
    /// </summary>
    /// <param name="previousPalette">Die gespeicherte Palette (Index = alte ID, Wert = Name).</param>
    /// <returns>Die Remap-Tabelle für <see cref="RemapBlockArray"/>.</returns>
    public int[] CreateRemapTable(IReadOnlyList<string> previousPalette)
    {
        return PaletteSnapshot.CreateRemap(previousPalette, _palette.Export(), Console.Error);
    }

    /// <summary>
    /// Mappt ein Block-ID-Array (z. B. ChunkData.Blocks) mit der Remap-Tabelle
    /// auf die IDs der aktuellen Palette. Arbeitet in-place ohne Allokation.
    /// </summary>
    /// <param name="blocks">Das Block-ID-Array, das in-place remappt wird.</param>
    /// <param name="remapTable">Die Remap-Tabelle von <see cref="CreateRemapTable"/>.</param>
    public static void RemapBlockArray(ushort[] blocks, int[] remapTable)
    {
        PaletteSnapshot.RemapBlockArray(blocks, remapTable);
    }

    /// <summary>
    /// Erstellt eine Fallback-Registry, wenn kein Content vorhanden ist
    /// (headless-Betrieb). Die Reihenfolge entspricht exakt der ordinalen Sortierung,
    /// die der ContentLoader für Content-Dateien verwendet (air, dirt, grass, snow,
    /// stone) — damit Fallback- und Content-Pfad für dieselben Namen dieselben IDs vergeben.
    /// </summary>
    /// <returns>Die Fallback-Registry mit null-Layern für alle Blöcke.</returns>
    public static BlockRegistry CreateFallback()
    {
        var builder = new BlockRegistryBuilder()
            .UseTextures(new TextureCatalog(Array.Empty<(ResourceId, string)>()))
            .ReserveAir();

        foreach (string name in new[] { "core:dirt", "core:grass", "core:snow", "core:stone" })
        {
            builder.AddBlock(
                ResourceId.Parse(name),
                new BlockSpec { Faces = null, Tags = Array.Empty<string>() },
                "(Fallback-Definition, kein Content gefunden)");
        }

        return builder.Build();
    }
}
