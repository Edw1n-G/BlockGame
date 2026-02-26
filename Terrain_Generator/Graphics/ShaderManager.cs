using Silk.NET.OpenGL;
using StbImageSharp;
using System;
using System.IO;
using System.Numerics;
using Basics.Game;
using Basics.Setup;
using Basics.Utilities;

namespace Basics.Graphics;

/**
 * Die Klasse verwaltet die Shaderdateien und die Texturen
 * Shader werden von der Shader Klasse kompiliert, dann hier benutzt.
 * Mit dem Camera Objekt wird die Richtige Sicht gesetzt
 */
public class ShaderManager : IDisposable
{
    
    // Die Tutorial-Shader und Texture Klassen
    private Shader _shader;
    private GL _gl; 
    
    
    public ShaderManager(GL gl, string vertexShaderFile, string fragmentShaderFile)
    {
        _gl = gl;
        

        // 1. Shader laden
        // Pfad anpassen, da deine Dateien im Graphics/Shader Ordner liegen könnten
        string vertPath = "Graphics/Shader/" + vertexShaderFile;
        string fragPath = "Graphics/Shader/" + fragmentShaderFile;
        
        _shader = new Shader(gl, vertPath, fragPath);
    }
    
    //Shader "Aktivieren" und die Uniforms setzen
    public unsafe void Use(GL gl, Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        
        gl.CullFace(GLEnum.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);
        
        // Shader aktivieren
        _shader.Use();
        
        var size = WindowSetup.window.FramebufferSize;
        var view = camera.GetViewMatrix();
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), (float)size.X / size.Y, 0.1f, 1000.0f);
        
        // Dem Shader sagen, dass die Textur auf Slot 0 liegt
        _shader.SetUniform("uTexture", 0);
        
        
        _shader.SetUniform("uView", view);
        _shader.SetUniform("uProjection", projection);
        
    }
    
    //Um ein Objekt and die richtige Stelle zu setzten.
    public void SetModelMatrix(Matrix4x4 model)
    {
        _shader.SetUniform("uModel", model);
    }
    
    public void BindTexture(Texture texture)
    {
        texture.Bind(TextureUnit.Texture0);
    }

    public void Dispose()
    {
        _shader.Dispose();
    }
}