using Silk.NET.OpenGL;

namespace Basics.Game.Graphics
{
    /// <summary>
    /// Verwaltet ein OpenGL-Vertex-Array-Objekt (VAO), das Vertex-Attribute und Indizes für das Rendern bündelt.
    /// Bindet VertexBuffer (VBO) und ElementBuffer (EBO) und konfiguriert die Vertex-Attribute für den Shader.
    /// </summary>
    /// <typeparam name="TVertexType">Der Typ der Vertex-Daten.</typeparam>
    /// <typeparam name="TIndexType">Der Typ der Index-Daten.</typeparam>
    public class VertexArrayObject<TVertexType, TIndexType> : IDisposable
        where TVertexType : unmanaged
        where TIndexType : unmanaged
    {
        private uint _handle;
        private GL _gl;

        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="VertexArrayObject{TVertexType, TIndexType}"/>-Klassens.
        /// Erstellt ein VAO und bindet die angegebenen VBO und EBO.
        /// </summary>
        /// <param name="gl">Die OpenGL-Instanz.</param>
        /// <param name="vbo">Das VertexBuffer-Objekt.</param>
        /// <param name="ebo">Das ElementBuffer-Objekt.</param>
        public VertexArrayObject(GL gl, BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)
        {
            _gl = gl;

            _handle = _gl.GenVertexArray();
            Bind();
            vbo.Bind();
            ebo.Bind();
        }

        /// <summary>
        /// Konfiguriert einen Vertex-Attribut-Pointer für Float-Daten.
        /// </summary>
        /// <param name="index">Der Index des Vertex-Attributes im Shader.</param>
        /// <param name="count">Die Anzahl der Komponenten pro Attribut (z. B. 3 für Vector3).</param>
        /// <param name="type">Der Typ der Daten (z. B. Float, Double).</param>
        /// <param name="vertexSize">Die Gesamtgröße eines Vertex in Floats.</param>
        /// <param name="offset">Der Offset des Attributs innerhalb eines Vertex in Bytes.</param>
        public unsafe void VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offSet)
        {
            _gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint) sizeof(TVertexType), (void*) (offSet * sizeof(TVertexType)));
            _gl.EnableVertexAttribArray(index);
        }
        
        /// <summary>
        /// Konfiguriert einen Vertex-Attribut-Pointer für Integer-Daten.
        /// Wird verwendet, um Attributedaten wie Textur-IDs als Integer zu übergeben, damit sie nicht normalisiert werden.
        /// </summary>
        /// <param name="index">Der Index des Vertex-Attributes im Shader.</param>
        /// <param name="count">Die Anzahl der Komponenten pro Attribut.</param>
        /// <param name="type">Der Typ der Integer-Daten.</param>
        /// <param name="vertexSize">Die Gesamtgröße eines Vertex.</param>
        /// <param name="offSet">Der Offset des Attributs innerhalb eines Vertex in Bytes.</param>
        public unsafe void VertexAttributeIPointer(uint index, int count, VertexAttribIType type, uint vertexSize, int offSet)
        {
            _gl.VertexAttribIPointer(index, count, type, vertexSize * (uint) sizeof(TVertexType), (void*) (offSet * sizeof(TVertexType)));
            _gl.EnableVertexAttribArray(index);
        }

        /// <summary>
        /// Bindet das VAO für nachfolgende Render-Aufrufe.
        /// </summary>
        public void Bind()
        {
            _gl.BindVertexArray(_handle);
        }

        /// <summary>
        /// Gibt das VAO frei und löscht es von der GPU.
        /// Die VBO und EBO werden nicht gelöscht, da sie auch von anderen VAOs verwendet werden können.
        /// </summary>
        public void Dispose()
        {
            _gl.DeleteVertexArray(_handle);
        }
    }
}