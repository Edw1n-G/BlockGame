using System.Text.Json;
using System.Text.Json.Serialization;

namespace Basics.Game.Content.Registries;

/// <summary>
/// Die Textur-Zuweisungen eines Block-JSON: optionale Einzelseiten plus
/// "sides" als Standard für alle nicht explizit genannten Seiten.
/// Wird vom <see cref="BlockJsonParser"/> befüllt und vom
/// <see cref="BlockRegistryBuilder"/> über den Texturkatalog aufgelöst.
/// </summary>
public sealed class BlockTextureFaces
{
    /// <summary>Textur-ID der Oberseite (optional, sonst "sides").</summary>
    public string? Top { get; set; }

    /// <summary>Textur-ID der Unterseite (optional, sonst "sides").</summary>
    public string? Bottom { get; set; }

    /// <summary>Textur-ID der Frontseite (optional, sonst "sides").</summary>
    public string? Front { get; set; }

    /// <summary>Textur-ID der Rückseite (optional, sonst "sides").</summary>
    public string? Back { get; set; }

    /// <summary>Textur-ID der linken Seite (optional, sonst "sides").</summary>
    public string? Left { get; set; }

    /// <summary>Textur-ID der rechten Seite (optional, sonst "sides").</summary>
    public string? Right { get; set; }

    /// <summary>Standard-Textur-ID für alle nicht explizit genannten Seiten.</summary>
    public string? Sides { get; set; }
}

/// <summary>
/// Ein geparster (aber noch nicht aufgelöster) Block aus einem Block-JSON:
/// die Textur-Zuweisungen als Ressourcennamen plus die Tags.
/// Der <see cref="BlockRegistryBuilder"/> löst die Texturen über den
/// Texturkatalog auf und erzeugt daraus die endgültige Block-Definition.
/// </summary>
public sealed class BlockSpec
{
    /// <summary>
    /// Die Textur-Zuweisungen als Ressourcennamen. <c>null</c> nur für
    /// Fallback-Blöcke ohne Texturen (alle Layer bleiben 0).
    /// </summary>
    public BlockTextureFaces? Faces { get; init; }

    /// <summary>Die normalisierten Tags des Blocks.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Parst Block-JSON in einen <see cref="BlockSpec"/> — rein und ohne Seiteneffekte,
/// dadurch mit String-Literalen testbar. Validiert das Format und wirft bei Fehlern
/// eine beschreibende <see cref="InvalidDataException"/> mit der Quelldatei.
/// Ein späteres Schema (z. B. "model" statt "textures") wird über
/// <see cref="FormatVersion"/> managesierbar.
/// </summary>
public static class BlockJsonParser
{
    /// <summary>Die aktuell unterstützte Schemaversion der Block-JSONs.</summary>
    public const int SupportedFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parst Block-JSON und validiert es.
    /// </summary>
    /// <param name="json">Der JSON-Text der Blockdatei.</param>
    /// <param name="sourcePath">Die Quelldatei, für aussagekräftige Fehlermeldungen.</param>
    /// <returns>Der geparste <see cref="BlockSpec"/>.</returns>
    /// <exception cref="InvalidDataException">Wird bei ungültigem JSON, fehlenden Texturen oder
    /// nicht unterstützter <see cref="FormatVersion"/> ausgelöst.</exception>
    public static BlockSpec Parse(string json, string sourcePath)
    {
        BlockFileJson? file;
        try
        {
            file = JsonSerializer.Deserialize<BlockFileJson>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Ungültiges Block-JSON in {sourcePath}: {exception.Message}", exception);
        }

        if (file is null)
        {
            throw new InvalidDataException($"Leere Blockdatei: {sourcePath}");
        }

        if (file.FormatVersion is not (null or SupportedFormatVersion))
        {
            throw new InvalidDataException(
                $"Nicht unterstütztes format_version {file.FormatVersion} in {sourcePath} " +
                $"(unterstützt: {SupportedFormatVersion}).");
        }

        if (file.Textures is null)
        {
            throw new InvalidDataException($"Blockdatei ohne gültigen 'textures'-Abschnitt: {sourcePath}");
        }

        string[] tags = file.Tags is null
            ? Array.Empty<string>()
            : file.Tags.Select(NormalizeTag).ToArray();

        return new BlockSpec
        {
            Faces = file.Textures,
            Tags = tags,
        };
    }

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidDataException("Leerer Tag in einer Blockdatei.");
        }

        return tag.Trim().ToLowerInvariant();
    }

    private sealed class BlockFileJson
    {
        // "name" ist informatisch — die kanonische ID kommt aus dem Dateinamen.
        public string? Name { get; set; }

        [JsonPropertyName("format_version")]
        public int? FormatVersion { get; set; }

        public string[]? Tags { get; set; }

        public BlockTextureFaces? Textures { get; set; }
    }
}
