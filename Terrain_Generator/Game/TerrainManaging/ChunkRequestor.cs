using System.Numerics;
using Basics.Graphics;
using Basics.Utilities;
using Silk.NET.OpenGL;

namespace Basics.Game;

/// <summary>
/// Berechnet basierend auf der Spielerposition welche Chunks geladen/entladen werden sollen.
/// Abonniert das OnChunkChanged Event der Kamera.
/// </summary>
public class ChunkRequestor
{
    private readonly ChunkProvidor _chunkProvidor;
    private readonly Camera _camera;
    private int _renderDistance = 4; // Render-Distanz in Chunks
    private HashSet<ChunkCoord> _activeChunks = new();

    public int RenderDistance
    {
        get => _renderDistance;
        set => _renderDistance = Math.Max(1, value);
    }

    public ChunkRequestor(Camera camera, ChunkProvidor chunkProvidor)
    {
        _camera = camera;
        _chunkProvidor = chunkProvidor;

        // Event abonnieren: wird gefeuert wenn der Spieler einen neuen Chunk betritt
        _camera.OnChunkChanged += OnPlayerChunkChanged;
    }

    /// <summary>
    /// Event-Handler: Wird aufgerufen wenn der Spieler in einen neuen Chunk wechselt.
    /// Berechnet welche Chunks im Render-Radius liegen und fordert sie an.
    /// </summary>
    private void OnPlayerChunkChanged(ChunkCoord playerChunk)
    {
        HashSet<ChunkCoord> newActiveChunks = new();

        // Alle Chunks im Render-Radius berechnen (kreisförmig auf der XZ-Ebene)
        for (int x = -_renderDistance; x <= _renderDistance; x++)
        {
            for (int z = -_renderDistance; z <= _renderDistance; z++)
            {
                // Kreisförmige Distanzprüfung statt quadratisch
                if (x * x + z * z > _renderDistance * _renderDistance)
                    continue;

                ChunkCoord coord = new ChunkCoord(playerChunk.X + x, 0, playerChunk.Z + z);
                newActiveChunks.Add(coord);

                // Chunk anfordern (ChunkProvidor prüft ob er schon geladen ist)
                _chunkProvidor.RequestChunk(coord);
            }
        }

        // Chunks entladen die außerhalb des Render-Radius liegen
        UnloadDistantChunks(newActiveChunks);

        _activeChunks = newActiveChunks;
    }

    /// <summary>
    /// Entlädt Chunks die nicht mehr im aktiven Set sind.
    /// </summary>
    private void UnloadDistantChunks(HashSet<ChunkCoord> newActiveChunks)
    {
        // Alle Chunks finden die vorher aktiv waren aber jetzt nicht mehr
        foreach (ChunkCoord oldChunk in _activeChunks)
        {
            if (!newActiveChunks.Contains(oldChunk))
            {
                // TODO: Hier später auch Chunk-Daten auf Festplatte speichern
                _chunkProvidor.UnloadChunk(oldChunk);
            }
        }
    }
}