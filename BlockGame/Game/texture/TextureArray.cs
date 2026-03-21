using System;
using System.IO;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Basics.Graphics;

public class TextureArray : IDisposable
{
    private uint _handle;
    private GL _gl;

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

        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int) GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int) GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int) GLEnum.NearestMipmapNearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int) GLEnum.Nearest);
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