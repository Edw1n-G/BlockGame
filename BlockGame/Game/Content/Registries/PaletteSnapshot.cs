namespace Basics.Game.Content.Registries;

/// <summary>
/// Reine Funktionen für Palette-Export und Remapping gespeicherter Blockdaten.
/// Dadurch sind gespeicherte Welten (Saves/Netzwerk) immun gegen geänderte
/// Load-Reihenfolgen, umbenannte Blöcke oder entfernte Mod-Erweiterungen:
/// Zusammen mit den Blockdaten wird die Palette (ID → Name) gespeichert und
/// beim Laden über <see cref="CreateRemap"/> auf die dann aktuelle Palette abgebildet.
/// </summary>
public static class PaletteSnapshot
{
    /// <summary>
    /// Erstellt eine Remap-Tabelle, die Block-IDs einer gespeicherten Palette auf die
    /// IDs der aktuellen Palette abbildet (Index = alte ID, Wert = neue ID).
    /// Unbekannte Namen (auch null/leer) werden auf Luft (0) abgebildet — entfernte Blöcke
    /// werden zu Luft, analog zu anderen Voxel-Engines. Über nicht mehr existierende Namen
    /// wird optional auf <paramref name="warnings"/> gewarnt, damit ein fehlender Mod beim
    /// nächsten Speichern nicht unbemerkt Blöcke verliert.
    /// </summary>
    /// <param name="savedPalette">Die gespeicherte Palette (Index = alte ID, Wert = Name).</param>
    /// <param name="currentPalette">Die aktuelle Palette (Index = ID, Wert = Name).</param>
    /// <param name="warnings">Optionaler Ausgabe-Stream für Warnungen über unbekannte Namen.</param>
    /// <returns>Die Remap-Tabelle für <see cref="RemapBlockArray"/>.</returns>
    public static int[] CreateRemap(
        IReadOnlyList<string> savedPalette,
        IReadOnlyList<string> currentPalette,
        TextWriter? warnings = null)
    {
        var currentIds = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < currentPalette.Count; i++)
        {
            currentIds[currentPalette[i]] = (ushort)i;
        }

        var remap = new int[savedPalette.Count];
        List<string>? unknownNames = null;
        for (int i = 0; i < savedPalette.Count; i++)
        {
            string? name = savedPalette[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                remap[i] = 0;
                continue;
            }

            if (currentIds.TryGetValue(name.Trim(), out ushort id))
            {
                remap[i] = id;
            }
            else
            {
                remap[i] = 0;
                (unknownNames ??= new List<string>()).Add(name);
            }
        }

        if (unknownNames is not null && warnings is not null)
        {
            warnings.WriteLine(
                $"Palette: {unknownNames.Count} Blockname(n) aus der gespeicherten Palette " +
                $"existieren nicht mehr und werden zu Luft gemappt: {string.Join(", ", unknownNames)}");
        }

        return remap;
    }

    /// <summary>
    /// Mappt ein Block-ID-Array (z. B. ChunkData.Blocks) mit der Remap-Tabelle
    /// auf die IDs der aktuellen Palette. Arbeitet in-place ohne Allokation.
    /// </summary>
    /// <param name="blocks">Das Block-ID-Array, das in-place remappt wird.</param>
    /// <param name="remapTable">Die Remap-Tabelle von <see cref="CreateRemap"/>.</param>
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
}
