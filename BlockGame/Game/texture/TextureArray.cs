using Silk.NET.OpenGL;
using StbImageSharp;

namespace Basics.Game.texture;

public class TextureArray : IDisposable
{
    private uint _handle;
    private GL _gl;

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

    public TextureArray(GL gl, string atlasPath, int tileSize = 32)
    {
        _gl = gl;
        
        //Das Flippen dreht die Texturen richtig aber die Indexierung der Tiles ist dann falschrum
        //Deswegen müssen textures einzelnd gedreht werden, damit die Indexierung der Tiles stimmt
        //StbImage.stbi_set_flip_vertically_on_load(1);
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

    private static ImageResult LoadImage(string path)
    {
        return ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
    }

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

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(textureSlot);
        _gl.BindTexture(TextureTarget.Texture2DArray, _handle);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_handle);
    }
}