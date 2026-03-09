using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace Basics.PhysicsSystem
{
    /// <summary>
    /// Alle skripte können über statische Physics klasse alle nicht statischen Funktionen aufrufen
    /// ohne referenz auf die eigentliche Instanz zu haben.
    /// </summary>
    public static class Physics
    {
        // Die eigentliche Instanz liegt versteckt im Hintergrund
        private static Raycaster _raycaster;

        // Wird beim Spielstart einmal aufgerufen
        public static void Initialize(Raycaster raycaster)
        {
            _raycaster = raycaster;
        }

        // --- Global API ---
        // Jedes Skript kann Physics.Raycast aufrufen
        public static BlockResult Raycast(Vector3D<float> start, Vector3D<float> dir, float maxDist)
        {
            if (_raycaster == null) throw new System.Exception("Physics wurde nicht initialisiert!");
            
            return _raycaster.CastBlockRay(start, dir, maxDist);
        }
    }
}