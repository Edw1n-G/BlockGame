using System;
using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace Basics.Game.PhysicsSystem;

/// <summary>
/// Interface für Physik- und Raycast-Operationen.
/// </summary>
public interface IPhysics
{
    /// <summary>Der für diesen Physik-Manager verwendete Raycaster.</summary>
    Raycaster Raycaster { get; }
    /// <summary>Führt einen Raycast durch und gibt das Ergebnis zurück.</summary>
    /// <param name="start">Startposition des Strahls in Weltkoordinaten.</param>
    /// <param name="dir">Richtungsvektor des Strahls.</param>
    /// <param name="maxDist">Maximale Reichweite des Strahls.</param>
    /// <returns>Ein <see cref="BlockResult"/> mit Trefferinformationen.</returns>
    BlockResult Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist);
}

/// <summary>
/// Verwaltet Physik- und Raycast-Operationen für eine Engine-Instanz.
/// Bietet eine statische Fassade für Gameplay-Skripte zur einfachen Verwendung.
/// </summary>
public class Physics : IPhysics
{
    private readonly Raycaster _raycaster;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="Physics"/>.
    /// </summary>
    /// <param name="raycaster">Der für diesen Physik-Manager zu verwendende <see cref="Raycaster"/>.</param>
    /// <exception cref="ArgumentNullException">Wird ausgelöst, wenn <paramref name="raycaster"/> null ist.</exception>
    public Physics(Raycaster raycaster)
    {
        _raycaster = raycaster ?? throw new ArgumentNullException(nameof(raycaster));
    }

    /// <summary>Der für diesen Physik-Manager verwendete Raycaster.</summary>
    public Raycaster Raycaster => _raycaster;

    /// <summary>Führt einen Raycast durch.</summary>
    /// <param name="start">Startposition des Strahls in Weltkoordinaten.</param>
    /// <param name="dir">Richtungsvektor des Strahls.</param>
    /// <param name="maxDist">Maximale Reichweite des Strahls.</param>
    /// <returns>Ein <see cref="BlockResult"/> mit Trefferinformationen.</returns>
    public BlockResult CastRay(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return _raycaster.CastBlockRay(start, dir, maxDist);
    }

    /// <inheritdoc/>
    BlockResult IPhysics.Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return CastRay(start, dir, maxDist);
    }

    // =========================================================================
    // --- Statische Fassade für Gameplay-Skripte & Ergonomie ---
    // =========================================================================

    private static Physics? _activeInstance;

    /// <summary>
    /// Gibt die aktuelle Instanz des Physik-Managers zurück.
    /// Nutzt den <see cref="EngineContext.Current"/> oder die statische Fallback-Instanz.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn der Physik-Manager nicht initialisiert wurde.</exception>
    public static Physics Current =>
        EngineContext.Current?.Physics ?? _activeInstance ?? throw new InvalidOperationException("Physics wurde nicht initialisiert!");

    /// <summary>
    /// Initialisiert die statische Fallback-Instanz des Physik-Managers.
    /// </summary>
    /// <param name="raycaster">Der für die Initialisierung zu verwendende <see cref="Raycaster"/>.</param>
    public static void Initialize(Raycaster raycaster)
    {
        _activeInstance = new Physics(raycaster);
    }

    /// <summary>
    /// Legt die aktive Instanz des Physik-Managers fest.
    /// </summary>
    /// <param name="physics">Die festzulegende Instanz von <see cref="Physics"/>.</param>
    public static void SetActive(Physics physics)
    {
        _activeInstance = physics;
    }

    /// <summary>
    /// Führt einen Raycast mithilfe der aktuellen Instanz durch.
    /// </summary>
    /// <param name="start">Startposition des Strahls in Weltkoordinaten.</param>
    /// <param name="dir">Richtungsvektor des Strahls.</param>
    /// <param name="maxDist">Maximale Reichweite des Strahls.</param>
    /// <returns>Ein <see cref="BlockResult"/> mit Trefferinformationen.</returns>
    public static BlockResult Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return Current.CastRay(start, dir, maxDist);
    }
}