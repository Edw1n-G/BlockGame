using Silk.NET.OpenGL;
using StbImageSharp;

namespace Basics.Game.texture;

/// <summary>
/// Verwaltet ein Texture-Array (Texture2DArray) für das Batch-Rendering mehrerer Texturen.
/// Ermöglicht das Laden mehrerer Texturen als einzelnes Array und schnelles Wechseln der Textur-Layer im Shader.
/// </summary>
public class TextureArray : IDisposable
{
    private uint _handle;
    private GL _gl;

    /// <summary>
    /// Initialisiert eine neue Instanz des <see cref="TextureArray"/>-Klassens.
    /// Lädt mehrere Texturen aus einzelnen Dateien und erstellt ein Texture-Array.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="texturePaths">Ein schreibgeschütztes Kollektion mit Pfaden zu den Texturen.</param>
    /// <exception cref="ArgumentException">Wird ausgelöst, wenn keine Texturen angegeben wurden.</exception>
    /// <exception cref="InvalidDataException">Wird ausgelöst, wenn Texturen unterschiedliche Größen haben.</exception>
    public TextureArray(GL gl, IReadOnlyList<string> texturePaths)
    {
        if (texturePaths == null || texturePaths.Count == 0)
        {
            throw new ArgumentException("Mindestens eine Textur wird benötigt.", nameof(texturePaths));
        }

        _gl = gl;
        _handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2DArray, _handle);

        ImageResult first = LoadImage(texturePaths[0]);
        int width = first.Width;
        int height = first.Height;

        unsafe
        {
            gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, (uint)texturePaths.Count, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        UploadLayer(gl, first, 0);
        for (int i = 1; i < texturePaths.Count; i++)
        {
            ImageResult image = LoadImage(texturePaths[i]);
            if (image.Width != width || image.Height != height)
            {
                throw new InvalidDataException(
                    $"Texturgröße stimmt nicht: '{texturePaths[i]}' hat {image.Width}x{image.Height}, erwartet {width}x{height}.");
            }

            UploadLayer(gl, image, i);
        }

        SetSamplerParameters();
    }

    /// <summary>
    /// Initialisiert eine neue Instanz des <see cref="TextureArray"/>-Klassens aus einem Texture-Atlas.
    /// Teilt eine große Textur in gleichgroße Tiles auf und erstellt ein Texture-Array.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="atlasPath">Der Pfad zur Texture-Atlas-Datei.</param>
    /// <param name="tileSize">Die Größe jedes Tiles in Pixeln (standardmäßig 32).</param>
    public TextureArray(GL gl, string atlasPath, int tileSize = 32)
    {
        _gl = gl;
        
        ImageResult result = ImageResult.FromMemory(File.ReadAllBytes(atlasPath), ColorComponents.RedGreenBlueAlpha);

        int tilesX = result.Width / tileSize;
        int tilesY = result.Height / tileSize;
        int totalLayers = tilesX * tilesY;

        _handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2DArray, _handle);

        unsafe
        {
            gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba8,
                (uint)tileSize, (uint)tileSize, (uint)totalLayers, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        int layerIndex = 0;
        for (int y = 0; y < tilesY; y++)
        {
            for (int x = 0; x < tilesX; x++)
            {
                byte[] tileData = new byte[tileSize * tileSize * 4];
                for (int row = 0; row < tileSize; row++)
                {
                    int flippedRow = tileSize - 1 - row;
                    for (int col = 0; col < tileSize; col++)
                    {
                        int atlasIndex = ((y * tileSize + row) * result.Width + (x * tileSize + col)) * 4;
                        int tileIndex = (flippedRow * tileSize + col) * 4;
                        Array.Copy(result.Data, atlasIndex, tileData, tileIndex, 4);
                    }
                }

                unsafe
                {
                    fixed (byte* ptr = tileData)
                    {
                        gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0,
                            layerIndex, (uint)tileSize, (uint)tileSize, 1,
                            PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                    }
                }
                layerIndex++;
            }
        }

        SetSamplerParameters();
    }

    /// <summary>
    /// Lädt ein Bild aus einer Datei.
    /// </summary>
    /// <param name="path">Der Pfad zur Bild-Datei.</param>
    /// <returns>Das geladene Bild als ImageResult.</returns>
    private static ImageResult LoadImage(string path)
    {
        return ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
    }

    /// <summary>
    /// Flipped die Bildzeilen, um die Textur-Koordinatenrichtung korrekt zu machen.
    /// Wird verwendet, bevor Textur-Daten an die GPU übertragen werden.
    /// </summary>
    /// <param name="image">Das zu spiegelnde Bild.</param>
    /// <returns>Das gespiegelte Bild als Byte-Array.</returns>
    private static byte[] FlipRows(ImageResult image)
    {
        int rowBytes = image.Width * 4;
        byte[] flipped = new byte[image.Data.Length];
        for (int row = 0; row < image.Height; row++)
        {
            int srcOffset = row * rowBytes;
            int dstOffset = (image.Height - 1 - row) * rowBytes;
            System.Buffer.BlockCopy(image.Data, srcOffset, flipped, dstOffset, rowBytes);
        }

        return flipped;
    }

    /// <summary>
    /// Lädt einen einzelnen Layer in das Texture-Array.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="image">Das Bild, das als Layer geladen werden soll.</param>
    /// <param name="layerIndex">Der Index des Layers im Array.</param>
    private static unsafe void UploadLayer(GL gl, ImageResult image, int layerIndex)
    {
        byte[] flipped = FlipRows(image);
        fixed (byte* ptr = flipped)
        {
            gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0,
                layerIndex, (uint)image.Width, (uint)image.Height, 1,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
    }

    /// <summary>
    /// Legt die Sampler-Parameter für Wrapping und Filtering fest.
    /// Verwendet ClampToEdge für Wrapping, Nearest für Magnification
    /// und NearestMipmapNearest für Minification mit Mipmaps.
    /// </summary>
    private void SetSamplerParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.NearestMipmapNearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 8);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxAnisotropy, 16.0f);

        _gl.GenerateMipmap(TextureTarget.Texture2DArray);
    }

    /// <summary>
    /// Bindet das Texture-Array an den angegebenen Texture-Slot.
    /// </summary>
    /// <param name="textureSlot">Der Texture-Slot, an den gebunden werden soll (standardmäßig Texture0).</param>
    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(textureSlot);
        _gl.BindTexture(TextureTarget.Texture2DArray, _handle);
    }

    /// <summary>
    /// Gibt das Texture-Array frei und löscht es von der GPU.
    /// </summary>
    public void Dispose()
    {
        if (_handle != 0)
        {
            _gl.DeleteTexture(_handle);
            _handle = 0;
        }
    }
}