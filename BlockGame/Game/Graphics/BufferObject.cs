using Silk.NET.OpenGL;

namespace Basics.Game.Graphics
{
    /// <summary>
    /// Verwaltet ein OpenGL-Buffer-Objekt (VBO) für Vertex-Daten.
    /// Bietet eine abstrahierte Schnittstelle zum Erstellen, Binden und Freigeben von Pufferobjekten auf der GPU.
    /// </summary>
    /// <typeparam name="TDataType">Der Typ der im Puffer gespeicherten Daten. Muss ein unmanaged Typ sein.</typeparam>
    public class BufferObject<TDataType> : IDisposable
        where TDataType : unmanaged
    {
        private uint _handle;
        private BufferTargetARB _bufferType;
        private GL _gl;

        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="BufferObject{TDataType}"/>-Klassens.
        /// Erstellt einen neuen Puffer auf der GPU und lädt die angegebenen Daten hoch.
        /// </summary>
        /// <param name="gl">Die OpenGL-Instanz.</param>
        /// <param name="data">Die Daten, die in den Puffer geladen werden sollen.</param>
        /// <param name="bufferType">Der Typ des Puffers (z. B. VertexBuffer, ElementArrayBuffer).</param>
        public unsafe BufferObject(GL gl, ReadOnlySpan<TDataType> data, BufferTargetARB bufferType)
        {
            _gl = gl;
            _bufferType = bufferType;

            _handle = _gl.GenBuffer();
            Bind();
            fixed (void* d = data)
            {
                _gl.BufferData(bufferType, (nuint) (data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
            }
        }

        /// <summary>
        /// Bindet den Puffer für nachfolgende OpenGL-Operationen.
        /// </summary>
        public void Bind()
        {
            _gl.BindBuffer(_bufferType, _handle);
        }

        /// <summary>
        /// Gibt den Puffer frei und löscht ihn von der GPU.
        /// </summary>
        public void Dispose()
        {
            _gl.DeleteBuffer(_handle);
        }
    }
}