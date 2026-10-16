using Silk.NET.Maths;

namespace Basics.PhysicsSystem.Structs;

/// <summary>
/// Ergebnis eines Voxel-Raycastings.
/// Enthält Informationen über den Treffer, inklusive Position und Normalenvektor.
/// </summary>
public struct BlockResult
{ 
    /// <summary>Gibt an, ob der Raycast einen Block getroffen hat.</summary>
    public bool Hit;
    /// <summary>Die Weltkoordinaten des Trefferpunkts.</summary>
    public Vector3D<int> HitPosition;
    /// <summary>Die Normalenrichtung der getroffenen Blockseite.</summary>
    public Vector3D<int> HitNormal;
}

/// <summary>
/// Ergebnis eines Entity-Raycastings.
/// </summary>
public struct EntityResult
{
    /// <summary>Gibt an, ob der Raycast eine Entity getroffen hat.</summary>
    public bool Hit;
    /// <summary>Die eindeutige ID der getroffenen Entity.</summary>
    public int EntityID;
    /// <summary>Die Weltkoordinaten des Trefferpunkts.</summary>
    public Vector3D<float> HitPosition;
}
