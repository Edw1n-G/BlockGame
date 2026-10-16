using System;
using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace Basics.Game.PhysicsSystem;

public interface IPhysics
{
    Raycaster Raycaster { get; }
    BlockResult Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist);
}

/// <summary>
/// Verwaltet Physik- und Raycast-Operationen für eine Engine-Instanz
/// mit statischer Fassade für Gameplay-Skripte.
/// </summary>
public class Physics : IPhysics
{
    private readonly Raycaster _raycaster;

    public Physics(Raycaster raycaster)
    {
        _raycaster = raycaster ?? throw new ArgumentNullException(nameof(raycaster));
    }

    public Raycaster Raycaster => _raycaster;

    public BlockResult CastRay(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return _raycaster.CastBlockRay(start, dir, maxDist);
    }

    BlockResult IPhysics.Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return CastRay(start, dir, maxDist);
    }

    // =========================================================================
    // --- Statische Fassade für Gameplay-Skripte & Ergonomie ---
    // =========================================================================

    private static Physics? _activeInstance;

    public static Physics Current =>
        EngineContext.Current?.Physics ?? _activeInstance ?? throw new InvalidOperationException("Physics wurde nicht initialisiert!");

    public static void Initialize(Raycaster raycaster)
    {
        _activeInstance = new Physics(raycaster);
    }

    public static void SetActive(Physics physics)
    {
        _activeInstance = physics;
    }

    public static BlockResult Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return Current.CastRay(start, dir, maxDist);
    }
}