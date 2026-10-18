using Basics.Game.Content.Registries;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Game.Graphics;

/// <summary>
/// Renderer-seitiger Builder für das GL-Texturarray: baut aus einem CPU-seitigen
/// <see cref="TextureCatalog"/> die eigentliche <see cref="TextureArray"/>.
/// Trennt die reinen Registrierungsdaten (Katalog) vom OpenGL-Upload,
/// damit Registry und Tests ohne GL-Kontext funktionieren.
/// Muss auf dem Main-Thread laufen (GL-Interaktion).
/// </summary>
public static class TextureArrayBuilder
{
    /// <summary>
    /// Lädt alle Texturen aus dem Katalog in ein <see cref="TextureArray"/>.
    /// Die Layer-Reihenfolge des Katalogs (Index = Layer-Index) bleibt erhalten.
    /// </summary>
    /// <param name="gl">Der GL-Kontext.</param>
    /// <param name="catalog">Der CPU-seitige Texturkatalog.</param>
    /// <returns>Das upload-fertige Texturarray.</returns>
    public static TextureArray Build(GL gl, TextureCatalog catalog)
    {
        string[] paths = new string[catalog.Count];
        for (int i = 0; i < catalog.OrderedEntries.Count; i++)
        {
            paths[i] = catalog.OrderedEntries[i].FullPath;
        }

        return new TextureArray(gl, paths);
    }
}
