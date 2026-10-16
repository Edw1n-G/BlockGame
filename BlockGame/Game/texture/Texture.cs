using Silk.NET.OpenGL;
using StbImageSharp;

namespace Basics.Game.texture
{
    /// <summary>
    /// Verwaltet eine einzelne OpenGL-2D-Textur.
    /// Lädt Texturen aus Dateien oder Speicher und bindet sie für das Rendering.
    /// </summary>
    public class Texture : IDisposable
    {
        private uint _handle;
        private GL _gl;

        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="Texture"/>-Klassens.
        /// Lädt eine Textur aus einer Datei und erstellt ein OpenGL-Texture-Objekt.
        /// </summary>
        /// <param name="gl">Die OpenGL-Instanz.</param>
        /// <param name="path">Der Pfad zur Texture-Datei.</param>
        public unsafe Texture(GL gl, string path)
        {
            _gl = gl;

            _handle = _gl.GenTexture();
            Bind();
            
            ImageResult result = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
            
            fixed (byte* ptr = result.Data)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint) result.Width, 
                    (uint) result.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }

            SetParameters();
        }
        
        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="Texture"/>-Klassens aus rohen Byte-Daten.
        /// Ermöglicht das Erstellen von Texturen aus programmgesteuert generierten Daten.
        /// </summary>
        /// <param name="gl">Die OpenGL-Instanz.</param>
        /// <param name="data">Die rohen Texture-Daten.</param>
        /// <param name="width">Die Breite der Textur in Pixeln.</param>
        /// <param name="height">Die Höhe der Textur in Pixeln.</param>
        public unsafe Texture(GL gl, Span<byte> data, uint width, uint height)
        {
            _gl = gl;

            _handle = _gl.GenTexture();
            Bind();

            fixed (void* d = &data[0])
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int) InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, d);
                SetParameters();
            }
        }

        /// <summary>
        /// Legt die Textur-Parameter für Wrapping und Filtering fest.
        /// Verwendet ClampToEdge für Wrapping, Nearest für Magnification
        /// und NearestMipmapNearest für Minification mit Mipmaps.
        /// </summary>
        private void SetParameters()
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) GLEnum.NearestMipmapNearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxAnisotropy, 16.0f);
            
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        /// <summary>
        /// Bindet die Textur an den angegebenen Texture-Slot.
        /// </summary>
        /// <param name="textureSlot">Der Texture-Slot, an den gebunden werden soll (standardmäßig Texture0).</param>
        public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
        {
            _gl.ActiveTexture(textureSlot);
            _gl.BindTexture(TextureTarget.Texture2D, _handle);
        }

        /// <summary>
        /// Gibt die Textur frei und löscht sie von der GPU.
        /// </summary>
        public void Dispose()
        {
            _gl.DeleteTexture(_handle);
        }
    }
}