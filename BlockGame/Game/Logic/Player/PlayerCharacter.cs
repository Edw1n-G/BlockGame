using System.Numerics;
using Basics.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.PhysicsSystem;
using Basics.Game.Utilities;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.PhysicsSystem.Structs;

namespace Basics.Game.Logic.Player;

/// <summary>
/// Repräsentiert den Spielercharakter mit Kamera, Bewegung und Interaktionsmöglichkeiten.
/// </summary>
public class PlayerCharacter
{
    /// <summary>Die Standard-Block-ID zum Platzieren (Erde).</summary>
    private static ushort DefaultPlaceBlockId => BlockLoader.GetBlockId("core:dirt");

    private readonly World? _world;
    private readonly Physics? _physics;

    /// <summary>Die Kamera des Spielers.</summary>
    public Camera Camera { get; }

    /// <summary>Event, das ausgelöst wird, wenn der Spieler den Chunk wechselt.</summary>
    public event Action<ChunkCoord>? OnChunkChanged;
    private ChunkCoord _currentChunkCoord;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="PlayerCharacter"/>.
    /// </summary>
    /// <param name="spawnPosition">Die Startposition des Spielers in Weltkoordinaten.</param>
    /// <param name="world">Die optionale <see cref="World"/>-Instanz für Block-Interaktionen.</param>
    /// <param name="physics">Die optionale <see cref="Physics"/>-Instanz für Raycasting.</param>
    public PlayerCharacter(Vector3 spawnPosition, World? world = null, Physics? physics = null)
    {
        _world = world;
        _physics = physics;
        Camera = new Camera(spawnPosition);
        _currentChunkCoord = Camera.GetChunkCoord(Camera.Position);
        UIManager.OnRenderDistanceChanged += ForceChunkUpdate;
        BindActions();
    }

    /// <summary>
    /// Erzwingt eine sofortige Aktualisierung der Chunk-Steuerung.
    /// Wird verwendet, um den Chunk-Wechsel-Event manuell auszulösen.
    /// </summary>
    public void ForceChunkUpdate()
    {
        _currentChunkCoord = Camera.GetChunkCoord(Camera.Position);
        OnChunkChanged?.Invoke(_currentChunkCoord);
    }

    /// <summary>
    /// Bewegt den Spieler in die angegebene Richtung.
    /// </summary>
    /// <param name="direction">Die Bewegungsrichtung (X=links/rechts, Y=hoch/runter, Z=vor/zurück).</param>
    /// <remarks>
    /// Die Y-Komponente wird ignoriert, um den Spieler auf dem Boden zu halten.
    /// Bei Chunk-Wechseln wird das <see cref="OnChunkChanged"/>-Event ausgelöst.
    /// </remarks>
    public void Move(Vector3 direction)
    {
        // Y aus dem Frontvektor entfernen, um auf den Boden zu bleiben.
        Vector3 groundedFront = Vector3.Normalize(new Vector3(Camera.Front.X, 0, Camera.Front.Z));

        // Relativ zur Blickrichtung bewegen.
        Camera.Position += groundedFront * direction.Z;
        Camera.Position += Camera.Right * direction.X;
        Camera.Position += Camera.GlobalUp * direction.Y;

        ChunkCoord newChunk = Camera.GetChunkCoord(Camera.Position);
        if (newChunk != _currentChunkCoord)
        {
            _currentChunkCoord = newChunk;
            OnChunkChanged?.Invoke(_currentChunkCoord);
        }
    }

    /// <summary>
    /// Bindet die Tasten- und Mausaktionen an die PlayerCharacter-Methoden.
    /// </summary>
    private void BindActions()
    {
        InputManager.SetActionBindings(Actions.DestroyBlock, DestroyBlock);
        InputManager.SetActionBindings(Actions.PlaceBlock, PlaceBlock);
    }

    /// <summary>
    /// Zerstört den Block, der in der Blickrichtung des Spielers getroffen wird.
    /// </summary>
    private void DestroyBlock()
    {
        BlockResult hit = _physics != null
            ? _physics.CastRay(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f)
            : Physics.Raycast(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f);

        if (hit.Hit)
        {
            if (_world != null)
            {
                _world.SetBlock(hit.HitPosition.X, hit.HitPosition.Y, hit.HitPosition.Z, 0);
            }
            else
            {
                World.ModifyBlock(hit.HitPosition.X, hit.HitPosition.Y, hit.HitPosition.Z, 0);
            }
        }
    }

    /// <summary>
    /// Plaziert einen Block neben dem Block, der in der Blickrichtung getroffen wurde.
    /// </summary>
    private void PlaceBlock()
    {
        BlockResult hit = _physics != null
            ? _physics.CastRay(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f)
            : Physics.Raycast(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f);

        if (hit.Hit)
        {
            // Block neben die getroffene Fläche setzen
            int placeX = hit.HitPosition.X + hit.HitNormal.X;
            int placeY = hit.HitPosition.Y + hit.HitNormal.Y;
            int placeZ = hit.HitPosition.Z + hit.HitNormal.Z;

            if (_world != null)
            {
                _world.SetBlock(placeX, placeY, placeZ, DefaultPlaceBlockId);
            }
            else
            {
                World.ModifyBlock(placeX, placeY, placeZ, DefaultPlaceBlockId);
            }
        }
    }
}