using Silk.NET.Maths;

namespace Basics.PhysicsSystem.Structs;

public struct BlockResult
{ 
    public bool Hit;                    // Haben wir etwas getroffen?
    public Vector3D<int> HitPosition; // Wo wurde was getroffen
    public Vector3D<int> HitNormal;     // block Seite
}

public struct EntityResult
{
    public bool Hit;
    public int EntityID;
    public Vector3D<float> HitPosition; 
}
