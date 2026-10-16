using System;
using System.IO;
using System.Numerics;
using Silk.NET.OpenGL;

namespace Basics.Graphics
{
    /// <summary>
    /// Lädt, kompiliert und verwaltet OpenGL-Shader sowie das Setzen von Uniform-Variablen.
    /// Stellt Methoden zum Laden von Vertex- und Fragment-Shadern aus Dateien und zum Binden von Uniforms bereit.
    /// </summary>
    public class Shader : IDisposable
    {
        private uint _handle;
        private GL _gl;

        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="Shader"/>-Klassens.
        /// Lädt und kompiliert die Vertex- und Fragment-Shader, verknüpft sie zu einem Shader-Programm
        /// und prüft auf Kompilierungs- und Verknüpfungsfehler.
        /// </summary>
        /// <param name="gl">Die OpenGL-Instanz.</param>
        /// <param name="vertexPath">Der Pfad zur Vertex-Shader-Datei.</param>
        /// <param name="fragmentPath">Der Pfad zur Fragment-Shader-Datei.</param>
        /// <exception cref="Exception">Wird ausgelöst, wenn der Shader nicht kompiliert oder verknüpft werden kann.</exception>
        public Shader(GL gl, string vertexPath, string fragmentPath)
        {
            _gl = gl;

            uint vertex = LoadShader(ShaderType.VertexShader, vertexPath);
            uint fragment = LoadShader(ShaderType.FragmentShader, fragmentPath);
            _handle = _gl.CreateProgram();
            _gl.AttachShader(_handle, vertex);
            _gl.AttachShader(_handle, fragment);
            _gl.LinkProgram(_handle);
            _gl.GetProgram(_handle, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_handle)}");
            }
            _gl.DetachShader(_handle, vertex);
            _gl.DetachShader(_handle, fragment);
            _gl.DeleteShader(vertex);
            _gl.DeleteShader(fragment);
        }

        /// <summary>
        /// Bindet das Shader-Programm für die nachfolgenden Render-Aufrufe.
        /// </summary>
        public void Use()
        {
            _gl.UseProgram(_handle);
        }

        /// <summary>
        /// Setzt einen Integer-Uniform im Shader.
        /// </summary>
        /// <param name="name">Der Name des Uniform-Variables im Shader.</param>
        /// <param name="value">Der Wert, der gesetzt werden soll.</param>
        /// <exception cref="Exception">Wird ausgelöst, wenn der Uniform nicht gefunden wird.</exception>
        public void SetUniform(string name, int value)
        {
            int location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.Uniform1(location, value);
        }

        /// <summary>
        /// Setzt einen Float-Uniform im Shader.
        /// </summary>
        /// <param name="name">Der Name des Uniform-Variables im Shader.</param>
        /// <param name="value">Der Wert, der gesetzt werden soll.</param>
        /// <exception cref="Exception">Wird ausgelöst, wenn der Uniform nicht gefunden wird.</exception>
        public void SetUniform(string name, float value)
        {
            int location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.Uniform1(location, value);
        }
        
        /// <summary>
        /// Setzt einen Matrix4x4-Uniform im Shader.
        /// </summary>
        /// <param name="name">Der Name des Uniform-Variables im Shader.</param>
        /// <param name="value">Der Wert der Matrix, der gesetzt werden soll.</param>
        public unsafe void SetUniform(string name, Matrix4x4 value)
        {
            int location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                return;
            }
            
            _gl.UniformMatrix4(location, 1, false, (float*) &value);
        }
        
        /// <summary>
        /// Setzt einen Vector3-Uniform im Shader.
        /// </summary>
        /// <param name="name">Der Name des Uniform-Variables im Shader.</param>
        /// <param name="value">Der Wert des Vektors, der gesetzt werden soll.</param>
        public unsafe void SetUniform(string name, Vector3 value)
        {
            int location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                return;
            }
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Setzt einen Boolean-Uniform im Shader.
        /// </summary>
        /// <param name="name">Der Name des Uniform-Variables im Shader.</param>
        /// <param name="value">Der Wert, der gesetzt werden soll.</param>
        public unsafe void SetUniform(string name, bool value)
        {
            int location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                return;
            }
            _gl.Uniform1(location, value ? 1 : 0);
        }

        /// <summary>
        /// Gibt das Shader-Programm frei und löscht es von der GPU.
        /// </summary>
        public void Dispose()
        {
            _gl.DeleteProgram(_handle);
        }

        /// <summary>
        /// Lädt und kompiliert einen einzelnen Shader aus einer Datei.
        /// </summary>
        /// <param name="type">Der Typ des Shaders (Vertex, Fragment, etc.).</param>
        /// <param name="path">Der Pfad zur Shader-Datei.</param>
        /// <returns>Der Handle des kompilierten Shaders.</returns>
        /// <exception cref="Exception">Wird ausgelöst, wenn der Shader nicht kompiliert werden kann.</exception>
        private uint LoadShader(ShaderType type, string path)
        {
            string src = File.ReadAllText(path);
            uint handle = _gl.CreateShader(type);
            _gl.ShaderSource(handle, src);
            _gl.CompileShader(handle);
            string infoLog = _gl.GetShaderInfoLog(handle);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");
            }

            return handle;
        }
    }
}