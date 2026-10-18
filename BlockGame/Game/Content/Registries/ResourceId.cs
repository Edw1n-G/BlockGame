namespace Basics.Game.Content.Registries;

/// <summary>
/// Werttyp für einen normalisierten Ressourcen-Bezeichner "namespace:name" (z. B. "core:stone").
/// Parst, validiert und lowercased die Zeichenkette genau einmal — String-Normalisierung
/// verteilt sich danach nicht mehr über den Code.
/// Wird für Blöcke, Texturen und später Items/Models verwendet; falsch typisierte
/// Bezeichner werden damit zur Compile-Zeit unterscheidbar.
/// </summary>
public readonly struct ResourceId : IEquatable<ResourceId>
{
    /// <summary>
    /// Der interne Name für Luft — ein hartes Invariant: Luft ist immer ID 0.
    /// </summary>
    public const string AirName = "core:air";

    private readonly string _fullName;

    private ResourceId(string fullName)
    {
        _fullName = fullName;
    }

    /// <summary>Der normalisierte vollständige Name ("namespace:name", kleingeschrieben).</summary>
    public string FullName => _fullName;

    /// <summary>Der Namespace-Teil vor dem Doppelpunkt.</summary>
    public string Namespace => _fullName[.._fullName.IndexOf(':')];

    /// <summary>Der Name-Teil nach dem Doppelpunkt.</summary>
    public string Name => _fullName[(_fullName.IndexOf(':') + 1)..];

    /// <summary>
    /// Parst einen Bezeichner und normalisiert ihn (Trim + Kleinbuchstaben).
    /// </summary>
    /// <param name="value">Die zu parsende Zeichenkette ("namespace:name").</param>
    /// <param name="sourceDescription">Optionale Quelle (Dateipfad), für aussagekräftige Fehlermeldungen.</param>
    /// <returns>Der geparste <see cref="ResourceId"/>.</returns>
    /// <exception cref="FormatException">Wird bei fehlendem oder leerem Namespace/Name ausgelöst.</exception>
    public static ResourceId Parse(string value, string? sourceDescription = null)
    {
        if (TryParse(value, out ResourceId id))
        {
            return id;
        }

        string source = sourceDescription is null ? string.Empty : $" (Quelle: {sourceDescription})";
        throw new FormatException($"Ungültiger Ressourcen-Bezeichner '{value}'{source}. " +
                                  "Erwartetes Format: 'namespace:name' mit nicht-leerem Namespace und Namen.");
    }

    /// <summary>
    /// Versucht, einen Bezeichner zu parsen und zu normalisieren.
    /// </summary>
    public static bool TryParse(string? value, out ResourceId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        int separator = normalized.IndexOf(':');
        if (separator <= 0 || separator == normalized.Length - 1 || normalized.IndexOf(':', separator + 1) >= 0)
        {
            return false;
        }

        id = new ResourceId(normalized);
        return true;
    }

    public bool Equals(ResourceId other) => string.Equals(_fullName, other._fullName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ResourceId other && Equals(other);

    public override int GetHashCode() => _fullName.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => _fullName;

    public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);

    public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);
}
