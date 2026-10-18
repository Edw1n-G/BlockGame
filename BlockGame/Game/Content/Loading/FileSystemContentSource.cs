using Basics.Game.Content.Registries;

namespace Basics.Game.Content.Loading;

/// <summary>
/// Content-Quelle, die das Dateisystem nach Modul-Erweiterungen durchsucht:
/// Unterverzeichnisse des Content-Roots sind Namespaces; darin liegen
/// "Blocks/" (JSON-Definitionen) und "Textures/" (PNG-Dateien).
/// Alle Erweiterungen müssen vor dem Start im Dateisystem vorhanden sein —
/// es gibt keine Laufzeit-Registrierung.
/// </summary>
public sealed class FileSystemContentSource : IContentSource
{
    private readonly string _root;

    /// <summary>
    /// Erstellt eine neue Dateisystem-Quelle für den gegebenen Content-Root.
    /// </summary>
    /// <param name="contentRoot">Der Content-Root (Standard: "Content").</param>
    public FileSystemContentSource(string contentRoot)
    {
        _root = ResolveContentRoot(contentRoot);
    }

    /// <summary>Der aufgelöste absolute Pfad des Content-Roots.</summary>
    public string Root => _root;

    /// <summary>Gibt an, ob der Content-Root existiert.</summary>
    public bool Exists => Directory.Exists(_root);

    /// <summary>
    /// Löst einen Content-Root-Pfad auf, der für das Laden von Blöcken und Texturen verwendet wird.
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

    /// <inheritdoc />
    public IEnumerable<ContentEntry> Enumerate(ContentKind kind)
    {
        if (!Directory.Exists(_root))
        {
            yield break;
        }

        string subDirectory = kind switch
        {
            ContentKind.Texture => "Textures",
            ContentKind.Block => "Blocks",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        foreach (string namespaceDirectory in Directory.EnumerateDirectories(_root))
        {
            string namespaceName = Path.GetFileName(namespaceDirectory).ToLowerInvariant();
            string kindDirectory = Path.Combine(namespaceDirectory, subDirectory);
            if (!Directory.Exists(kindDirectory))
            {
                continue;
            }

            string searchPattern = kind == ContentKind.Texture ? "*.png" : "*.json";
            foreach (string file in Directory.EnumerateFiles(kindDirectory, searchPattern, SearchOption.AllDirectories))
            {
                string entryName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string fullName = $"{namespaceName}:{entryName}";
                if (!ResourceId.TryParse(fullName, out ResourceId id))
                {
                    continue;
                }

                yield return new ContentEntry
                {
                    Id = id,
                    SourcePath = file,
                    Kind = kind,
                    ReadText = () => File.ReadAllText(file),
                };
            }
        }
    }
}
