using Basics.Game.Configurations;
using Silk.NET.OpenGL;

namespace Basics.Game.texture;

internal static class BlockTextureLoader
{
    public static TextureArray Load(GL gl, BlockRegistry registry, string contentRoot = "Content")
    {
        string root = BlockRegistry.ResolveContentRoot(contentRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Content root nicht gefunden: {Path.GetFullPath(root)}");

        registry.InitializeHeadless(root);
        if (registry.TexturePaths.Count == 0)
            throw new InvalidDataException($"Keine Texturen unter '{root}/<namespace>/Textures' gefunden.");

        return new TextureArray(gl, registry.TexturePaths);
    }
}
