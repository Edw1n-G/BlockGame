using System.Numerics;
using Basics.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.PhysicsSystem;
using Basics.Game.Utilities;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.PhysicsSystem.Structs;

namespace Basics.Game.Logic.Player;

public class PlayerCharacter
{
    private static readonly ushort DefaultPlaceBlockId = BlockTextures.GetBlockId("core:dirt");

    public Camera Camera { get; }

    public event Action<ChunkCoord>? OnChunkChanged;
    private ChunkCoord _currentChunkCoord;

    public PlayerCharacter(Vector3 spawnPosition)
    {
        Camera = new Camera(spawnPosition);
        _currentChunkCoord = Camera.GetChunkCoord(Camera.Position);
        UIManager.OnRenderDistanceChanged += ForceChunkUpdate;
        BindActions();
    }

    public void ForceChunkUpdate()
    {
        _currentChunkCoord = Camera.GetChunkCoord(Camera.Position);
        OnChunkChanged?.Invoke(_currentChunkCoord);
    }

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

    private void BindActions()
    {
        InputManager.SetActionBindings(Actions.DestroyBlock, DestroyBlock);
        InputManager.SetActionBindings(Actions.PlaceBlock, PlaceBlock);
    }

    private void DestroyBlock()
    {
        BlockResult hit = Physics.Raycast(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f);

        if (hit.Hit)
        {
            World.ModifyBlock(hit.HitPosition.X, hit.HitPosition.Y, hit.HitPosition.Z, 0);
        }
    }

    private void PlaceBlock()
    {
        BlockResult hit = Physics.Raycast(MathHelper.ToGeneric(Camera.Position), MathHelper.ToGeneric(Camera.Front), 5.0f);

        if (hit.Hit)
        {
            // Block neben die getroffene Fläche setzen
            int placeX = hit.HitPosition.X + hit.HitNormal.X;
            int placeY = hit.HitPosition.Y + hit.HitNormal.Y;
            int placeZ = hit.HitPosition.Z + hit.HitNormal.Z;

            World.ModifyBlock(placeX, placeY, placeZ, DefaultPlaceBlockId);
        }
    }
}