using System;
using System.Collections.Generic;
using Basics.Game;
using Basics.Game.Configurations;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Configurations;

/// <summary>
/// Statische Fassade für Block- und Texturabfragen.
/// Delegiert an die aktive BlockRegistry des aktuellen EngineContext bzw. die Standardinstanz.
/// </summary>
public static class BlockLoader
{
    private static BlockRegistry? _defaultRegistry;

    public static BlockRegistry Current =>
        EngineContext.Current?.BlockRegistry ?? (_defaultRegistry ??= CreateDefaultRegistry());

    private static BlockRegistry CreateDefaultRegistry()
    {
        var registry = new BlockRegistry();
        if (System.IO.Directory.Exists("Content"))
        {
            try
            {
                registry.InitializeHeadless("Content");
            }
            catch
            {
                // Headless Fallback falls Content unvollständig ist
            }
        }
        return registry;
    }

    public const int Top = BlockRegistry.Top;
    public const int Bottom = BlockRegistry.Bottom;
    public const int Front = BlockRegistry.Front;
    public const int Back = BlockRegistry.Back;
    public const int Left = BlockRegistry.Left;
    public const int Right = BlockRegistry.Right;

    public static ushort Air => 0;

    public static TextureArray TerrainTexture => Current.TerrainTexture;

    public static void Initialize(GL gl, string contentRoot = "Content")
    {
        Current.Initialize(gl, contentRoot);
    }

    public static void InitializeHeadless(string contentRoot = "Content")
    {
        Current.InitializeHeadless(contentRoot);
    }

    public static void Reset()
    {
        Current.Reset();
    }

    public static ushort Get(int blockId, int faceIndex)
    {
        return Current.Get(blockId, faceIndex);
    }

    public static ushort GetBlockId(string blockName)
    {
        return Current.GetBlockId(blockName);
    }

    public static bool HasTag(ushort blockId, string tag)
    {
        return Current.HasTag(blockId, tag);
    }

    public static IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        return Current.GetByTag(tag);
    }
}
